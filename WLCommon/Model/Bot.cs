using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLCommon.Model
{
    /// <summary>
    /// An individual bot account with state.
    /// </summary>
    public class Bot
    {
        /// <summary>
        /// ID of the bot
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Username of the account.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Password of the account.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Is this account valid? Will be flagged invalid if system can't sign in with the account.
        /// </summary>
        public bool Invalid { get; set; }
    }
}
