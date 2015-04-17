#define SEND_DATA_TO_EVERYONE

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dota2.GC.Dota.Internal;
using log4net;
using MongoDB.Driver.Builders;
using TentacleSoftware.TeamSpeakQuery.ServerQueryResult;
using WLNetwork.Bots;
using WLNetwork.Chat;
using WLNetwork.Controllers;
using WLNetwork.Database;
using WLNetwork.Matches.Enums;
using WLNetwork.Matches.Methods;
using WLNetwork.Model;
using WLNetwork.Rating;
using WLNetwork.Utils;
using WLNetwork.Voice;
using XSockets.Core.XSocket.Helpers;
using MatchType = WLNetwork.Matches.Enums.MatchType;

namespace WLNetwork.Matches
{
    /// <summary>
    ///     A instance of a match.
    /// </summary>
    public class MatchGame
    {
        private static readonly Admin Admins = new Admin();
        private static readonly Controllers.Matches Matches = new Controllers.Matches();
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private bool _balancing;
        private MatchGameInfo _info;
        private ObservableRangeCollection<MatchPlayer> _players;
        private MatchSetup _setup;
        private ActiveMatch _activeMatch = null;

        public BotController controller = null;

        private List<string> ChannelNames = new List<string>(); 

        public static ConcurrentQueue<MatchGame> TSSetupQueue = new ConcurrentQueue<MatchGame>(); 

        /// <summary>
        ///     This is for two picks in captains, set to true at start so first pick is just 1
        /// </summary>
        private bool pickedAlready;

        /// <summary>
        /// Try to recover a match from a stored active match.
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
            Players.CollectionChanged += PlayersOnCollectionChanged;

            var ebot = BotDB.Bots.Values.FirstOrDefault(m => m.Username == match.Details.Bot.Username);
            if (ebot != null)
            {
                ebot.InUse = true;
                Setup.Details.Bot = ebot;
            }
            else
            {
                ebot = Mongo.Bots.FindOneAs<Bot>(Query<Bot>.EQ(m => m.Username, match.Details.Bot.Username));
                if (ebot != null)
                {
                    ebot.InUse = true;
                    BotDB.Bots[ebot.Id] = ebot;
                }
                else
                {
                    log.Warn("Can't find bot for " + match.Id + "! Dropping match...");
                    return;
                }
            }

            controller = new BotController(Setup.Details);
            controller.instance.Start();
            controller.instance.bot.LobbyNotRecovered +=
                (sender, args) => ProcessMatchResult(EMatchOutcome.k_EMatchOutcome_Unknown);

            MatchesController.Games.Add(this);
            log.Info("MATCH RESTORE [" + match.Id + "] [" + Info.Owner + "] [" + Info.GameMode + "] [" + Info.MatchType + "]");

