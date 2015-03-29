namespace WLNetwork.Matches.Methods
{
    /// <summary>
    ///     Add or update some available games
    /// </summary>
    public class AvailableGameUpd
    {
        public const string Msg = "availablegameupd";

        /// <summary>
        ///     Add/update some games.
        /// </summary>
        /// <param name="members"></param>
        public AvailableGameUpd(params MatchGame[] matches)
        {
            this.matches = matches;
        }

        /// <summary>
        ///     Games to add/update
        /// </summary>
        public MatchGame[] matches { get; set; }
    }

    public class AvailableGameRm
    {
        public const string Msg = "availablegamerm";

        /// <summary>
        ///     Create a remove op with some members.
        /// </summary>
        /// <param name="mems"></param>
        public AvailableGameRm(params MatchGame[] matches)
        {
            ids = new string[matches.Length];
            int i = 0;
            foreach (MatchGame match in matches)
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