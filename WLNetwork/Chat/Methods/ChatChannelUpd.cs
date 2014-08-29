using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLNetwork.Chat.Methods
{
    /// <summary>
    /// Add or update some chat channels.
    /// </summary>
    public class ChatChannelUpd
    {
        public const string Msg = "chatchannelupd";

        /// <summary>
        /// Channels to add/update
        /// </summary>
        public ChatChannel[] channels { get; set; }

        /// <summary>
        /// Add/update some channels.
        /// </summary>
        /// <param name="members"></param>
        public ChatChannelUpd(params ChatChannel[] channels)
        {
            this.channels = channels;
        }
    }

    public class ChatChannelRm
    {
        public const string Msg = "chatchannelrm";

        public string[] ids { get; set; }

        /// <summary>
        /// Create a remove op with some members.
        /// </summary>
        /// <param name="mems"></param>
        public ChatChannelRm(params ChatChannel[] chans)
        {
            this.ids = new string[chans.Length];
            int i = 0;
            foreach (var chan in chans)
            {
                this.ids[i] = chan.Id.ToString();
                i++;
            }
        }
    }
}
