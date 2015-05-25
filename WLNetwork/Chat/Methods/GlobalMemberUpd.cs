using System;

namespace WLNetwork.Chat.Methods
{
    /// <summary>
    ///     Add or update a global user.
    /// </summary>
    public class GlobalMemberSnapshot
    {
        public const string Msg = "globalmembersnap";

        /// <summary>
        ///     Add/update some members.
        /// </summary>
        /// <param name="members"></param>
        public GlobalMemberSnapshot(params ChatMember[] members)
        {
            this.members = members;
        }

        /// <summary>
        ///     Members to add/update
        /// </summary>
        public ChatMember[] members { get; set; }
    }

    public class GlobalMemberUpdate
    {
        public const string Msg = "globalmemberupdate";

        /// <summary>
        /// Create a member update with a property k,v
        /// </summary>
        /// <param name="id">steam ID</param>
        /// <param name="memberid">key</param>
        /// <param name="val">value</param>
        public GlobalMemberUpdate(string id, string memberid, object val)
        {
            this.id = id;
            this.key = memberid;
            this.value = val;
        }

        public string id { get; set; }
        public string key { get; set; }
        public object value { get; set; }
    }

    public class GlobalMemberRm
    {
        public const string Msg = "globalmemberrm";

        /// <summary>
        ///     Remove a global member
        /// </summary>
        /// <param name="mems"></param>
        public GlobalMemberRm(params string[] id)
        {
            this.members = id;
        }

        /// <summary>
        ///     Member steamids
        /// </summary>
        public string[] members { get; set; }
    }
}