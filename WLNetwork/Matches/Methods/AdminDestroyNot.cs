namespace WLNetwork.Matches.Methods
{
    /// <summary>
    ///    System not
    /// </summary>
    public class SystemMsg
    {
        public const string Msg = "sysnot";

        /// <summary>
        ///     Add/update some games.
        /// </summary>
        /// <param name="members"></param>
        public SystemMsg(string title, string message)
        {
            this.Title = title;
            this.Message = message;
        }

        /// <summary>
        ///     Games to add/update
        /// </summary>
        public string Title { get; set; }

        public string Message { get; set; }
    }
}