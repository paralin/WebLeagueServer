namespace WLNetwork.Chat.Methods
{
    /// <summary>
    ///     Add or update some chat channels.
    /// </summary>
    public class ChatChannelUpd
    {
        public const string Msg = "chatchannelupd";

        /// <summary>
        ///     Add/update some channels.
        /// </summary>
        /// <param name="members"></param>
        public ChatChannelUpd(params ChatChannel[] channels)
        {
            this.channels = channels;
        }

        /// <summary>
        ///     Channels to add/update
        /// </summary>
        public ChatChannel[] channels { get; set; }
    }

    public class ChatChannelRm
    {
        public const string Msg = "chatchannelrm";

        /// <summary>
        ///     Create a remove op with some members.
        /// </summary>
        /// <param name="mems"></param>
        public ChatChannelRm(params ChatChannel[] chans)
        {
            ids = new string[chans.Length];
            int i = 0;
            foreach (ChatChannel chan in chans)
            {
                ids[i] = chan.Id.ToString();
                i++;
            }
        }

        public string[] ids { get; set; }
    }
}