using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Linq;
using WLNetwork.Chat;
using WLNetwork.Chat.Exceptions;
using WLNetwork.Chat.Methods;
using WLNetwork.Model;
using XSockets.Core.Common.Socket.Attributes;
using XSockets.Core.Common.Socket.Event.Arguments;
using XSockets.Core.XSocket;
using XSockets.Core.XSocket.Helpers;
using Timer = System.Timers.Timer;

namespace WLNetwork.Controllers
{
    /// <summary>
    ///     Chat controller.
    /// </summary>
    [Authorize(Roles = "chat")]
    public class Chat : WebLeagueController
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public ObservableCollection<ChatChannel> Channels = new ObservableCollection<ChatChannel>();
        private ChatMember member;
        private Timer pingTimer = null;
        
        public Chat()
        {
            Channels.CollectionChanged += ChatChannelOnCollectionChanged;
            OnClose += OnOnClose;
            OnOpen += (sender, args) =>
            {
                log.Debug("CONNECTED [" + ConnectionContext.PersistentId + "]");
                Task.Run(() =>
                {
                    Thread.Sleep(100);
                    StartPingTimer();
                    JoinOrCreate(new JoinCreateRequest() { Name = "main" });
                    LoadChatChannels();
                });
            };
            OnAuthorizationFailed +=
                (sender, args) =>
                    log.Warn("Failed authorize for " + args.MethodName + " [" + ConnectionContext.PersistentId +
                             "]" + (ConnectionContext.IsAuthenticated ? " [" + User.steam.steamid + "]" : ""));
        }

        private void StartPingTimer()
        {
            if (pingTimer != null) return;
            pingTimer = new Timer(5000);
            pingTimer.Elapsed += (sender, args) => this.Invoke("ping");
            pingTimer.Start();
        }

        [AllowAnonymous]
        public string[] AuthInfo()
        {
            User user = User;
            if (user == null) return new string[0];
            return user.authItems;
        }

        public override bool OnAuthorization(IAuthorizeAttribute authorizeAttribute)
        {
            if (User == null) return false;
            if (!string.IsNullOrWhiteSpace(authorizeAttribute.Roles))
            {
                string[] roles = authorizeAttribute.Roles.Split(',');
                return User.authItems.ContainsAll(roles);
            }
            return User.steam.steamid == authorizeAttribute.Users;
        }

        private void OnOnClose(object sender, OnClientDisconnectArgs onClientDisconnectArgs)
        {
            log.Debug("DISCONNECTED [" + ConnectionContext.PersistentId + "]" +
                      (ConnectionContext.IsAuthenticated ? " [" + User.steam.steamid + "]" : ""));
            if (pingTimer != null)
            {
                pingTimer.Stop();
                pingTimer.Dispose();
                pingTimer = null;
            }
            if (!ConnectionContext.IsAuthenticated || User == null) return;
            SaveChatChannels();
            foreach (var channel in Channels.ToArray())
            {
                if (channel.Leavable) Leave(new LeaveRequest() {Id = channel.Id.ToString()});
                else channel.Members.Remove(User.steam.steamid);
            }
            Channels.Clear();
        }

        private void SaveChatChannels()
        {
            User.channels = Channels.Where(m=>m.Leavable && m.ChannelType == ChannelType.Public).Select(m => m.Name).ToArray();
            Database.Mongo.Users.Update(Query<User>.EQ(m=>m.Id, User.Id), Update<User>.Set(m=>m.channels, User.channels));
        }

        private void LoadChatChannels()
        {
            if (User == null) return;
            if (User.channels == null) User.channels = new string[0];
            foreach (var chan in User.channels)
                JoinOrCreate(new JoinCreateRequest() {Name = chan.ToLower()});
        }

        private void ChatChannelOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                this.Invoke(new ChatChannelUpd(e.NewItems.OfType<ChatChannel>().ToArray()), ChatChannelUpd.Msg);
            if (e.OldItems != null)
                this.Invoke(new ChatChannelRm(e.OldItems.OfType<ChatChannel>().ToArray()), ChatChannelRm.Msg);
        }

        public void SendMessage(Message message)
        {
			if (message == null || !ConnectionContext.IsAuthenticated || !message.Validate() || User == null) return;
            ChatChannel chan = Channels.FirstOrDefault(m => m.Id.ToString() == message.Channel);
            if (chan == null) return;
            log.DebugFormat("[{0}] {1}: \"{2}\"", chan.Name, User.profile.name, message.Text);
            chan.TransmitMessage(chan.Members.Values.FirstOrDefault(m => m.SteamID == User.steam.steamid), message.Text);
        }

        private static object joinLock = new object();
        public string JoinOrCreate(JoinCreateRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.Name)) return "You didn't provide any channels to join.";
            if (member == null) ReloadUser();
            if (member == null) return "Unable to create chat member";
            if (req.OneToOne)
            {
                if (Channels.Any(m => m.ChannelType == ChannelType.OneToOne && m.Members.Values.Any(x => x.SteamID == req.Name)))
                    return "You already are in a private chat with that person.";
                if (req.Name == this.User.steam.steamid) return "You can't have a private chat with yourself.";
                var user = this.Find(m => m.User != null && m.User.steam.steamid == req.Name).FirstOrDefault();
                if (user == null) return "Can't find that person.";
                if (user.member == null) user.ReloadUser();
                if (user.member == null) return "That user is not available to chat.";
                lock (joinLock)
                {
                    var chan =
                        new ChatChannel(
                            User.profile.name.Substring(0, Math.Min(5, User.profile.name.Length)) + " + " +
                            user.User.profile.name.Substring(0, Math.Min(5, user.User.profile.name.Length)),
                            ChannelType.OneToOne);
                    chan.Members.Add(member.ID, member);
                    chan.Members.Add(user.member.ID, user.member);
                    Channels.Add(chan);
                    user.Channels.Add(chan);
                }
                return null;
            }
            else
            {
                req.Name = Regex.Replace(req.Name, @"[^\w\s]", string.Empty).Trim();
                if (string.IsNullOrEmpty(req.Name)) return "That chat name is completely invalid.";
                if (Channels.Any(m => m.Name.ToLower() == req.Name.ToLower()))
                    return "You are already in that channel.";
                try
                {
                    lock (joinLock)
                    {
                        ChatChannel chan = ChatChannel.JoinOrCreate(req.Name.ToLower(), member);
                        if (chan != null)
                            Channels.Add(chan);
                    }
                    return null;
                }
                catch (JoinCreateException ex)
                {
                    return ex.Message;
                }
            }
        }

        public void Leave(LeaveRequest req)
        {
            if (req == null || req.Id == null || User == null) return;
            ChatChannel chan = Channels.FirstOrDefault(m => m.Id.ToString() == req.Id);
            if (chan == null || !chan.Leavable) return;
            if (chan.ChannelType == ChannelType.Public)
            {
                chan.Members.Remove(User.steam.steamid);
                Channels.Remove(chan);
            }
            else if(chan.ChannelType == ChannelType.OneToOne) chan.Close();
        }

        public void ReloadUser()
        {
            if (User == null) return;
            bool overava = User.vouch != null && !string.IsNullOrEmpty(User.vouch.avatar);
            member = new ChatMember(User.steam.steamid, User, overava ? User.vouch.avatar : User.steam.avatarfull);
            foreach (ChatChannel chat in Channels)
            {
                chat.Members[member.ID] = member;
            }
        }
    }
}