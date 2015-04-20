using System.Linq;
using System.Reflection;
using System.Timers;
using log4net;
using MongoDB.Driver.Builders;
using WLNetwork.Chat;
using WLNetwork.Database;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;
using WLNetwork.Matches.Methods;
using WLNetwork.Model;
using XSockets.Core.Common.Socket.Attributes;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Controllers
{
    /// <summary>
    ///     Games controller
    /// </summary>
    [Authorize(Roles = "matches")]
    public class Matches : WebLeagueController
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Timer challengeTimer;
        private Challenge activeChallenge;
        private MatchGame activeMatch;
        private MatchResult activeResult;
        private bool userped;

        public Matches()
        {
            OnAuthorizationFailed +=
                (sender, args) =>
                    log.Warn("Failed authorize for " + args.MethodName + " [" + ConnectionContext.PersistentId + "]" +
                             (ConnectionContext.IsAuthenticated ? " [" + User.steam.steamid + "]" : ""));
            OnOpen += (sender, args) =>
            {
                if (User == null) return;
                Matches other =
                    this.Find(m => m != this && m.User != null && m.User.steam.steamid == User.steam.steamid)
                        .FirstOrDefault();
                if (other != null)
                {
                    other.userped = true;
                    if (other.Challenge != null)
                    {
                        Challenge = other.Challenge;
                        if (other.challengeTimer.Enabled)
                            challengeTimer.Start();
                        other.Match = null;
                    }
                    if (other.Match != null)
                    {
                        Match = other.Match;
                        other.Match = null;
                    }
                    if (other.Result != null)
                    {
                        Result = other.Result;
                        other.Result = null;
                    }
                    other.UserpConnection();
                }
                else
                {
                    //See if we're in any matches already
                    MatchGame game =
                        MatchesController.Games.FirstOrDefault(m => m.Players.Any(x => x.SID == User.steam.steamid));
                    if (game != null)
                    {
                        Match = game;
                    }
                }
            };
            OnClose += (sender, args) =>
            {
                if (User == null) return;
                if (!userped)
                    LeaveMatch();
                challengeTimer.Stop();
                challengeTimer.Close();
                if (Challenge != null && !userped)
                {
                    Matches tcont =
                        this.Find(m => m.User != null && m.User.steam.steamid == Challenge.ChallengedSID)
                            .FirstOrDefault();
                    if (tcont != null)
                    {
                        tcont.Challenge = null;
                        tcont.challengeTimer.Stop();
                    }
                    Challenge = null;
                }
            };
            challengeTimer = new Timer(20000);
            challengeTimer.Elapsed += (sender, args) =>
            {
                if (Challenge != null)
                {
                    Matches tcont =
                        this.Find(m => m.User != null && m.User.steam.steamid == Challenge.ChallengerSID)
                            .FirstOrDefault();
                    if (tcont != null)
                    {
                        tcont.Challenge = null;
                        tcont.challengeTimer.Stop();
                    }
                    Challenge = null;
                }
                challengeTimer.Stop();
            };
        }

        /// <summary>
        ///     The active match the player is in.
        /// </summary>
        public MatchGame Match
        {
            get { return activeMatch; }
            internal set
            {
                this.Invoke(value, "matchsnapshot");
                activeMatch = value;
            }
        }

        /// <summary>
        ///     The active match the player is in.
        /// </summary>
        public MatchResult Result
        {
            get { return activeResult; }
            internal set
            {
                this.Invoke(value, "resultsnapshot");
                activeResult = value;
            }
        }

        /// <summary>
        ///     The active challenge the player is in.
        /// </summary>
        public Challenge Challenge
        {
            get { return activeChallenge; }
            internal set
            {
                if (value != null)
                    this.Invoke(value, "challengesnapshot");
                else
                {
                    this.Invoke("clearchallenge");
                }
                activeChallenge = value;
            }
        }

        /// <summary>
        ///     Returns a snapshot of the public game list.
        /// </summary>
        /// <returns></returns>
        public MatchGameInfo[] GetPublicGameList()
        {
            return MatchesController.PublicGames.ToArray();
        }

        /// <summary>
        ///     Returns a snapshot of games that the player could join.
        /// </summary>
        /// <returns></returns>
        public MatchGame[] GetAvailableGameList()
        {
            //todo: limit to non-in-progress matches
            return MatchesController.Games.ToArray();
        }

        /// <summary>
        ///     Create a new match.
        /// </summary>
        /// <param name="options">Match options</param>
        /// <returns>Error else null</returns>
        [Authorize("startGames")]
        public string CreateMatch(MatchCreateOptions options)
        {
            LeaveMatch();
            if (options == null) return "You didn't give any options for the match.";
            if (Match != null) return "You are already in a match you cannot leave.";
            if (User != null && User.authItems.Contains("spectateOnly")) return "You are limited to spectating only.";
            if (User != null && User.authItems.Contains("challengeOnly")) return "You are limited to joining challenge pools only. You cannot create challenges/startgames.";
            options.MatchType = MatchType.StartGame;
            var match = new MatchGame(User.steam.steamid, options);
            Match = match;
            match.Players.Add(new MatchPlayer(User));
            ChatChannel.GlobalSystemMessage(User.profile.name+" created a new match.");
            return null;
        }

        /// <summary>
        ///     Admin command to fill the current match with players from a chat
        /// </summary>
        /// <param name="options">Match options</param>
        /// <returns>Error else null</returns>
        [Authorize("admin")]
        public string FillChatPlayers(FillChatPlayersOptions options)
        {
            if (options == null) return "You didn't give any options for the fill.";
            if (string.IsNullOrWhiteSpace(options.ChatName)) return "You didn't specify a chat name.";
            options.ChatName = options.ChatName.ToLower().Replace('\n', ' ');
            if (Match == null) return "You must be in a match.";
            int playersNeeded = 10-Match.Players.Count;
            int origPlayersNeeded = playersNeeded;
            if (playersNeeded <= 0) return "Your match is already full.";
            var channel = ChatChannel.Channels.Values.FirstOrDefault(m => m.Name.ToLower() == options.ChatName);
            if(channel == null) return "Can't find chat \""+options.ChatName+"\"...";
            foreach(var player in channel.Members.Values.Where(m=>Match.Players.All(x => x.SID != m.SteamID)))
            {
                ChatMember player1 = player;
                var cont = this.Find(m => m.User != null && m.User.steam.steamid == player1.SteamID).FirstOrDefault();
                if (cont == null) continue;
                cont.JoinMatch(new MatchJoinOptions() {Id = Match.Id});
                playersNeeded--;
                if (playersNeeded <= 0) break;
            }
            ChatChannel.GlobalSystemMessage(User.profile.name+" pulled "+origPlayersNeeded+" players into their match.");
            return null;
        }

        /// <summary>
        ///     Starts the queue to find a bot for the match
        /// </summary>
        /// <returns></returns>
        public string StartMatch()
        {
            if (Match == null) return "You are not currently in a match.";
            if (User == null) return "You are not logged in for some reason.";
            if (User.authItems.Contains("spectateOnly")) return "You cannot start matches, you can spectate only.";
            if (Match.Info.Owner != User.steam.steamid) return "You are not the host of this game.";
            if (Match.Setup != null || Match.Info.Status == MatchStatus.Teams)
                return "The match is already being set up.";
            if (Match.Info.MatchType == MatchType.Captains)
            {
				if (Match.Players.Count(m=>m.Team != MatchTeam.Spectate) < 10) return "You need at least 10 players to start the challenge.";
                Match.StartPicks();
            }
            else if (Match.Info.MatchType == MatchType.StartGame)
            {
#if !DEBUG
                if (Match.Players.Count < 10 && !User.authItems.Contains("admin"))
                    return "Non admins must have 10 players for start games.";
#endif
                Match.StartSetup();
            }
            return null;
        }

        /// <summary>
        ///     Pick a player in captains
        /// </summary>
        /// <param name="player"></param>
        public void PickPlayer(PickPlayer player)
        {
            if (Match == null || Match.Info.MatchType != MatchType.Captains) return;
            MatchPlayer me = Match.Players.FirstOrDefault(m => m.IsCaptain && m.SID == User.steam.steamid);
            if (me == null) return;
            Match.PickPlayer(player.SID, me.Team);
        }

        /// <summary>
        ///     Dismiss a result.
        /// </summary>
        public void DismissResult()
        {
            Result = null;
        }

        /// <summary>
        ///     Starts the game in-game
        /// </summary>
        /// <returns></returns>
        public string FinalizeMatch()
        {
            if (Match == null) return "You are not currently in a match.";
            if (User == null) return "You are not logged in for some reason.";
            if (Match.Info.Owner != User.steam.steamid) return "You are not the host of this game.";
            if (Match.Setup == null || Match.Setup.Details.Status != MatchSetupStatus.Wait ||
                Match.Players.Any(m => !m.Ready && m.Team < MatchTeam.Spectate)) return "The match cannot be started yet.";
            Match.StartMatch();
            return null;
        }

        /// <summary>
        ///     Respond to a challenge
        /// </summary>
        /// <param name="resp"></param>
        public void ChallengeResponse(ChallengeResponse resp)
        {
            if (Challenge == null || Challenge.ChallengedSID != User.steam.steamid) return;
            challengeTimer.Stop();
            Matches other =
                this.Find(m => m.User != null && m.User.steam.steamid == Challenge.ChallengerSID).FirstOrDefault();
            Challenge chal = Challenge;
            Challenge = null;
            if (other == null) return;
            other.Challenge = null;
            ChatChannel.GlobalSystemMessage(User.profile.name+(resp.accept ? " accepted the challenge." : " declined the challenge."));
            if (!resp.accept) return;
            //Create the match
            var match = new MatchGame(User.steam.steamid, new MatchCreateOptions
            {
                GameMode = chal.GameMode,
                MatchType = MatchType.Captains,
                OpponentSID = other.User.steam.steamid
            });
            Match = match;
            other.Match = match;
            match.Players.AddRange(new[]
            {
                new MatchPlayer(other.User) {IsCaptain = true, Team = MatchTeam.Dire},
                new MatchPlayer(User) {IsCaptain = true, Team = MatchTeam.Radiant}
            });
            MatchGame.TSSetupQueue.Enqueue(match);
        }

        /// <summary>
        ///     Join an existing match.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public string JoinMatch(MatchJoinOptions options)
        {
            if (activeMatch != null && options.Id == activeMatch.Id) return "You are already in that match.";
            if (User.authItems.Contains("spectateOnly") && !options.Spec) return "You cannot join matches, you can spectate only.";
            //LeaveMatch();
            if (activeMatch != null) return "You are already in a match, leave that one first.";
            MatchGame match = MatchesController.Games.FirstOrDefault( m => m.Id == options.Id && m.Info.Public);
            if (match == null) return "That match can't be found.";
            if (User != null && User.authItems.Contains("challengeOnly") && match.Info.MatchType != MatchType.Captains) return "You are limited to joining challenge pools only.";
            if (match.Info.Status != MatchStatus.Players && !options.Spec)
                return "Can't join a match that has started.";
            if (match.Info.Status > MatchStatus.Lobby && options.Spec)
                return "Can't spectate a match already past the lobby stage.";
            if (!options.Spec && match.Players.Count(m => m.Team != MatchTeam.Spectate) >= 10 && match.Info.MatchType != MatchType.Captains) 
                return "That match is full.";
            Match = match;
            match.Players.Add(new MatchPlayer(User) {Team = options.Spec ? MatchTeam.Spectate : MatchTeam.Unassigned});
            return null;
        }

        /// <summary>
        ///     Leave an existing match.
        ///     <returns>Error else null</returns>
        /// </summary>
        public string LeaveMatch()
        {
            if (Match == null) return "You are not currently in a match.";
            if (User == null) return "You are not signed in and thus cannot be in a match.";
            MatchPlayer me = Match.Players.FirstOrDefault(m => m.SID == User.steam.steamid);
            bool isOwner = Match.Info.Owner == User.steam.steamid || me.IsCaptain;
            if (me.Team < MatchTeam.Spectate && ((Match.Info.Status > MatchStatus.Lobby && isOwner) || (Match.Info.Status > MatchStatus.Players && !isOwner))) return "You cannot leave matches in progress.";
            if (isOwner)
            {
                Match.Destroy();
            }
            else
            {
                MatchPlayer plyr = Match.Players.FirstOrDefault(m => m.SID == User.steam.steamid);
                if (plyr != null) Match.Players.Remove(plyr);
                Match = null;
            }
            return null;
        }

        /// <summary>
        ///     Create a challenge
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        [Authorize("startGames")]
        public string StartChallenge(Challenge target)
        {
            if (Match != null) return "You are already in a match.";
            if (Challenge != null) return "Waiting for a challenge response already...";
            if (User != null && User.authItems.Contains("spectateOnly")) return "You are spectator and cannot play matches.";
            if (User != null && User.authItems.Contains("challengeOnly")) return "You are limited to joining challenge pools only.";
            target.ChallengerSID = User.steam.steamid;
            target.ChallengerName = User.steam.personaname;
            if (target.ChallengedSID == null) return "You didn't specify a person to challenge.";
            if (target.ChallengedSID == User.steam.steamid) return "You cannot challenge yourself!";
            Matches tcont = this.Find(m => m.User != null && m.User.steam.steamid == target.ChallengedSID).FirstOrDefault();
            if (tcont == null) return "That player is no longer online.";
            if (tcont.Match != null) return "That player is already in a match.";
            if (tcont.Challenge != null) return "That player is already waiting for a challenge.";
            if (tcont.User.authItems.Contains("spectateOnly")) return "That player is a spectator and cannot play matches.";
            target.ChallengedName = tcont.User.steam.personaname;
            target.ChallengedSID = tcont.User.steam.steamid;
            tcont.Challenge = target;
            tcont.challengeTimer.Start();
            Challenge = target;
            ChatChannel.GlobalSystemMessage(string.Format("{0} challenged {1} to a Captain's match!", User.profile.name, tcont.User.profile.name));
            return null;
        }

        public void UserpConnection()
        {
            log.Debug("USERPED");
            userped = true;
            this.Invoke("userped");
            Close();
        }

        public void ReloadUser()
        {
            var user = Mongo.Users.FindOneAs<User>(Query.EQ("steam.steamid", User.steam.steamid));
            if (user != null) User = user;
        }
    }
}