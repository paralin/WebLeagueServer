using System.Linq;
using System.Runtime.Remoting.Messaging;
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

        private bool _ready;
        public bool Ready { get { return Authed && _ready; } }

        /// <summary>
        /// Add this bot to the active bot host pool.
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public bool ReadyUp()
        {
            if (Authed) _ready = true;
            return Ready;
        }
    }
}
