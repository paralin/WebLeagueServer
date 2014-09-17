using System.ComponentModel.Design;
using System.Linq;
using MongoDB.Driver.Linq;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;
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
            this.OnClose += (sender, args) => LeaveMatch();
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
            return MatchesController.Games.Where(m=>m.Info.MatchType == MatchType.StartGame && m.Info.Status == MatchStatus.Players).ToArray();
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
            match.Players.Add(new MatchPlayer(User));
            this.Match = match;
            return null;
        }

        /// <summary>
        /// Join an existing match.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public string JoinMatch(MatchJoinOptions options)
        {
            if (activeMatch != null && options.Id == activeMatch.Id) return "You are already in that match.";
            LeaveMatch();
            if (activeMatch != null) return "You cannot leave your current match.";
            var match =
                MatchesController.Games.FirstOrDefault(
                    m => m.Id == options.Id && m.Info.Public && m.Info.Status == MatchStatus.Players);
            match.Players.Add(new MatchPlayer(User));
            Match = match;
            return null;
        }

        /// <summary>
        /// Switch teams.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public string SwitchTeam()
        {
            if (activeMatch == null) return "You are not currently in a match.";
            var plyr = activeMatch.Players.ForUser(User);
            if (plyr == null) return "Can't find you in the match. Try rejoining.";
            var tteam = plyr.Team == MatchTeam.Dire ? MatchTeam.Radiant : MatchTeam.Dire;
            if (activeMatch.Players.TeamCount(tteam) == 5) return "The other team is currently full.";
            plyr.Team = tteam;
            activeMatch.PlayerUpdate(plyr);
            return null;
        }

        /// <summary>
        /// Leave an existing match.
        /// <returns>Error else null</returns>
        /// </summary>
        public string LeaveMatch()
        {
            if (Match == null) return "You are not currently in a match.";
            if (Match.Info.Status > MatchStatus.Players) return "You cannot leave matches in progress.";
            if (User == null) return "You are not signed in and thus cannot be in a match.";
            if (Match.Info.Owner == User.steam.steamid)
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
