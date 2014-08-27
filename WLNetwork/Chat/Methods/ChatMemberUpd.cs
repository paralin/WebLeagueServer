using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLNetwork.Chat.Methods
{
    /// <summary>
    /// Add or update some chat memebers.
    /// </summary>
    public class ChatMemberUpd
    {
        public const string Msg = "chatmemberupd";

        /// <summary>
        /// ID of the chat
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Members to add/update
        /// </summary>
        public ChatMember[] members { get; set; }

        /// <summary>
        /// Add/update some members.
        /// </summary>
        /// <param name="members"></param>
        public ChatMemberUpd(params ChatMember[] members)
        {
            this.members = members;
        }
    }
    
    public class ChatMemberRm
    {
        public const string Msg = "chatmemberrm";

        /// <summary>
        /// ID of the chat
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Member steamids
        /// </summary>
        public string[] members { get; set; }

        /// <summary>
        /// Create a remove op with some members.
        /// </summary>
        /// <param name="mems"></param>
        public ChatMemberRm(params ChatMember[] mems)
        {
            this.members = new string[mems.Length];
            int i = 0;
            foreach (var mem in mems)
            {
                this.members[i] = mem.SteamID;
                i++;
            }
        }
    }
}
