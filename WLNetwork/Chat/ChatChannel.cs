using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using log4net;
using SteamKit2.GC.Dota.Internal;
using WLNetwork.Chat.Exceptions;
using WLNetwork.Chat.Methods;
using WLNetwork.Utils;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Chat
{
    /// <summary>
    ///     Instance of a chat channel.
    /// </summary>
    public class ChatChannel
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Controllers.Chat ChatController = new Controllers.Chat();

        public static ConcurrentDictionary<Guid, ChatChannel> Channels = new ConcurrentDictionary<Guid, ChatChannel>();

        /// <summary>
        ///     Create a channel with a name and type.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ctype"></param>
        public ChatChannel(string name, ChannelType ctype = ChannelType.Public, bool leavable = true, bool perm = false)
        {
            Id = Guid.NewGuid();
            ChannelType = ctype;
            Permanent = perm;
            Name = name;
            Leavable = leavable;
            Members = new ObservableCollection<string>();
            Members.CollectionChanged += MembersOnCollectionChanged;
            Channels[Id] = this;
            log.DebugFormat("CREATED [{0}] ({1})", Name, Id);
        }

        /// <summary>
        ///     ID of the channel.
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        ///     Name of the channel.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     What kind of channel is it?
        /// </summary>
        public ChannelType ChannelType { get; set; }

        /// <summary>
        ///     Can you leave this chat?
        /// </summary>
        public bool Leavable { get; set; }

        /// <summary>
        ///     Will this channel ever be deleted?
        /// </summary>
        public bool Permanent { get; set; }

        /// <summary>
        ///     Online members of the channel.
        /// </summary>
        public ObservableCollection<string> Members { get; set; }

        /// <summary>
        ///     Handle the collection update event.
        /// </summary>
        /// <param name="s">source</param>
        /// <param name="e">event</param>
        private void MembersOnCollectionChanged(object s, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                var old = e.OldItems.OfType<string>();
                var msg = new ChatMemberRm(Id.ToString(), old.ToArray());
                foreach (var oldm in old)
                {
                    ChatMember nm = null;
                    if(MemberDB.Members.TryGetValue(oldm, out nm) && nm != null)
                        log.DebugFormat("PARTED [{0}] {{{2}}}", Name, Id, nm.Name);
                }
                foreach (var mm in Members)
                {
                    ChatController.InvokeTo(
                        m => m.ConnectionContext.IsAuthenticated && m.User != null && m.User.steam.steamid == mm,
                        msg, ChatMemberRm.Msg);
                }
            }
            if (e.NewItems != null)
            {
                var memb = e.NewItems.OfType<string>().ToArray();
                var msg = new ChatMemberAdd(Id.ToString(), memb);
                foreach (var newm in memb)
                {
                    ChatMember nm = null;
                    if (MemberDB.Members.TryGetValue(newm, out nm) && nm != null)
                        log.DebugFormat("JOINED [{0}] ({1}) {{{2}}}", Name, Id, nm.Name);
                }
                foreach (var mm in Members.ToArray())
                {
                    ChatController.InvokeTo(m => m.ConnectionContext.IsAuthenticated && m.User != null && m.User.steam.steamid == mm, msg,
                        ChatMemberAdd.Msg);
                }
            }
            if (Members.Count == 0) Close(true);
        }

        /// <summary>
        ///     Delete all members and close chat.
        /// </summary>
        public void Close(bool noModifyMembers = false)
        {
            string[] oldMembers = Members.ToArray();
            if (!noModifyMembers)
                Members.Clear();
            foreach (
                Controllers.Chat so in
                    oldMembers.Select(
                        member =>
                            ChatController.Find(
                                m => m.ConnectionContext.IsAuthenticated && m.User.steam.steamid == member))
                        .SelectMany(sox => sox))
            {
                so.Channels.Remove(this);
            }
            if (!Permanent)
            {
                ChatChannel dummy = null;
                Channels.TryRemove(Id, out dummy);
                log.DebugFormat("DELETED [{0}] ({1})", Name, Id);
            }
            else
            {
                log.DebugFormat("PRESERVED [{0}] ({1})", Name, Id);
            }
        }

        /// <summary>
        ///     Send a message to the channel.
        /// </summary>
        /// <param name="memberid">the sender steamid</param>
        /// <param name="text">message</param>
        public void TransmitMessage(string memberid, string text, bool service = false)
        {
            ChatMember member = null;
            if (memberid != "system")
            {
                member = MemberDB.Members[memberid];
                if (member == null && !service)
                {
                    log.ErrorFormat("Message transmit request with no member! Ignoring...");
                    return;
                }
                if (member != null && Members.All(m => m != member.SteamID))
                {
                    log.ErrorFormat("Message transmit with member not in the channel! Ignoring....");
                    return;
                }
            }
            var msg = new ChatMessage { Text = text, Member = memberid, Id = Id.ToString(), Auto = service };
            foreach (var mm in Members)
            {
                var mm1 = mm;
                ChatController.InvokeTo(
                    m => m.ConnectionContext.IsAuthenticated && m.User != null && m.User.steam.steamid == mm1,
                    msg, ChatMessage.Msg);
            }
        }

        /// <summary>
        ///     Join by name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="member"></param>
        /// <returns></returns>
        public static ChatChannel Join(string name, ChatMember member)
        {
            ChatChannel chan = Channels.Values.FirstOrDefault(m => m.Name.ToLower() == name.ToLower());
            if (chan == null) return null;
            return Join(chan.Id, member);
        }

        /// <summary>
        ///     Join by GUID.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="member"></param>
        /// <returns></returns>
        public static ChatChannel Join(Guid id, ChatMember member)
        {
            ChatChannel chan = null;
            if (!Channels.TryGetValue(id, out chan)) return null;
            if (chan.ChannelType != ChannelType.Public && chan.Name != "main")
                throw new JoinCreateException("That channel is not joinable this way.");
            chan.Members.Add(member.SteamID);
            return chan;
        }

        /// <summary>
        /// Send a global system message.
        /// </summary>
        /// <param name="message"></param>
        public static void GlobalSystemMessage(string message)
        {
            log.Debug("[GLOBAL MESSAGE] " + message);
            Channels.Values.First(m => m.Name == "main").TransmitMessage(null, message, true);
        }

        /// <summary>
        ///     Join or create by name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="member"></param>
        /// <param name="chanType"></param>
        /// <returns></returns>
        public static ChatChannel JoinOrCreate(string name, ChatMember member, ChannelType chanType = ChannelType.Public)
        {
            ChatChannel chan = Join(name, member);
            if (chan == null)
            {
                chan = new ChatChannel(name, chanType);
                chan.Members.Add(member.SteamID);
            }
            return chan;
        }
    }

    /// <summary>
    ///     Type of the channel.
    /// </summary>
    public enum ChannelType
    {
        Public,
        OneToOne
    }
}