using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using WLNetwork.Chat;
using WLNetwork.Chat.Exceptions;
using WLNetwork.Chat.Methods;
using WLNetwork.Model;
using XSockets.Core.Common.Socket.Attributes;
using XSockets.Core.Common.Socket.Event.Arguments;
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
            this.OnOpen += (sender, args) => log.Debug("CONNECTED [" + this.ConnectionContext.PersistentId + "]");
            this.OnClose += OnOnClose;
        }

        private void OnOnClose(object sender, OnClientDisconnectArgs onClientDisconnectArgs)
        {
            log.Debug("DISCONNECTED [" + this.ConnectionContext.PersistentId + "]");
            if (!ConnectionContext.IsAuthenticated) return;
            foreach (var channel in Channels)
            {
                var member = channel.Members.Values.FirstOrDefault(m => m.SteamID == User.steam.steamid);
                if (member != null) channel.Members.Remove(member.ID);
            }
            this.Channels.Clear();
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
            log.DebugFormat("[{0}] {1}: \"{2}\"", chan.Name, User.profile.name, message.Text);
            chan.TransmitMessage(chan.Members.Values.FirstOrDefault(m => m.SteamID == User.steam.steamid), message.Text);
        }

        public string JoinOrCreate(JoinCreateRequest req)
        {
            if (req == null) return "You didn't provide any channels to join.";
            if (Channels.Any(m => m.Name.ToLower() == req.Name.ToLower())) return "You are already in that channel.";
            try
            {
                var chan = ChatChannel.JoinOrCreate(req.Name.ToLower(), new ChatMember(this.ConnectionId, User));
                if (chan != null)
                    this.Channels.Add(chan);
                return null;
            }
            catch (JoinCreateException ex)
            {
                return ex.Message;
            }
        }

        public void Leave(LeaveRequest req)
        {
            if (req == null || req.Id == null) return;
            var chan = this.Channels.FirstOrDefault(m => m.Id.ToString() == req.Id);
            if (chan == null || !chan.Leavable) return;
            chan.Members.Remove(this.ConnectionId);
            this.Channels.Remove(chan);
        }
    }
}
