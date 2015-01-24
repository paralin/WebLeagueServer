namespace WLNetwork.Matches.Methods
{
    /// <summary>
    ///     Add or update some public matches.
    /// </summary>
    public class PublicMatchUpd
    {
        public const string Msg = "publicmatchupd";

        /// <summary>
        ///     Add/update some channels.
        /// </summary>
        /// <param name="members"></param>
        public PublicMatchUpd(params MatchGameInfo[] matches)
        {
            this.matches = matches;
        }

        /// <summary>
        ///     Matches to add/update
        /// </summary>
        public MatchGameInfo[] matches { get; set; }
    }

    public class PublicMatchRm
    {
        public const string Msg = "publicmatchrm";

        /// <summary>
        ///     Create a remove op with some members.
        /// </summary>
        /// <param name="mems"></param>
        public PublicMatchRm(params MatchGameInfo[] matches)
        {
            ids = new string[matches.Length];
            int i = 0;
            foreach (MatchGameInfo match in matches)
            {
                ids[i] = match.Id.ToString();
                i++;
            }
        }

        /// <summary>
        ///     IDS of the matches
        /// </summary>
        public string[] ids { get; set; }
    }
}