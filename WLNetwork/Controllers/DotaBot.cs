using System.Linq;
using WLCommon.Model;
using WLNetwork.Bots;
using XSockets.Core.Common.Socket.Attributes;
using XSockets.Core.XSocket;

namespace WLNetwork.Controllers
{
    /// <summary>
    /// The controller for Dota BotHost
    /// </summary>
    [Authorize(Roles = "dotaBot")]
    public class DotaBot : XSocketController
    {
        /// <summary>
        /// Is this bot authed?
        /// </summary>
        public bool Authed { get { return this.ConnectionContext.IsAuthenticated&&this.ConnectionContext.User.IsInRole("dotaBot"); } }

        /// <summary>
        /// Get a snapshot of the bot database.
        /// </summary>
        /// <returns></returns>
        public Bot[] GetBotSnapshot()
        {
            return BotDB.Bots.Values.ToArray();
        }

        public void OnSAuthFailed()
    }
}
