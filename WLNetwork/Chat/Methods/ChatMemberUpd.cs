using System;

namespace WLNetwork.Chat.Methods
{
    /// <summary>
    ///     Add or update some chat memebers.
    /// </summary>
    public class ChatMemberUpd
    {
        public const string Msg = "chatmemberupd";

        /// <summary>
        ///     Add/update some members.
        /// </summary>
        /// <param name="members"></param>
        public ChatMemberUpd(string id, params ChatMember[] members)
        {
            this.id = id;
            this.members = members;
        }

        /// <summary>
        ///     ID of the chat
        /// </summary>
        public string id { get; set; }

        /// <summary>
        ///     Members to add/update
        /// </summary>
        public ChatMember[] members { get; set; }
    }

    public class ChatMemberRm
    {
        public const string Msg = "chatmemberrm";

        /// <summary>
        ///     Create a remove op with some members.
        /// </summary>
        /// <param name="mems"></param>
        public ChatMemberRm(string id, params ChatMember[] mems)
        {
            this.id = id;
            members = new Guid[mems.Length];
            int i = 0;
            foreach (ChatMember mem in mems)
            {
                members[i] = mem.ID;
                i++;
            }
        }

        /// <summary>
        ///     ID of the chat
        /// </summary>
        public string id { get; set; }

        /// <summary>
        ///     Member steamids
        /// </summary>
        public Guid[] members { get; set; }
    }
}