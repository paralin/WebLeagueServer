using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using log4net;
using WLNetwork.Clients;
using WLNetwork.Model;

namespace WLNetwork.Chat
{
    /// <summary>
    ///     Instance of a chat channel.
    /// </summary>
    public class ChatChannel
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        ///     Channels / ID pairs.
        /// </summary>
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
        public Guid Id { get; }

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
                foreach (var oldm in old)
                {
                    ChatMember nm;
                    if (MemberDB.Members.TryGetValue(oldm, out nm) && nm != null)
                        log.DebugFormat("PARTED [{0}] {{{2}}}", Name, Id, nm.Name);
                }
                foreach (var mm in Members.ToArray())
                {
                    BrowserClient cli;
                    if (!BrowserClient.ClientsBySteamID.TryGetValue(mm, out cli)) continue;
                    foreach (var ccli in cli.ChatClients.Values)
                        ccli.ChatMemberRemoved(Id.ToString(), old.ToArray());
                }
            }
            if (e.NewItems != null)
            {
                var memb = e.NewItems.OfType<string>().ToArray();
                foreach (var newm in memb)
                {
                    ChatMember nm;
                    if (MemberDB.Members.TryGetValue(newm, out nm) && nm != null)
                        log.DebugFormat("JOINED [{0}] ({1}) {{{2}}}", Name, Id, nm.Name);
                }
                foreach (var mm in Members.ToArray())
                {
                    BrowserClient cli;
                    if (!BrowserClient.ClientsBySteamID.TryGetValue(mm, out cli)) continue;
                    foreach (var ccli in cli.ChatClients.Values)
                        ccli.ChatMemberAdd(Id.ToString(), memb);
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
            foreach (var client in oldMembers)
            {
                BrowserClient cli;
                if (!BrowserClient.ClientsBySteamID.TryGetValue(client, out cli)) continue;
                cli.Channels.Remove(this);
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
        /// <param name="filterToId">Filter the message to a single user</param>
        public void TransmitMessage(string memberid, string text, bool service = false, string filterToId = null)
        {
            ChatMember member = null;
            if (memberid == null) memberid = "system";
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
            foreach (var mm in Members.Where(m => filterToId == null || m == filterToId))
            {
                var mm1 = mm;
                BrowserClient cli;
                if (!BrowserClient.ClientsBySteamID.TryGetValue(mm1, out cli)) continue;
                // OnChatMessage: ID, sender id, text, service (true/false), datetime, name of channel
                foreach (var ccli in cli.ChatClients.Values)
                    ccli.OnChatMessage(Id.ToString(), memberid, text, service, DateTime.UtcNow, Name);
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
            var chan =
                Channels.Values.FirstOrDefault(
                    m => string.Equals(m.Name, name, StringComparison.CurrentCultureIgnoreCase));
            return chan == null ? null : Join(chan.Id, member);
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
            //if (chan.ChannelType != ChannelType.Public && chan.Name != "main")
            //    throw new JoinCreateException("That channel is not joinable this way.");
            if (!chan.Members.Contains(member.SteamID)) chan.Members.Add(member.SteamID);
            return chan;
        }

        /// <summary>
        ///     Send a global system message.
        /// </summary>
        /// <param name="league"></param>
        /// <param name="message"></param>
        /// <param name="filterToId">filter to send to just 1 steam id</param>
        public static void SystemMessage(string league, string message, string filterToId = null)
        {
            if (filterToId == null)
                log.Debug($"[SYSTEM MESSAGE] [{league}] {message}");
            var chan = Channels.Values.FirstOrDefault(m => m.Name == league);
            chan?.TransmitMessage(null, message, true, filterToId);
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
            if (chan != null) return chan;
            chan = new ChatChannel(name, chanType);
            if (chanType == ChannelType.League) chan.Leavable = false;
            if (!chan.Members.Contains(member.SteamID)) chan.Members.Add(member.SteamID);
            return chan;
        }

        /// <summary>
        ///     Sends all MOTD messages to a channel
        /// </summary>
        /// <param name="id"></param>
        public static void TransmitMOTD(string id, League league)
        {
            log.Debug($"[MOTD] [{id}] Transmitting messages.");
            var chan = Channels.Values.FirstOrDefault(m => m.Name == id);

            foreach (var msg in league.MotdMessages)
                chan?.TransmitMessage(null, "MOTD: " + msg, true);
        }
    }

    /// <summary>
    ///     Type of the channel.
    /// </summary>
    public enum ChannelType
    {
        Public,
        OneToOne,
        League
    }
}