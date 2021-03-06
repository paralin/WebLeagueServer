﻿#define SEND_DATA_TO_EVERYONE

using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dota2.GC.Internal;
using log4net;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.Builders;
using WLNetwork.Bots;
using WLNetwork.Chat;
using WLNetwork.Clients;
using WLNetwork.Database;
using WLNetwork.Hubs;
using WLNetwork.Matches.Enums;
using WLNetwork.Matches.Methods;
using WLNetwork.Model;
using WLNetwork.Properties;
using WLNetwork.Rating;
using WLNetwork.Utils;

//#define ENABLE_LEAVER_PENALTY

namespace WLNetwork.Matches
{
    /// <summary>
    ///     A instance of a match.
    /// </summary>
    public class MatchGame
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly bool _initFinished = false;
        private readonly ConcurrentBag<string> forbidSids = new ConcurrentBag<string>();
        private ActiveMatch _activeMatch = null;
        private bool _alreadyAttemptedMatchResult = false;
        private bool _balancing;
        private MatchGameInfo _info;
        private ObservableRangeCollection<MatchPlayer> _players;
        private MatchSetup _setup;
        private BotController controller = null;
        public volatile bool Destroyed;

        /// <summary>
        ///     This is for two picks in captains, set to true at start so first pick is just 1
        /// </summary>
        private bool pickedAlready;

        /// <summary>
        ///     Try to recover a match from a stored active match.
        /// </summary>
        /// <param name="match"></param>
        private MatchGame(ActiveMatch match)
        {
            Id = match.Id;
            Info = match.Info;
            Setup = new MatchSetup(match.Id, match.Details);
            Setup.Details.IsRecovered = true;
            pickedAlready = true;
            Players = new ObservableRangeCollection<MatchPlayer>(match.Details.Players);
            KickSpectators();
            KickUnassigned();
            Players.CollectionChanged += PlayersOnCollectionChanged;

            var ebot = BotDB.Bots.Values.FirstOrDefault(m => m.Username == match.Details.Bot.Username);
            if (ebot != null)
                Setup.Details.Bot = ebot;
            else
            {
                ebot = Mongo.Bots.FindOneAs<Bot>(Query<Bot>.EQ(m => m.Username, match.Details.Bot.Username));
                if (ebot != null)
                    BotDB.Bots[ebot.Id] = ebot;
                else
                {
                    log.Warn("Can't find bot for " + match.Id + "! Dropping match...");
                    return;
                }
            }

            controller = new BotController(Setup.Details);
            controller.instance.Start();

            MatchesController.Games.Add(this);
            log.Info("MATCH RESTORE [" + match.Id + "] [" + Info.Owner + "] [" + Info.GameMode + "] [" + Info.MatchType +
                     "]");

            _activeMatch = match;
            SaveActiveGame();
        }

        /// <summary>
        ///     Create a new game with options
        /// </summary>
        /// <param name="options"></param>
        public MatchGame(string owner, MatchCreateOptions options)
        {
            Id = Guid.NewGuid();
            Info = new MatchGameInfo
            {
                Id = Id,
                Public = true,
                Status = MatchStatus.Players,
                MatchType = options.MatchType,
                Owner = owner,
                GameMode = options.GameMode,
                Opponent = options.OpponentSID,
                CaptainStatus = CaptainsStatus.DirePick,
                League = options.League,
                LeagueSeason = options.LeagueSeason,
                LeagueTicket = options.LeagueTicket,
                LeagueRegion = options.LeagueRegion,
                SecondaryLeagueSeason = options.SecondaryLeagueSeason,
            };
            pickedAlready = true;
            Players = new ObservableRangeCollection<MatchPlayer>();
            Players.CollectionChanged += PlayersOnCollectionChanged;
            MatchesController.Games.Add(this);
            //note: Don't add to public games as it's not started yet
            log.Info("MATCH CREATE [" + Id + "] [" + owner + "] [" + options.GameMode + "] [" + options.MatchType + "]");
            _initFinished = true;
        }

