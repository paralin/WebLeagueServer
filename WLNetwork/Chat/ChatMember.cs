﻿using System;
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
        public Guid ID { get; set; }

        /// <summary>
        /// Steam ID
        /// </summary>
        public string SteamID { get; set; }

        /// <summary>
        /// Name of the member.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Avatar image URL.
        /// </summary>
        public string Avatar { get; set; }

        /// <summary>
        /// Create a chat member.
        /// </summary>
        /// <param name="user">user</param>
        public ChatMember(Guid id, User user, string avatar = null)
        {
            this.ID = id;
            this.SteamID = user.steam.steamid;
            this.Name = user.profile.name;
            this.Avatar = avatar;
        }
    }
}
