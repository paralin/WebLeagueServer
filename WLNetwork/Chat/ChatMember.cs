using System;
using System.Linq;
using WLNetwork.Model;

namespace WLNetwork.Chat
{
    /// <summary>
    ///     Member of a chat.
    /// </summary>
    public class ChatMember
    {
        /// <summary>
        ///     Create a chat member.
        /// </summary>
        /// <param name="user">user</param>
        public ChatMember(string id, User user, string avatar = null)
        {
            ID = id;
            SteamID = user.steam.steamid;
            UID = user.Id;
            Name = user.profile.name;
            Rating = user.profile.rating;
            WinStreak = user.profile.winStreak;
            Avatar = avatar;

            if (user.authItems.Contains("admin"))
                MemberType = ChatMemberType.Admin;
            else if (user.authItems.Contains("vouch"))
                MemberType = ChatMemberType.Moderator;
            else if (user.authItems.Contains("spectateOnly"))
                MemberType = ChatMemberType.Spectator;
            else
                MemberType = ChatMemberType.Normal;
        }

        public string ID { get; set; }

        /// <summary>
        ///     Steam ID
        /// </summary>
        public string SteamID { get; set; }

        /// <summary>
        ///     User ID
        /// </summary>
        public string UID { get; set; }

        /// <summary>
        ///     Name of the member.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Avatar image URL.
        /// </summary>
        public string Avatar { get; set; }

        /// <summary>
        ///     Rating
        /// </summary>
        public uint Rating { get; set; }

        /// <summary>
        ///     Current win streak
        /// </summary>
        public uint WinStreak { get; set; }

        /// <summary>
        ///     Member type for visibility in the player list
        /// </summary>
        public ChatMemberType MemberType { get; set; }

        public enum ChatMemberType : int
        {
            Spectator = -1,
            Normal = 0,
            Moderator = 1,
            Admin = 2
        }
    }
}