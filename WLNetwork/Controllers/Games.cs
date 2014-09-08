using System.Collections.ObjectModel;
using System.Linq;
using WLNetwork.Matches;
using XSockets.Core.Common.Socket.Attributes;
using XSockets.Core.XSocket;
using XSockets.Plugin.Framework.Attributes;

namespace WLNetwork.Controllers
{
    /// <summary>
    /// Games controller
    /// </summary>
    [Authorize(Roles = "matches")]
    public class Games : XSocketController
    {
        public MatchGameInfo[] GetPublicGameList()
        {
            return MatchesController.PublicGames.ToArray();
        }
    }
}
