using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Principal;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Linq;
using Newtonsoft.Json.Linq;
using WLNetwork.Chat;
using WLNetwork.Chat.Exceptions;
using WLNetwork.Chat.Methods;
using WLNetwork.Database;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;
using WLNetwork.Model;
using WLNetwork.Properties;
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
            this.OnClose += OnOnClose;
            this.OnOpen += (sender, args) => log.Debug("CONNECTED [" + this.ConnectionContext.PersistentId + "]");
            this.OnAuthorizationFailed +=
                (sender, args) =>
                    log.Warn("Failed authorize for " + args.MethodName + " [" + this.ConnectionContext.PersistentId +
                             "]" + (this.ConnectionContext.IsAuthenticated ? " [" + this.User.steam.steamid + "]" : ""));
        }

        [AllowAnonymous]
        public string[] AuthInfo()
        {
            var user = User;
            if(user == null) return new string[0];
            return user.authItems;
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

        private void OnOnClose(object sender, OnClientDisconnectArgs onClientDisconnectArgs)
        {
            log.Debug("DISCONNECTED [" + this.ConnectionContext.PersistentId + "]"+(this.ConnectionContext.IsAuthenticated?" ["+this.User.steam.steamid+"]":""));
            if (!ConnectionContext.IsAuthenticated) return;
            foreach (var channel in Channels)
            {
                channel.Members.Remove(this.ConnectionId);
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
                var chan = ChatChannel.JoinOrCreate(req.Name.ToLower(), new ChatMember(this.ConnectionId, User, User.steam.avatarfull));
                if (chan != null)
                    this.Channels.Add(chan);
                return null;
            }
            catch (JoinCreateException ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Join or create a game channel.
        /// </summary>
        /// <param name="chan"></param>
        internal void JoinOrCreateGameChannel(MatchGame ichan)
        {
            try
            {
                var memb = ichan.Players.FirstOrDefault(m => m.SID == ichan.Info.Owner);
                if (memb == null) return;
                var chan = ChatChannel.JoinOrCreate(Enum.GetName(typeof(MatchType), ichan.Info.MatchType)+" "+ichan.Id.ToString().Split('-')[0], new ChatMember(this.ConnectionId, User, User.steam.avatarfull), ChannelType.Lobby, false, true);
                if (chan != null)
                {
                    chan.MatchId = ichan.Id;
                    this.Channels.Add(chan);
                }
                return;
            }
            catch (JoinCreateException ex)
            {
                log.Error("Problem joining or creating game chat channel " + ichan.Id);
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

        internal void Leave(Guid id)
        {
            var chan = this.Channels.FirstOrDefault(m => m.Id == id);
            if (chan == null) return;
            chan.Members.Remove(this.ConnectionId);
            this.Channels.Remove(chan);
        }

        internal void Leave(ChatChannel firstOrDefault)
        {
            if (firstOrDefault == null) return;
            this.Leave(firstOrDefault.Id);
        }
    }
}
