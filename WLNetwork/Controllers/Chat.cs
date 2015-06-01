using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Linq;
using WLNetwork.Chat;
using WLNetwork.Chat.Enums;
using WLNetwork.Chat.Exceptions;
using WLNetwork.Chat.Methods;
using WLNetwork.Database;
using WLNetwork.Model;
using XSockets.Core.Common.Socket.Attributes;
using XSockets.Core.Common.Socket.Event.Arguments;
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
        
        public Chat()
        {
            Channels.CollectionChanged += ChatChannelOnCollectionChanged;
            OnClose += OnOnClose;
            OnOpen += (sender, args) =>
            {
                log.Debug("CONNECTED [" + ConnectionContext.PersistentId + "]");
                SendMemberList();
                ReloadUser();
                if (member != null)
                {
                    member.State = UserState.ONLINE;
                    member.StateDesc = "Online";
                }
            };
            OnAuthorizationFailed +=
                (sender, args) =>
                    log.Warn("Failed authorize for " + args.MethodName + " [" + ConnectionContext.PersistentId +
                             "]" + (ConnectionContext.IsAuthenticated ? " [" + User.steam.steamid + "]" : ""));
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
            if (member != null)
            {
                member.StateDesc = "Offline";
                member.State = UserState.OFFLINE;
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

        /// <summary>
        /// Complete member list snapshot
        /// </summary>
        private void SendMemberList()
        {
            this.Invoke(new GlobalMemberSnapshot(MemberDB.Members.Values.ToArray()), GlobalMemberSnapshot.Msg);
        }

        private void SaveChatChannels()
        {
            User.channels = Channels.Where(m=>m.Leavable && m.ChannelType == ChannelType.Public).Select(m => m.Name).ToArray();
             Mongo.Users.Update(Query<User>.EQ(m=>m.Id, User.Id), Update<User>.Set(m=>m.channels, User.channels));
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
            chan.TransmitMessage(User.steam.steamid, message.Text);
        }

        private static object joinLock = new object();
        public string JoinOrCreate(JoinCreateRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.Name)) return "You didn't provide any channels to join.";
            if (member == null) ReloadUser();
            if (member == null) return "Unable to create chat member";
            if (req.OneToOne)
            {
                if (Channels.Any(m => m.ChannelType == ChannelType.OneToOne && m.Members.Contains(req.Name)))
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
                    chan.Members.Add(member.ID);
                    chan.Members.Add(user.member.ID);
                    Channels.Add(chan);
                    user.Channels.Add(chan);
                }
                return null;
            }
            else
            {
#if ENABLE_PUBLIC
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
#else
                return "Public channels are disabled.";
#endif
            }
        }

        /// <summary>
        /// Leave a chat
        /// </summary>
        /// <param name="req"></param>
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

        /// <summary>
        /// Recheck chats
        /// </summary>
        private void RecheckChats()
        {
            if (member == null || member.Leagues == null) return;
            var leagueChans = Channels.Where(m => m.ChannelType == ChannelType.League);
            foreach (var league in leagueChans.ToArray())
            {
                if (!member.Leagues.Contains(league.Name))
                {
                    league.Members.Remove(User.steam.steamid);
                    Channels.Remove(league);
                }
            }
            foreach (var league in member.Leagues)
            {
                if (Channels.All(m => m.Name != league))
                {
                    lock (joinLock)
                    {
                        ChatChannel chan = ChatChannel.JoinOrCreate(league, member, ChannelType.League);
                        if (chan != null)
                            Channels.Add(chan);
                    }
                }
            }
        }

        public void ReloadUser()
        {
            if (User == null) return;
            ChatMember oldMember = member;

            ChatMember memb = null;
            if (MemberDB.Members.TryGetValue(User.steam.steamid, out memb) && memb != null)
            {
                member = memb;
            }
            else
            {
                MemberDB.UpdateDB();
                if (!MemberDB.Members.TryGetValue(User.steam.steamid, out memb) || memb == null)
                {
                    log.Warn("Unable to find ChatMember for user " + User.profile.name + "!");
                }
            }
            if (memb != null && oldMember != memb)
            {
                if(oldMember != null) oldMember.PropertyChanged -= MemberPropertyChanged;
                memb.PropertyChanged += MemberPropertyChanged;
            }
            RecheckChats();
        }

        /// <summary>
        /// Watch changes on the member.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="propertyChangedEventArgs"></param>
        private void MemberPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs.PropertyName == "Leagues")
            {
                RecheckChats();
            }
        }
    }
}
