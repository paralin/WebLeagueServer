using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using WLNetwork.Chat.Methods;
using WLNetwork.Model;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Chat
{
    /// <summary>
    /// Instance of a chat channel.
    /// </summary>
    public class ChatChannel
    {
        private static readonly log4net.ILog log =
log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static Controllers.Chat ChatController = new Controllers.Chat();

        public static ConcurrentDictionary<Guid, ChatChannel> Channels = new ConcurrentDictionary<Guid, ChatChannel>();

        /// <summary>
        /// ID of the channel.
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// Name of the channel.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// What kind of channel is it?
        /// </summary>
        public ChannelType ChannelType { get; set; }

        /// <summary>
        /// Online members of the channel.
        /// </summary>
        public ObservableCollection<ChatMember> Members { get; set; } 

        /// <summary>
        /// Create a channel with a name and type.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ctype"></param>
        public ChatChannel(string name, ChannelType ctype = ChannelType.Public)
        {
            this.Id = Guid.NewGuid();
            this.ChannelType = ctype;
            this.Name = name;
            this.Members = new ObservableCollection<ChatMember>();
            this.Members.CollectionChanged += MembersOnCollectionChanged;
            Channels[this.Id]=this;
        }
        
        /// <summary>
        /// Handle the collection update event.
        /// </summary>
        /// <param name="s">source</param>
        /// <param name="e">event</param>
        private void MembersOnCollectionChanged(object s, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                var memb = e.NewItems.OfType<ChatMember>();
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    var chatMembers = memb as ChatMember[] ?? memb.ToArray();
                    var msg = new ChatMemberUpd(chatMembers);
                    foreach (var mm in this.Members)
                    {
                        ChatController.InvokeTo(m => m.ConnectionContext.IsAuthenticated && m.User.steam.steamid == mm.SteamID, msg, ChatMemberUpd.Msg);
                    }
                }
            }
            if (e.OldItems != null)
            {
                var memb = e.OldItems.OfType<ChatMember>();
                var chatMembers = memb as ChatMember[] ?? memb.ToArray();
                var msg = new ChatMemberRm(chatMembers);
                foreach (var mm in this.Members)
                {
                    ChatController.InvokeTo(
                        m => m.ConnectionContext.IsAuthenticated && m.User.steam.steamid == mm.SteamID,
                        msg, ChatMemberRm.Msg);
                }
            }
            if (Members.Count == 0) Close(true);
        }

        /// <summary>
        /// Delete all members and close chat.
        /// </summary>
        public void Close(bool noModifyMembers=false)
        {
            var oldMembers = Members.ToArray();
            if(!noModifyMembers)
                this.Members.Clear();
            foreach (var so in oldMembers.Select(member => ChatController.Find(m => m.ConnectionContext.IsAuthenticated && m.User.steam.steamid == member.SteamID)).SelectMany(sox => sox))
            {
                so.Channels.Remove(this);
            }
            ChatChannel dummy = null;
            Channels.TryRemove(this.Id, out dummy);
        }

        /// <summary>
        /// Send a message to the channel.
        /// </summary>
        /// <param name="member">the sender</param>
        /// <param name="text">message</param>
        public void TransmitMessage(ChatMember member, string text)
        {
            if (member == null)
            {
                log.ErrorFormat("Message transmit request with no member! Ignoring...");
                return;
            }
            if (this.Members.All(m => m.SteamID != member.SteamID))
            {
                log.ErrorFormat("Message transmit with member not in the channel! Ignoring....");
                return;
            }
            var msg = new ChatMessage() {Text = text, Member = member};
            foreach (var mm in this.Members)
            {
                ChatController.InvokeTo(
                        m => m.ConnectionContext.IsAuthenticated && m.User.steam.steamid == mm.SteamID,
                        msg, ChatMessage.Msg);
            }
        }

        /// <summary>
        /// Join by name.
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
        /// Join by GUID.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="member"></param>
        /// <returns></returns>
        public static ChatChannel Join(Guid id, ChatMember member)
        {
            ChatChannel chan = null;
            if (!Channels.TryGetValue(id, out chan)) return null;
            chan.Members.Add(member);
            return chan;
        }

        /// <summary>
        /// Join or create by name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="member"></param>
        /// <param name="chanType"></param>
        /// <returns></returns>
        public static ChatChannel JoinOrCreate(string name, ChatMember member, ChannelType chanType = ChannelType.Public)
        {
            var chan = Join(name, member);
            if (chan == null)
            {
                chan = new ChatChannel(name, chanType);
                chan.Members.Add(member);
            }
            return chan;
        }
    }

    /// <summary>
    /// Type of the channel.
    /// </summary>
    public enum ChannelType
    {
        Public,
        OneToOne,
        PlayerPool,
        Lobby
    }
}
