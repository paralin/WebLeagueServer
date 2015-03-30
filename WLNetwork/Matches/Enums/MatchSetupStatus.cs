namespace WLNetwork.Matches.Enums
{
    /// <summary>
    ///     Status of the bot
    /// </summary>
    public enum MatchSetupStatus
    {
        /// <summary>
        ///     Waiting for an available bot to setup the match
        /// </summary>
        Queue = 0,

        /// <summary>
        ///     BotHost is setting up Dota 2
        /// </summary>
        Init = 2,

        /// <summary>
        ///     Waiting for players to get in correct slots
        /// </summary>
        Wait = 3,

        /// <summary>
        ///     Ready to start the game
        /// </summary>
        Done = 4
    }
}