        /// <summary>
        ///     ID of the game.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        ///     Public info about the match.
        /// </summary>
        public MatchGameInfo Info
        {
            get { return _info; }
            set
            {
                _info = value;
                if (_initFinished)
                    TransmitInfoUpdate();
            }
        }

        /// <summary>
        ///     Players
        /// </summary>
        public ObservableRangeCollection<MatchPlayer> Players
        {
            get { return _players; }
            set
            {
                lock (value)
                {
                    _players = value;
                    if (_initFinished)
                    {
                        Hubs.Matches.HubContext.Clients.All.MatchPlayersSnapshot(Id, value.ToArray());
                        Admin.HubContext.Clients.All.MatchPlayersSnapshot(Id, value.ToArray());
                        if (_activeMatch != null)
                            SaveActiveGame();
                    }
                }
            }
        }

        public MatchSetup Setup
        {
            get { return _setup; }
            set
            {
                _setup = value;
                if (_initFinished)
                    TransmitSetupUpdate();
            }
        }

        /// <summary>
        ///     Called when the lobby is not recovered
        /// </summary>
        public void LobbyNotRecovered()
        {
            if (Setup?.Details != null && Setup.Details.MatchId != 0)
            {
                Info.Status = MatchStatus.Complete;
                Info = Info;
                controller.StartAttemptResult();
            }
            else
                ProcessMatchResult(EMatchResult.Unknown);
        }

        public void StartPicks()
        {
            if (Info.Status != MatchStatus.Players) return;
            Info.Status = MatchStatus.Teams;
            pickedAlready = true;
            Info.CaptainStatus = CaptainsStatus.DirePick;
            foreach (MatchPlayer player in Players.Where(m => !m.IsCaptain && m.Team != MatchTeam.Spectate))
            {
                player.Team = MatchTeam.Unassigned;
            }
            Info = Info;
        }

        public void StartSetup()
        {
            if (Setup != null || (Info.Status != MatchStatus.Players && Info.MatchType != MatchType.Captains)) return;
            RebalanceTeams();
            Players = Players;
            Setup = new MatchSetup(Id, new MatchSetupDetails
            {
                Id = Id,
                GameMode = Info.GameMode,
                Password = RandomPassword.CreateRandomPassword(9),
                Players = Players.ToArray(),
                TicketID = Info.LeagueTicket,
                Region = Info.LeagueRegion,
            });
            Info.Status = MatchStatus.Lobby;
            Info = Info;
            BotDB.RegisterSetup(Setup);
        }

