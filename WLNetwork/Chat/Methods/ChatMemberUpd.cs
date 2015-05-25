using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLNetwork.Chat.Methods
{
    /// <summary>
    ///     Add a member to a chat
    /// </summary>
    public class ChatMemberAdd
    {
        public const string Msg = "chatmemberadd";

        /// <summary>
        ///     Add/update some members.
        /// </summary>
        /// <param name="id">Chat ID</param>
        /// <param name="members">Members to add</param>
        public ChatMemberAdd(string id, params string[] members)
        {
            this.id = id;
            this.members = members;
        }

        /// <summary>
        ///     Members steamids to add
        /// </summary>
        public string[] members { get; set; }

        /// <summary>
        /// Chat ID
        /// </summary>
        public string id { get; set; }
    }

    public class ChatMemberRm
    {
        public const string Msg = "chatmemberrm";

        /// <summary>
        ///     Remove a global member
        /// </summary>
        /// <param name="mems"></param>
        public ChatMemberRm(string id, params string[] members)
        {
            this.id = id;
            this.members = members;
        }

        /// <summary>
        ///     Member steamids
        /// </summary>
        public string[] members { get; set; }

        /// <summary>
        /// Chat ID
        /// </summary>
        public string id { get; set; }
    }
}
