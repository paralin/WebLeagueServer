using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using D2MPMaster.LiveData;
using Newtonsoft.Json.Linq;
using WLNetwork.Chat.Methods;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Chat
{
    /// <summary>
    /// Instance of a chat channel.
    /// </summary>
    public class ChatChannel
    {
        private static Controllers.Chat ChatController = new Controllers.Chat();
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
                    foreach (var mm in this.Members)
                    {
                        ChatController.InvokeTo(m => m.ConnectionContext.IsAuthenticated && m.User.steam.steamid == mm.SteamID, new ChatMemberUpd(chatMembers), ChatMemberUpd.Msg);
                    }
                }
            }
            if (e.OldItems != null)
            {
                var memb = e.OldItems.OfType<ChatMember>();
                var chatMembers = memb as ChatMember[] ?? memb.ToArray();
                foreach (var mm in this.Members)
                {
                    ChatController.InvokeTo(
                        m => m.ConnectionContext.IsAuthenticated && m.User.steam.steamid == mm.SteamID,
                        new ChatMemberRm(chatMembers), ChatMemberRm.Msg);
                }
            }
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
