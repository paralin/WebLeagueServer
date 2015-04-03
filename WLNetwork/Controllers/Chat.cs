﻿using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Timers;
using log4net;
using MongoDB.Driver.Linq;
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
                StartPingTimer();
                JoinOrCreate(new JoinCreateRequest() {Name = "main"});
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
            if (!ConnectionContext.IsAuthenticated) return;
            foreach (ChatChannel channel in Channels)
            {
                channel.Members.Remove(ConnectionId);
            }
            Channels.Clear();
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
            if (message == null || !ConnectionContext.IsAuthenticated || !message.Validate()) return;
            ChatChannel chan = Channels.FirstOrDefault(m => m.Id.ToString() == message.Channel);
            if (chan == null) return;
            log.DebugFormat("[{0}] {1}: \"{2}\"", chan.Name, User.profile.name, message.Text);
            chan.TransmitMessage(chan.Members.Values.FirstOrDefault(m => m.SteamID == User.steam.steamid), message.Text);
        }

        public string JoinOrCreate(JoinCreateRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.Name)) return "You didn't provide any channels to join.";
            req.Name = Regex.Replace(req.Name, @"[^\w\s]", string.Empty).Trim();
            if (Channels.Any(m => m.Name.ToLower() == req.Name.ToLower())) return "You are already in that channel.";
            try
            {
                if (member == null) ReloadUser();
                ChatChannel chan = ChatChannel.JoinOrCreate(req.Name.ToLower(), member);
                if (chan != null)
                    Channels.Add(chan);
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
            ChatChannel chan = Channels.FirstOrDefault(m => m.Id.ToString() == req.Id);
            if (chan == null || !chan.Leavable) return;
            chan.Members.Remove(ConnectionId);
            Channels.Remove(chan);
        }

        public void ReloadUser()
        {
            if (User == null) return;
            bool overava = User.vouch != null && !string.IsNullOrEmpty(User.vouch.avatar);
            member = new ChatMember(PersistentId, User, overava ? User.vouch.avatar : User.steam.avatarfull);
            foreach (ChatChannel chat in Channels)
            {
                foreach (var omember in chat.Members.Where(m => m.Value.SteamID == User.steam.steamid).ToList())
                {
                    chat.Members.Remove(omember.Key);
                }
                chat.Members[member.ID] = member;
            }
        }

        public void BroadcastServiceMessage(string message)
        {
            if (message == null || !ConnectionContext.IsAuthenticated) return;
            foreach (ChatChannel channel in Channels)
            {
                channel.TransmitMessage(member, message, true);
            }
            log.DebugFormat("[BROADCAST] {0}: \"{1}\"", User.profile.name, message);
        }
    }
}