            TSSetupQueue.Enqueue(this);
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
                CaptainStatus = CaptainsStatus.DirePick
            };
            pickedAlready = true;
            Players = new ObservableRangeCollection<MatchPlayer>();
            Players.CollectionChanged += PlayersOnCollectionChanged;
            MatchesController.Games.Add(this);
            //note: Don't add to public games as it's not started yet
            log.Info("MATCH CREATE [" + Id + "] [" + owner + "] [" + options.GameMode + "] [" + options.MatchType + "]");
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
#if SEND_DATA_TO_EVERYONE
                    Matches.InvokeTo(m => m.User != null, new MatchPlayersSnapshot(this), MatchPlayersSnapshot.Msg);
#else
                    Matches.InvokeTo(
                        m =>
                            m.User != null &&
                            ((Info.Public && Info.Status == MatchStatus.Players) || (m.Match == this)),
                        new MatchPlayersSnapshot(this), MatchPlayersSnapshot.Msg);
#endif
                    Admins.InvokeTo(
                        m =>
                            m.User != null,
                        new MatchPlayersSnapshot(this), MatchPlayersSnapshot.Msg);
                }
            }
        }

        public MatchSetup Setup
        {
            get { return _setup; }
            set
            {
                _setup = value;
                TransmitSetupUpdate();
            }
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
            Setup = new MatchSetup(Id, new MatchSetupDetails
            {
                Id = Id,
                GameMode = Info.GameMode,
                Password = RandomPassword.CreateRandomPassword(9),
                Players = Players.ToArray()
            });
            Info.Status = MatchStatus.Lobby;
            Info = Info;
            BotDB.RegisterSetup(Setup);
        }

        public void MovePlayersToChannel()
        {
            foreach (var plyr in Players.Where(m => m.Team == MatchTeam.Radiant || m.Team == MatchTeam.Dire))
                Teamspeak.Instance.ForceChannel[plyr.SID] = (plyr.Team == MatchTeam.Radiant ? "Radiant" : "Dire")+" "+Id.ToString().Substring(0, 4);
        }

        private void PlayersOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (_balancing) return;
            RebalanceTeams();
            Players = Players;
        }

        /// <summary>
        ///     Balance teams
        /// </summary>
        public void RebalanceTeams()
        {
            if (_balancing || Info.MatchType != MatchType.StartGame || Info.Status > MatchStatus.Lobby) return;
            _balancing = true;
            //MMR algorithm
            int direCount = 0;
            int radiCount = 0;
            foreach (
                MatchPlayer plyr in Players.Where(m => m.Team != MatchTeam.Spectate).OrderByDescending(m => m.Rating))
            {
                if (direCount < radiCount && direCount < 5)
                {
                    plyr.Team = MatchTeam.Dire;
                    direCount++;
                }
                else
                {
                    plyr.Team = MatchTeam.Radiant;
                    radiCount++;
                }
            }
            _balancing = false;
        }

        public volatile bool Destroyed;
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
            foreach (Controllers.Matches cont in Matches.Find(m => m.Match == this))
            {
                cont.Match = null;
            }
            if (Setup != null)
            {
                BotDB.SetupQueue.Remove(Setup);
                Setup.Details.Cleanup(true);
            }
            if (MatchesController.Games.Contains(this))
            {
                MatchesController.Games.Remove(this);
#if !SEND_DATA_TO_EVERYONE
                MatchesController.PublicGames.Remove(Info);
#endif
                log.Info("MATCH DESTROY [" + Id + "]");
            }
            DeleteTeamspeakChannels();
        }

        public async Task CreateTeamspeakChannels()
        {
            if (ChannelNames.Count > 0 || Destroyed) return;

            //Top level match channel
            string name;
            if (Info.MatchType == MatchType.StartGame)
                name = Players.First(m => m.IsCaptain || m.SID == Info.Owner).Name + "'s Startgame";
            else
            {
                var plyrs = Players.Where(m => m.IsCaptain).ToArray();
                name = plyrs[0].Name + " vs. " + plyrs[1].Name;
            }
            ChannelNames.Add(name);
            if (Destroyed) 
                {
                    DeleteTeamspeakChannels();
                    return;
                }
            var topLevel = Teamspeak.Instance.Channels[name] = new ChannelInfoResult()
            {
                ChannelName = name,
                ChannelCodecQuality = "10",
                ChannelDescription = "Top level channel for this match. Match ID is "+Id+".",
                ChannelFlagPermanent = "1"
            };
            await Teamspeak.Instance.SetupChannels();
            if (Destroyed)
            {
                DeleteTeamspeakChannels();
                return;
            }
            var uid = Id.ToString().Substring(0, 4);
            name = "Radiant "+uid;
            ChannelNames.Add(name);
            var radiant = Teamspeak.Instance.Channels[name] = new ChannelInfoResult()
            {
                ChannelName = name,
                ChannelCodecQuality = "10",
                ChannelDescription = "Channel for the radiant team.",
                ChannelFlagPassword = "1",
                ChannelPassword = Id.ToString()+"p",
                ChannelFlagPermanent = "1",
                Pid = topLevel.Cid
            };
            name = "Dire "+uid;
            ChannelNames.Add(name);
            var dire = Teamspeak.Instance.Channels[name] = new ChannelInfoResult()
            {
                ChannelName = name,
                ChannelCodecQuality = "10",
                ChannelDescription = "Channel for the dire team. Match ID is "+Id+".",
                ChannelFlagPassword = "1",
                ChannelPassword = Id+"p",
                ChannelFlagPermanent = "1",
                Pid = topLevel.Cid
            };
            await Teamspeak.Instance.SetupChannels();
            if (Destroyed)
            {
                DeleteTeamspeakChannels();
                return;
            }

            if (Setup.Details.IsRecovered && Info.Status > MatchStatus.Players)
            {
                MovePlayersToChannel();
            }

            if (Destroyed)
            {
                DeleteTeamspeakChannels();
                return;
            }
        }

        public void DeleteTeamspeakChannels()
        {
            ChannelInfoResult val;
            var sids = Teamspeak.Instance.ForceChannel.Values.Where(m => m.Contains(Id.ToString().Substring(0, 4))).ToArray();
            string bogus;
            foreach (var sid in sids) Teamspeak.Instance.ForceChannel.TryRemove(sid, out bogus);
            foreach (var chan in ChannelNames)
               Teamspeak.Instance.Channels.TryRemove(chan, out val);
            ChannelNames.Clear();
        }

        ~MatchGame()
        {
            //Destructor
            Destroy();
        }

        private void TransmitSetupUpdate()
        {
            if (_setup == null)
            {
                var arg = new ClearSetupMatch() {Id = Id};
                foreach (Controllers.Matches cont in Matches.Find(m => m.Match == this))
                {
                    cont.Invoke(arg, "clearsetupmatch");
                }
                Admins.InvokeTo(m => m.User != null, arg, "clearsetupmatch");
                foreach (var plyr in Players)
                {
                    string foobar;
                    Teamspeak.Instance.ForceChannel.TryRemove(plyr.SID, out foobar);
                }
            }
            else
            {
                lock (_setup)
                {
                    Bot bot = _setup.Details.Bot;
                    _setup.Details.Bot = null;
#if SEND_DATA_TO_EVERYONE
                    Matches.InvokeToAll(_setup, "setupsnapshot");
#else
                    foreach (Controllers.Matches cont in Matches.Find(m => m.Match == this))
                    {
                        cont.Invoke(_setup, "setupsnapshot");
                    }
#endif
                    Admins.InvokeTo(m => m.User != null, _setup, "setupsnapshot");
                    _setup.Details.Bot = bot;
                    if (_activeMatch != null) SaveActiveGame();
                }
            }
        }

        private void TransmitInfoUpdate()
        {
            if (_info != null)
            {
                foreach (Controllers.Matches cont in Matches.Find()) //Matches.Find(m => m.Match == this))
                {
                    cont.Invoke(_info, "infosnapshot");
                }
                Admins.InvokeTo(m => m.User != null, _info, "infosnapshot");
                if (_activeMatch != null) SaveActiveGame();
            }
        }

        /// <summary>
        ///     Start the match in-game
        /// </summary>
        public void StartMatch()
        {
            if (Info.Status == MatchStatus.Play) return;
            if (controller != null)
            {
                controller.instance.bot.dota.JoinBroadcastChannel();
                controller.instance.bot.StartGame();
            }
            Setup.Details.Status = MatchSetupStatus.Done;
            Setup = Setup;
            Info.Status = MatchStatus.Play;
            Info = Info;
#if !SEND_DATA_TO_EVERYONE
            MatchesController.PublicGames.Add(Info);
#endif
        }

        public void KickUnassigned()
        {
            MatchPlayer[] toRemove = Players.Where(m => m.Team == MatchTeam.Unassigned).ToArray();
            Players.RemoveRange(toRemove);
            foreach (
                Controllers.Matches cont in
                    toRemove.Select(
                        player =>
                            Matches.Find(m => m.User != null && m.User.steam.steamid == player.SID).FirstOrDefault())
                        .Where(cont => cont != null))
                cont.Match = null;
        }

        public void KickSpectators()
        {
            MatchPlayer[] toRemove = Players.Where(m => m.Team == MatchTeam.Spectate).ToArray();
            Players.RemoveRange(toRemove);
            foreach (
                Controllers.Matches cont in
                    toRemove.Select(
                        player =>
                            Matches.Find(m => m.User != null && m.User.steam.steamid == player.SID).FirstOrDefault())
                        .Where(cont => cont != null))
                cont.Match = null;
        }

        /// <summary>
        /// Updates/saves active game info
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
            Teamspeak.Instance.ForceChannel[player.SID] = ChannelNames.FirstOrDefault(m => m.Contains(player.Team == MatchTeam.Radiant ? "Radiant" : "Dire"));
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
        /// <param name="matchId"></param>
        /// <param name="match"></param>
        public void ProcessMatchResult(EMatchOutcome outcome)
        {
            if (!MatchesController.Games.Contains(this))
                return;
            ulong matchId = Setup.Details.MatchId;
            var countMatch = (outcome == EMatchOutcome.k_EMatchOutcome_DireVictory ||
                              outcome == EMatchOutcome.k_EMatchOutcome_RadVictory);
            if (outcome == EMatchOutcome.k_EMatchOutcome_NotScored_Leaver)
            {
                log.Debug(matchId + " HAS NO KNOWN OUTCOME DUE TO LEAVERS");
                countMatch = false;
            }

            log.Debug("PROCESSING RESULT " + matchId + " WITH OUTCOME " + outcome);
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
                MatchCounted = countMatch,
                RatingDire = 0,
                RatingRadiant = 0
            };

            if (countMatch)
                RatingCalculator.CalculateRatingDelta(result);

            result.ApplyRating();
            result.Save();

            if (countMatch)
            {
                foreach (
                    Controllers.Matches c in
                        Players.Select(player => Matches.Find(m => m.User != null && m.User.steam.steamid == player.SID))
                            .SelectMany(cont => cont))
                {
                    c.Result = result;
                }
                var leavers = Setup.Details.Players.Where(m => m.IsLeaver);
                if (leavers.Any())
                    ChatChannel.GlobalSystemMessage(" Punishing leaver(s) " +
                                                    string.Join(", ", leavers.Select(m => m.Name)) +
                                                    " with an abandon and -25 rating.");
            }
            else
            {
                var reason = "some unknown reason";
                switch (outcome)
                {
                    case EMatchOutcome.k_EMatchOutcome_RadVictory:
                    case EMatchOutcome.k_EMatchOutcome_DireVictory:
                    case EMatchOutcome.k_EMatchOutcome_NotScored_Leaver:
                        reason = "leavers";
                        break;
                    case EMatchOutcome.k_EMatchOutcome_NotScored_NeverStarted:
                        reason = "the match never starting";
                        break;
                    case EMatchOutcome.k_EMatchOutcome_NotScored_ServerCrash:
                        reason = "the server crashing";
                        break;
                    case EMatchOutcome.k_EMatchOutcome_NotScored_PoorNetworkConditions:
                        reason = "poor network conditions";
                        break;
                    case EMatchOutcome.k_EMatchOutcome_Unknown:
                        reason = "unknown match result, admin will confirm result and apply rating";
                        break;
                }
                ChatChannel.GlobalSystemMessage("Match not counted due to " + reason + ".");
            }
            Destroy();
        }

        public void AdminDestroy()
        {
            log.Debug("ADMIN DESTROY " + Id);
            Matches.InvokeTo(m => m.Match == this,
                new SystemMsg("Admin Closed Match", "An admin has destroyed the match you were in."), SystemMsg.Msg);
            this.Destroy();
        }

        public static void RecoverActiveMatches()
        {
            foreach (var match in Mongo.ActiveMatches.FindAllAs<ActiveMatch>())
            {
                log.Debug("RECOVERING MATCH " + match.Id + "...");
// ReSharper disable once ObjectCreationAsStatement
                new MatchGame(match).SaveActiveGame();
            }
        }
    }

    /// <summary>
    ///     Match information.
    /// </summary>
    public class MatchGameInfo
    {
        public Guid Id { get; set; }
        public MatchType MatchType { get; set; }
        public bool Public { get; set; }
        public MatchStatus Status { get; set; }
        public string Owner { get; set; }
        public GameMode GameMode { get; set; }
        public string Opponent { get; set; }
        public CaptainsStatus CaptainStatus { get; set; }
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