        private void PlayersOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            /* Don't rebalance every time players change now
            if (_balancing) return;
            RebalanceTeams();
             */
            Players = Players;
            if (Setup != null && Setup.Details != null && Setup.Details.Players != null)
            {
                Setup.Details.Players = Players.ToArray();
                Setup = Setup;
            }
        }

        /// <summary>
        ///     Determines whether the supplied binary team is valid
        /// </summary>
        private static bool IsValidTeam(MatchPlayer[] playerPool, uint team)
        {
            uint v = (uint) team;
            uint c;
            for (c = 0; v != 0; v >>= 1)
            {
                c += v & 1;
            }
            return (c == playerPool.Length/2);
        }

        /// <summary>
        ///     Calculates the team score for a binary team
        /// </summary>
        private static int CalculateTeamScore(MatchPlayer[] playerPool, uint team)
        {
            var score = 0;
            for (var i = 0; i < playerPool.Length; ++i)
            {
                if ((team & 1) == 1)
                    score += (int) playerPool[i].Rating;
                team >>= 1;
            }
            return score;
        }

        private static void ApplyBinaryTeams(MatchPlayer[] playerPool, uint team)
        {
            foreach (MatchPlayer t in playerPool)
            {
                t.Team = (team & 1) == 1 ? MatchTeam.Radiant : MatchTeam.Dire;
                team >>= 1;
            }
        }

        /// <summary>
        ///     Balance teams
        /// </summary>
        public void RebalanceTeams()
        {
            if (_balancing || Info.MatchType != MatchType.StartGame || Info.Status > MatchStatus.Lobby) return;
            _balancing = true;

            var playerPool =
                Players.Where(
                    m => m.Team == MatchTeam.Radiant || m.Team == MatchTeam.Dire || m.Team == MatchTeam.Unassigned)
                    .ToArray();

            uint min = uint.MaxValue;
            uint minTeam = 0;
            for (uint team = 0; team < (1 << (playerPool.Length - 1)); ++team)
            {
                if (!IsValidTeam(playerPool, team))
                    continue;

                var scoreDiff = Math.Abs(CalculateTeamScore(playerPool, team) - CalculateTeamScore(playerPool, ~team));
                if (scoreDiff >= min) continue;

                min = (uint) scoreDiff;
                minTeam = team;
            }

            ApplyBinaryTeams(playerPool, minTeam);

            _balancing = false;
        }

        /// <summary>
        ///     Delete the game.
        /// </summary>
        public void Destroy()
        {
            Destroyed = true;
            if (_activeMatch != null)
            {
                Mongo.ActiveMatches.Remove(Query<ActiveMatch>.EQ(m => m.Id, Id));
                _activeMatch = null;
            }

            foreach (var cli in BrowserClient.Clients.Where(m => m.Value.Match == this))
            {
                Hubs.Matches.HubContext.Clients.Group(Id.ToString()).ClearMatch();
                Hubs.Matches.HubContext.Groups.Remove(cli.Key, Id.ToString());
            }

            if (Setup != null)
            {
                BotDB.SetupQueue.Remove(Setup);
                Setup.Details.Cleanup(true);
            }

            if (MatchesController.Games.Contains(this))
            {
                MatchesController.Games.Remove(this);
                log.Info("MATCH DESTROY [" + Id + "]");
            }
        }

        ~MatchGame()
        {
            //Destructor
            Destroy();
        }

        /// <summary>
        ///     Send out a setup snapshot.
        /// </summary>
        private void TransmitSetupUpdate()
        {
            if (_setup == null || _setup.Details == null)
                Hubs.Matches.HubContext.Clients.All.ClearSetup(Id.ToString());
            else
            {
                lock (_setup)
                {
                    Bot bot = _setup.Details.Bot;
                    _setup.Details.Bot = null;
                    Hubs.Matches.HubContext.Clients.All.SetupSnapshot(_setup);
                    _setup.Details.Bot = bot;
                    if (_activeMatch != null) SaveActiveGame();
                }
            }
        }

        /// <summary>
        ///     Send out an info snapshot.
        /// </summary>
        private void TransmitInfoUpdate()
        {
            if (_info == null) return;
            Hubs.Matches.HubContext.Clients.All.InfoSnapshot(_info);
            if (_activeMatch != null) SaveActiveGame();
        }

        /// <summary>
        ///     Start the match in-game
        /// </summary>
        public void StartMatch()
        {
            if (Info.Status == MatchStatus.Play) return;
            controller?.instance.StartMatch();
            Setup.Details.Status = MatchSetupStatus.Done;
            Setup = Setup;
            Info.Status = MatchStatus.Play;
            Info = Info;
        }

        /// <summary>
        ///     Kick unassigned players.
        /// </summary>
        public void KickUnassigned()
        {
            MatchPlayer[] toRemove = Players.Where(m => m.Team == MatchTeam.Unassigned).ToArray();
            Players.RemoveRange(toRemove);
        }

        /// <summary>
        ///     Kick all spectators.
        /// </summary>
        public void KickSpectators()
        {
            MatchPlayer[] toRemove = Players.Where(m => m.Team == MatchTeam.Spectate).ToArray();
            Players.RemoveRange(toRemove);
        }

        /// <summary>
        ///     Updates/saves active game info
        /// </summary>
        public void SaveActiveGame()
        {
            if (_activeMatch == null) _activeMatch = new ActiveMatch();
            _activeMatch.Id = Id;
            _activeMatch.Details = Setup.Details;
            _activeMatch.Info = Info;
            Mongo.ActiveMatches.Save(_activeMatch);
        }

        /// <summary>
        ///     Pick a player
        /// </summary>
        /// <param name="sid"></param>
        public void PickPlayer(string sid, MatchTeam team)
        {
            //NOTE! The players list only rerenders when the player object is updated. As a result, we need to send the player team update AFTER the info update.
            if (
                !(team == MatchTeam.Dire && Info.CaptainStatus == CaptainsStatus.DirePick ||
                  team == MatchTeam.Radiant && Info.CaptainStatus == CaptainsStatus.RadiantPick))
                return;
            MatchPlayer player = Players.FirstOrDefault(m => m.SID == sid && m.Team != MatchTeam.Spectate);
            if (player == null || player.Team != MatchTeam.Unassigned) return;
            player.Team = team;
            if (Players.Count(m => m.Team == MatchTeam.Radiant || m.Team == MatchTeam.Dire) >= 10)
            {
                Info.CaptainStatus = CaptainsStatus.DirePick;
                KickUnassigned();
                StartSetup();
                pickedAlready = true;
            }
            else
            {
                if (pickedAlready)
                {
                    Info.CaptainStatus = Info.CaptainStatus == CaptainsStatus.DirePick
                        ? CaptainsStatus.RadiantPick
                        : CaptainsStatus.DirePick;
                    Info = Info;
                    pickedAlready = false;
                }
                else
                {
                    pickedAlready = true;
                }
            }
            Task.Run(() =>
            {
                Thread.Sleep(100);
                Players = Players;
            });
        }

        /// <summary>
        ///     Complete the game
        /// </summary>
        /// <param name="outcome"></param>
        /// <param name="manualResult">was an admin result</param>
        public void ProcessMatchResult(EMatchResult outcome, bool manualResult = false)
        {
            if (!MatchesController.Games.Contains(this))
                return;

            if (Info.SecondaryLeagueSeason == null) Info.SecondaryLeagueSeason = new uint[] {};

            ulong matchId = Setup.Details.MatchId;
            var countMatch = (outcome == EMatchResult.DireVictory ||
                              outcome == EMatchResult.RadVictory);

            log.Debug("PROCESSING " + (manualResult ? "ADMIN " : "") + "RESULT " + matchId + " WITH " +
                      outcome.ToString("G"));
            var result = new MatchResult
            {
                Id = matchId,
                MatchId = Id.ToString(),
                MatchCompleted = DateTime.UtcNow,
                Players =
                    Players.Where(m => m.Team == MatchTeam.Dire || m.Team == MatchTeam.Radiant)
                        .Select(x => new MatchResultPlayer(x))
                        .ToArray(),
                Result = outcome,
                MatchCounted = countMatch && Info.MatchType != MatchType.OneVsOne,
                MatchType = Info.MatchType,
                League = Info.League,
                LeagueSeason = Info.LeagueSeason,
                LeagueSecondarySeasons = Info.SecondaryLeagueSeason ?? new uint[0],
                TicketId = Info.LeagueTicket
            };

            if (countMatch && Info.MatchType != MatchType.OneVsOne)
            {
                RatingCalculator.CalculateRatingDelta(result);

                result.ApplyRating(Info.SecondaryLeagueSeason.Concat(new[] {Info.LeagueSeason}).ToArray());
            }

            result.Save();

            if (countMatch)
            {
                Hubs.Matches.HubContext.Clients.Group(Id.ToString()).ResultSnapshot(result);

                var completeMsg = "Match " + Id.ToString().Substring(0, 4) + " (MatchID " + Setup.Details.MatchId +
                                  ") " + (manualResult ? "admin resulted" : "completed") + " with ";
                if (Info.MatchType != MatchType.OneVsOne)
                    completeMsg +=
                        (outcome == EMatchResult.DireVictory
                            ? "dire victory."
                            : "radiant victory.");
                else
                {
                    var direplyr = Setup.Details.Players.FirstOrDefault(m => m.Team == MatchTeam.Dire);
                    var radplyr = Setup.Details.Players.FirstOrDefault(m => m.Team == MatchTeam.Radiant);
                    if (direplyr == null || radplyr == null)
                        completeMsg += ".... an unknown 1v1 victory! I'm sure it was glorious!";
                    else
                    {
                        completeMsg += (outcome == EMatchResult.RadVictory ? radplyr : direplyr).Name +
                                       "'s crushing victory over " +
                                       (outcome == EMatchResult.RadVictory ? direplyr : radplyr).Name +
                                       "!";
                    }
                }
                if (result.StreakEndedRating > 0 && Info.MatchType != MatchType.OneVsOne)
                {
                    var max = result.EndedWinStreaks.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
                    var plyr = (Players.FirstOrDefault(m => m.SID == max));
                    if (plyr != null)
                        completeMsg += " " + (result.Result == EMatchResult.RadVictory ? "Radiant" : "Dire") +
                                       " received a bonus " + result.StreakEndedRating + " rating for ending " +
                                       plyr.Name + "'s " + result.EndedWinStreaks[max] + " win streak!";
                }
                if (Info.League != null) ChatChannel.SystemMessage(Info.League, completeMsg);
            }
            else
            {
                var reason = "some unknown reason";
                switch (outcome)
                {
                    case EMatchResult.RadVictory:
                    case EMatchResult.DireVictory:
                        reason = "leavers";
                        break;
                    case EMatchResult.Unknown:
                        reason = "unknown match result, admin will confirm result and apply rating";
                        break;
                    case EMatchResult.DontCount:
                        reason = "admin purging the game";
                        break;
                }
                if (Info.League != null) ChatChannel.SystemMessage(Info.League, $"Match not counted due to {reason}.");
            }

            Destroy();
        }

        /// <summary>
        ///     Retreive the bot controller. Needed so it doesn't get serialized to JSON
        /// </summary>
        /// <returns></returns>
        public BotController GetBotController()
        {
            return controller;
        }

        /// <summary>
        ///     Clear bot controller
        /// </summary>
        public void SetBotController(BotController controller)
        {
            this.controller = controller;
        }

        /// <summary>
        ///     Admin destroying this match
        /// </summary>
        public void AdminDestroy()
        {
            log.Debug("ADMIN DESTROY " + Id);
            Hubs.Matches.HubContext.Clients.Group(Id.ToString())
                .SystemMessage("Admin Closed Match", "An admin has destroyed the match you were in.");
            if (Setup != null) ProcessMatchResult(EMatchResult.DontCount);
            else Destroy();
        }

        /// <summary>
        ///     Static, recover all active matches
        /// </summary>
        public static void RecoverActiveMatches()
        {
            foreach (var match in Mongo.ActiveMatches.FindAllAs<ActiveMatch>())
            {
                log.Debug("RECOVERING MATCH " + match.Id + "...");
                new MatchGame(match).SaveActiveGame();
            }
        }

        /// <summary>
        ///     Called when the match is started
        /// </summary>
        public void GameStarted()
        {
            if (Info.League == null) return;
            // Announce win streaks
            foreach (
                var plyr in
                    Players.Where(m => m.Team == MatchTeam.Dire || m.Team == MatchTeam.Radiant)
                        .Where(plyr => plyr.WinStreak >= Settings.Default.MinWinStreakForAnnounce))
                ChatChannel.SystemMessage(Info.League, $"{plyr.Name} has a {plyr.WinStreak} win streak!");
        }

        /// <summary>
        ///     Boot a player from the startgame.
        /// </summary>
        /// <param name="sid"></param>
        public void KickPlayer(string sid)
        {
            BrowserClient first;
            if (!BrowserClient.ClientsBySteamID.TryGetValue(sid, out first) || first.Match != this) return;
            var plyr = Players.FirstOrDefault(m => m.SID == sid);
            if (plyr != null && plyr.Team == MatchTeam.Spectate) return;
            first.LeaveMatch();
            Hubs.Matches.HubContext.Clients.Group(sid).OnKickedFromMatch();
            forbidSids.Add(sid);
        }

        /// <summary>
        ///     Check if a player is forbidden from this match
        /// </summary>
        /// <param name="steamid"></param>
        /// <returns></returns>
        public bool PlayerForbidden(string steamid)
        {
            return forbidSids.Contains(steamid);
        }

        /// <summary>
        ///     Transmits a snapshot to everyone in the match
        /// </summary>
        public void TransmitSnapshot()
        {
            // Send a match snapshot
            //Hubs.Matches.HubContext.Clients.Group(Id.ToString()).AvailableGameUpdate(new [] { this });
            Hubs.Matches.HubContext.Clients.All.AvailableGameUpdate(new[] {this});
        }
    }

    /// <summary>
    ///     Match information.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class MatchGameInfo
    {
        /// <summary>
        ///     ID of this match
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        ///     Match type
        /// </summary>
        public MatchType MatchType { get; set; }

        /// <summary>
        ///     Is this match public?
        /// </summary>
        public bool Public { get; set; }

        /// <summary>
        ///     Status of the match
        /// </summary>
        public MatchStatus Status { get; set; }

        /// <summary>
        ///     Owner of this match.
        /// </summary>
        public string Owner { get; set; }

        /// <summary>
        ///     Game mode
        /// </summary>
        public GameMode GameMode { get; set; }

        /// <summary>
        ///     Opponent, if a challenge
        /// </summary>
        public string Opponent { get; set; }

        /// <summary>
        ///     Captain status.
        /// </summary>
        public CaptainsStatus CaptainStatus { get; set; }

        /// <summary>
        ///     League ID
        /// </summary>
        public string League { get; set; }

        /// <summary>
        ///     Season
        /// </summary>
        public uint LeagueSeason { get; set; }

        /// <summary>
        ///     Ticket for this league
        /// </summary>
        public uint LeagueTicket { get; set; }

        /// <summary>
        ///     Region for this league
        /// </summary>
        public uint LeagueRegion { get; set; }

        /// <summary>
        ///     League season
        /// </summary>
        public uint[] SecondaryLeagueSeason { get; set; }
    }

    public static class MatchGameExt
    {
        /// <summary>
        ///     How many players in a team?
        /// </summary>
        /// <param name="plyrs">Players</param>
        /// <param name="team">Team</param>
        /// <returns></returns>
        public static int TeamCount(this ObservableCollection<MatchPlayer> plyrs, MatchTeam team)
        {
            return plyrs.Count(m => m.Team == team);
        }

        /// <summary>
        ///     Find the MatchPlayer for a user.
        /// </summary>
        /// <param name="plyrs"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public static MatchPlayer ForUser(this ObservableCollection<MatchPlayer> plyrs, User user)
        {
            if (user == null) return null;
            return plyrs.FirstOrDefault(m => m.SID == user.steam.steamid);
        }

        /// <summary>
        ///     Is this player in the team?
        /// </summary>
        /// <param name="players"></param>
        /// <param name="player"></param>
        /// <returns></returns>
        public static bool HasPlayer(this ObservableCollection<MatchPlayer> players, MatchPlayer player)
        {
            return players.Contains(player);
        }
    }
}
