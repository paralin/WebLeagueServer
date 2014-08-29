using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using WLNetwork.Chat;
using WLNetwork.Chat.Methods;
using WLNetwork.Model;
using XSockets.Core.Common.Socket.Attributes;
using XSockets.Core.XSocket;
using XSockets.Core.XSocket.Helpers;

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

        public ObservableCollection<ChatChannel> Channels = new ObservableCollection<ChatChannel>();

        public Chat()
        {
            this.Channels.CollectionChanged += ChatChannelOnCollectionChanged;
        }

        private void ChatChannelOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if(e.NewItems != null)
                this.Invoke(new ChatChannelUpd(e.NewItems.OfType<ChatChannel>().ToArray()), ChatChannelUpd.Msg);
            if (e.OldItems != null)
                this.Invoke(new ChatChannelRm(e.OldItems.OfType<ChatChannel>().ToArray()), ChatChannelRm.Msg);
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
            if (message == null || !this.ConnectionContext.IsAuthenticated || !message.Validate()) return;
            var chan = Channels.FirstOrDefault(m => m.Id.ToString() == message.Channel);
            if (chan == null) return;
            log.DebugFormat("[{0}] {1}: \"{2}\"", message);
            chan.TransmitMessage(chan.Members.FirstOrDefault(m => m.SteamID == User.steam.steamid), message.Text);
        }

        public void JoinOrCreate(JoinCreateRequest req)
        {
            if (req == null) return;
            if (Channels.Any(m => m.Name.ToLower() == req.Name.ToLower())) return;
            ChatChannel.JoinOrCreate(req.Name.ToLower(), new ChatMember(User));
        }
    }
}
