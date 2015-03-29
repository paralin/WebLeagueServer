using System.Linq;
using System.Reflection;
using log4net;
using WLCommon.Model;
using WLNetwork.Matches;
using WLNetwork.Matches.Methods;
using XSockets.Core.Common.Socket.Attributes;
using XSockets.Core.XSocket;

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

        public Admin()
        {
            
        }


        /// <summary>
        ///     Returns a snapshot of the game list.
        /// </summary>
        /// <returns></returns>
        public MatchGame[] GetGameList()
        {
            return MatchesController.Games.ToArray();
        }

        public void KillMatch(KillMatchArgs args)
        {
            //Find match
            var match = MatchesController.Games.FirstOrDefault(m => m.Id == args.Id);
            if (match == null) return;
            match.AdminDestroy();
        }
    }
}
