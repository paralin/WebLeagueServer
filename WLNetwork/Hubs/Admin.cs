using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dota2.GC.Dota.Internal;
using log4net;
using MongoDB.Driver.Builders;
using WLNetwork.Clients;
using WLNetwork.Database;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;
using MatchType = WLNetwork.Matches.Enums.MatchType;

namespace WLNetwork.Hubs
{
    /// <summary>
    /// Admin controller
    /// </summary>
    public class Admin : WebLeagueHub<Admin>
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public override Task OnConnected()
        {
            BrowserClient.HandleConnection(this.Context, this.Clients.Caller, BrowserClient.ClientType.CHAT);
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            BrowserClient.HandleDisconnected(this.Context, BrowserClient.ClientType.CHAT);
            return base.OnDisconnected(stopCalled);
        }

        /// <summary>
        ///     Returns a snapshot of the game list.
        /// </summary>
        /// <returns></returns>
        public MatchGame[] GetGameList()
        {
            return MatchesController.Games.ToArray();
        }

        /// <summary>
        /// Destroys a match with no result.
        /// </summary>
        public void KillMatch(Guid id)
        {
            var client = Client;
            if (client?.User == null || !client.User.authItems.Contains("admin")) return;

            MatchesController.Games.FirstOrDefault(m => m.Id == id)?.AdminDestroy();
        }

        /// <summary>
        /// Manually result a match
        /// </summary>
        /// <param name="id">Match ID</param>
        /// <param name="result">New result</param>
        public void ResultMatch(Guid id, EMatchResult result)
        {
            var client = Client;
            if (client?.User == null || !client.User.authItems.Contains("admin")) return;
            if (result == EMatchResult.DontCount || result == EMatchResult.Unknown) return;
            var match = MatchesController.Games.FirstOrDefault(m => m.Id == id);
            if (match?.Setup == null || match.Setup.Details.State != DOTA_GameState.DOTA_GAMERULES_STATE_GAME_IN_PROGRESS)
                return;
            match.ProcessMatchResult(result, true);
        }

        /// <summary>
        /// Recalculate a match result
        /// </summary>
        /// <param name="args"></param>
        public void RecalculateMatch(long id)
        {
            var client = Client;
            if (client?.User == null || !client.User.authItems.Contains("admin")) return;
            var res = Mongo.Results.FindOneAs<MatchResult>(Query.EQ("_id", id));
            if (res == null)
            {
                log.Warn("Can't find match "+id+", not re-calculating.");
                return;
            }

            log.Info("Recalculating result "+id+" on request of admin.");
            res.RecalculateResult();
        }


        /// <summary>
        /// Changes match result
        /// </summary>
        /// <returns>Any errors</returns>
        public string ChangeResult(ulong id, EMatchResult result)
        {
            var client = Client;
            if (client?.User == null || !client.User.authItems.Contains("admin")) return "You are not an admin.";
			if (id == 0)
				return "Invalid request.";
			var res = Mongo.Results.FindAs<MatchResult> (Query<MatchResult>.EQ (m => m.Id, id)).FirstOrDefault();
			if (res == null)
				return "Unable to find that match result.";
			if (res.Result == result)
				return "The result is already set to that.";
			if (res.Result == EMatchResult.DontCount && res.MatchType == MatchType.OneVsOne)
				return "Cannot count this match.";
			if (res.MatchType == MatchType.OneVsOne) 
			{
				res.Result = EMatchResult.DontCount;
				res.Save ();
				return "Unable to change 1v1 results.";
			}

			log.Debug ("Request to change result of " + res.Id + " from " + res.Result.ToString ("G") + " to " + result.ToString ("G"));
			if(!res.AdjustResult(result)) return "Don't know how to convert " + res.Result.ToString ("G") + " to " + result.ToString ("G")+".";
			res.Save ();

			return null;
        }
    }
}
