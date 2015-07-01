using System.Linq;
using System.Reflection;
using Dota2.GC.Dota.Internal;
using log4net;
using WLNetwork.Matches;
using WLNetwork.Matches.Methods;
using XSockets.Core.Common.Socket.Attributes;
using WLNetwork.Database;
using MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoDB.Driver.Builders;

namespace WLNetwork.Controllers
{
    /// <summary>
    /// Admin controller
    /// </summary>
    [Authorize(Roles = "admin")]
    public class Admin : WebLeagueController
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);


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
        public void KillMatch(KillMatchArgs args)
        {
            //Find match
            var match = MatchesController.Games.FirstOrDefault(m => m.Id == args.Id);
            if (match == null) return;
            match.AdminDestroy();
        }

        /// <summary>
        /// Manually result a match
        /// </summary>
        /// <param name="args"></param>
        public void ResultMatch(ResultMatchArgs args)
        {
            if (args.Result == EMatchResult.DontCount || args.Result == EMatchResult.Unknown) return;
            var match = MatchesController.Games.FirstOrDefault(m => m.Id == args.Id);
            if (match == null) return;
            if (match.Setup == null || match.Setup.Details.State != DOTA_GameState.DOTA_GAMERULES_STATE_GAME_IN_PROGRESS)
                return;
            match.ProcessMatchResult(args.Result, true);
        }


        /// <summary>
        /// Changes match result
        /// </summary>
        /// <returns>Any errors</returns>
        public string ChangeResult(ChangeResultArgs args)
        {
			if (args.Id == 0)
				return "Invalid request.";
			var res = Mongo.Results.FindAs<MatchResult> (Query<MatchResult>.EQ (m => m.Id, args.Id)).FirstOrDefault();
			if (res == null)
				return "Unable to find that match result.";
			if (res.Result == args.Result)
				return "The result is already set to that.";
			if (res.Result == EMatchResult.DontCount && res.MatchType == WLNetwork.Matches.Enums.MatchType.OneVsOne)
				return "Cannot count this match.";
			if (res.MatchType == WLNetwork.Matches.Enums.MatchType.OneVsOne) 
			{
				res.Result = EMatchResult.DontCount;
				res.Save ();
				return "Unable to change 1v1 results.";
			}

			log.Debug ("Request to change result of " + res.Id + " from " + res.Result.ToString ("G") + " to " + args.Result.ToString ("G"));
			if(!res.AdjustResult (args.Result)) return "Don't know how to convert " + res.Result.ToString ("G") + " to " + args.Result.ToString ("G")+".";
			res.Save ();

			return null;
        }
    }
}
