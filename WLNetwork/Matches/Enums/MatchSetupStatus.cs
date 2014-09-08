namespace WLNetwork.Matches.Enums
{
    /// <summary>
    /// Status of the bot
    /// </summary>
    public enum MatchSetupStatus
    {
        /// <summary>
        /// Waiting for an available bot to setup the match
        /// </summary>
        Queue,

        /// <summary>
        /// Bot is setting up Dota 2
        /// </summary>
        Init,

        /// <summary>
        /// Waiting for players to get in correct slots
        /// </summary>
        Wait,

        /// <summary>
        /// Ready to start the game
        /// </summary>
        Done
    }
}
