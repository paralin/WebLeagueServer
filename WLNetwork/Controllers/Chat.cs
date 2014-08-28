using System;
using System.Collections.ObjectModel;
using System.Security.Principal;
using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;
using WLNetwork.Chat;
using WLNetwork.Database;
using WLNetwork.Model;
using WLNetwork.Properties;
using XSockets.Core.Common.Socket.Attributes;
using XSockets.Core.Common.Socket.Event.Arguments;
using XSockets.Core.XSocket;
using XSockets.Plugin.Framework.Attributes;

namespace WLNetwork.Controllers
{
    /// <summary>
    /// Chat controller.
    /// </summary>
    [Authorize(Roles = "chat")]
    public class Chat : XSocketController
    {
        private static readonly log4net.ILog log =
   log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private ObservableCollection<ChatChannel> ChatChannel = new ObservableCollection<ChatChannel>();

        public Chat()
        {
            
        }

        public User User
        {
            get
            {
                if (!this.ConnectionContext.IsAuthenticated) return null;
                return ((UserIdentity) this.ConnectionContext.User.Identity).User;
            }
        }

        public void SendMessage(Message message)
        {
            log.Debug("Chat message received: "+message.Text);
        }
    }
}
