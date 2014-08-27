using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WLNetwork.Model;

namespace WLNetwork.Chat
{
    /// <summary>
    /// Member of a chat.
    /// </summary>
    public class ChatMember
    {
        /// <summary>
        /// Steam ID
        /// </summary>
        public string SteamID { get; set; }

        /// <summary>
        /// Name of the member.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Create a chat member.
        /// </summary>
        /// <param name="user">user</param>
        public ChatMember(User user)
        {
            this.SteamID = user.steam.steamid;
            this.Name = user.profile.name;
        }
    }
}
