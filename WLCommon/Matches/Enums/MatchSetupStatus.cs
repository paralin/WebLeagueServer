namespace WLCommon.Matches.Enums
{
    /// <summary>
    /// Status of the bot
    /// </summary>
    public enum MatchSetupStatus : int
    {
        /// <summary>
        /// Waiting for an available bot to setup the match
        /// </summary>
        Queue=0,

        /// <summary>
        /// Waiting for an available host to setup the bot
        /// </summary>
        QueueHost,

        /// <summary>
        /// BotHost is setting up Dota 2
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
