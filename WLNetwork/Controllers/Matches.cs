using System.CodeDom;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Threading.Tasks;
using System.Timers;
using MongoDB.Driver.Linq;
using WLCommon.Matches;
using WLCommon.Matches.Enums;
using WLCommon.Model;
using WLNetwork.Matches;
using WLNetwork.Matches.Methods;
using WLNetwork.Model;
using XSockets.Core.Common.Socket.Attributes;
using XSockets.Core.XSocket;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Controllers
{
    /// <summary>
    /// Games controller
    /// </summary>
    [Authorize(Roles = "matches")]
    public class Matches : XSocketController
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Matches()
        {
            this.OnAuthorizationFailed += (sender, args) => log.Warn("Failed authorize for " + args.MethodName + " [" + this.ConnectionContext.PersistentId + "]" + (this.ConnectionContext.IsAuthenticated ? " [" + this.User.steam.steamid + "]" : ""));
            this.OnClose += (sender, args) =>
            {
                LeaveMatch();
                challengeTimer.Stop();
                challengeTimer.Close();
                if (Challenge != null)
                {
                    var tcont =
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
                    var tcont =
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

        private MatchGame activeMatch = null;

        /// <summary>
        /// The active match the player is in.
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

        private Challenge activeChallenge = null;

        /// <summary>
        /// The active challenge the player is in.
        /// </summary>
        public Challenge Challenge
        {
            get { return activeChallenge; }
            internal set
            {
                if(value != null)
                    this.Invoke(value, "challengesnapshot");
                else
                {
                    this.Invoke("clearchallenge");
                }
                activeChallenge = value;
            }
        }

        private Timer challengeTimer;

        public User User
        {
            get
            {
                if (!this.ConnectionContext.IsAuthenticated) return null;
                return ((UserIdentity)this.ConnectionContext.User.Identity).User;
            }
        }

        /// <summary>
        /// Returns a snapshot of the public game list.
        /// </summary>
        /// <returns></returns>
        public MatchGameInfo[] GetPublicGameList()
        {
            return MatchesController.PublicGames.ToArray();
        }

        /// <summary>
        /// Returns a snapshot of games that the player could join.
        /// </summary>
        /// <returns></returns>
        public MatchGame[] GetAvailableGameList()
        {
            //todo: List only StartGames for now 
            return MatchesController.Games.ToArray();
        }

        /// <summary>
        /// Create a new match.
        /// </summary>
        /// <param name="options">Match options</param>
        /// <returns>Error else null</returns>
        [Authorize("startGames")]
        public string CreateMatch(MatchCreateOptions options)
        {
            LeaveMatch();
            if (options == null) return "You didn't give any options for the match.";
            if (string.IsNullOrWhiteSpace(options.Name)) return "You didn't specify a name.";
            options.Name = options.Name.Replace('\n', ' ');
            if (Match != null) return "You are already in a match you cannot leave.";
            var match = new MatchGame(this.User.steam.steamid, options);
            this.Match = match;
            match.Players.Add(new MatchPlayer(User));
            return null;
        }

        /// <summary>
        /// Starts the queue to find a bot for the match
        /// </summary>
        /// <returns></returns>
        public string StartMatch()
        {
            if (Match == null) return "You are not currently in a match.";
            if (User == null) return "You are not logged in for some reason.";
            if (Match.Info.Owner != this.User.steam.steamid) return "You are not the host of this game.";
            if (Match.Setup != null || Match.Info.Status == MatchStatus.Teams) return "The match is already being set up.";
            if (Match.Info.MatchType == MatchType.Captains)
            {
                if (Match.Players.Count < 10) return "You need at least 10 players to start the challenge."; 
                Match.StartPicks();
            }
            else Match.StartSetup();
            return null;
        }

        /// <summary>
        /// Pick a player in captains
        /// </summary>
        /// <param name="player"></param>
        public void PickPlayer(PickPlayer player)
        {
            if (Match == null || Match.Info.MatchType != MatchType.Captains) return;
            var me = Match.Players.FirstOrDefault(m => m.IsCaptain && m.SID == User.steam.steamid);
            if (me == null) return;
            Match.PickPlayer(player.SID, me.Team);
        }

        /// <summary>
        /// Starts the game in-game
        /// </summary>
        /// <returns></returns>
        public string FinalizeMatch()
        {
            if (Match == null) return "You are not currently in a match.";
            if (User == null) return "You are not logged in for some reason.";
            if (Match.Info.Owner != this.User.steam.steamid) return "You are not the host of this game.";
            if (Match.Setup == null || Match.Setup.Details.Status != MatchSetupStatus.Wait || Match.Players.Any(m =>!m.Ready)) return "The match cannot be started yet.";
            Match.Finalize();
            return null;
        }

        /// <summary>
        /// Respond to a challenge
        /// </summary>
        /// <param name="resp"></param>
        public void ChallengeResponse(ChallengeResponse resp)
        {
            if (Challenge == null || Challenge.ChallengedSID != User.steam.steamid) return;
            challengeTimer.Stop();
            var other =
                this.Find(m => m.User != null && m.User.steam.steamid == Challenge.ChallengerSID).FirstOrDefault();
            var chal = Challenge;
            Challenge = null;
            if (other == null) return;
            other.Challenge = null;
            if (!resp.accept) return;
            //Create the match
            var match = new MatchGame(this.User.steam.steamid, new MatchCreateOptions()
            {
                GameMode = chal.GameMode,
                MatchType = MatchType.Captains,
                Name =
                    "Challenge, " + this.User.steam.personaname + " vs. " + other.User.steam.personaname + ".",
                OpponentSID = other.User.steam.steamid
            });
            this.Match = match;
            other.Match = match;
            match.Players.AddRange(new [] { new MatchPlayer(other.User) { IsCaptain = true, Team = MatchTeam.Dire }, new MatchPlayer(User) { IsCaptain = true, Team = MatchTeam.Radiant } });
        }

        /// <summary>
        /// Join an existing match.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public string JoinMatch(MatchJoinOptions options)
        {
            if (activeMatch != null && options.Id == activeMatch.Id) return "You are already in that match.";
            //LeaveMatch();
            if (activeMatch != null) return "You are already in a match, leave that one first.";
            var match =
                MatchesController.Games.FirstOrDefault(
                    m => m.Id == options.Id && m.Info.Public && m.Info.Status == MatchStatus.Players);
            if (match == null) return "That match can't be found.";
            if (match.Players.Count >= 10 && match.Info.MatchType != MatchType.Captains) return "That match is full.";
            Match = match;
            match.Players.Add(new MatchPlayer(User){Team = MatchTeam.Unassigned});
            return null;
        }

        /// <summary>
        /// Leave an existing match.
        /// <returns>Error else null</returns>
        /// </summary>
        public string LeaveMatch()
        {
            if (Match == null) return "You are not currently in a match.";
            if (User == null) return "You are not signed in and thus cannot be in a match.";
            var me = Match.Players.FirstOrDefault(m => m.SID == User.steam.steamid);
            var isOwner = Match.Info.Owner == User.steam.steamid || me.IsCaptain;
            if ((Match.Info.Status > MatchStatus.Lobby && isOwner) || (Match.Info.Status > MatchStatus.Players && !isOwner)) return "You cannot leave matches in progress.";
            if (isOwner)
            {
                Match.Destroy();
            }
            else
            {
                var plyr = Match.Players.FirstOrDefault(m => m.SID == User.steam.steamid);
                if (plyr != null) Match.Players.Remove(plyr);
                Match = null;
            }
            return null;
        }
        
        /// <summary>
        /// Create a challenge
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        [Authorize("startGames")]
        public string StartChallenge(Challenge target)
        {
            if (Match != null) return "You are already in a match.";
            if (Challenge != null) return "Waiting for a challenge response already...";
            target.ChallengerSID = User.steam.steamid;
            target.ChallengerName = User.steam.personaname;
            if (target.ChallengedSID == null) return "You didn't specify a person to challenge.";
            var tcont = this.Find(m => m.User != null && m.User.steam.steamid == target.ChallengedSID).FirstOrDefault();
            if (tcont == null) return "That player is no longer online.";
            if (tcont.Match != null) return "That player is already in a match.";
            if (tcont.Challenge != null) return "That player is already waiting for a challenge.";
            target.ChallengedName = tcont.User.steam.personaname;
            target.ChallengedSID = tcont.User.steam.steamid;
            tcont.Challenge = target;
            tcont.challengeTimer.Start();
            Challenge = target;
            return null;
        }

        public override bool OnAuthorization(IAuthorizeAttribute authorizeAttribute)
        {
            if (User == null) return false;
            if (!string.IsNullOrWhiteSpace(authorizeAttribute.Roles))
            {
                var roles = authorizeAttribute.Roles.Split(',');
                return User.authItems.ContainsAll(roles);
            }
            else
            {
                return User.steam.steamid == authorizeAttribute.Users;
            }
        }
    }
}
