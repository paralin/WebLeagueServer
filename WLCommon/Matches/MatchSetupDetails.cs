using System;
using WLCommon.Matches.Enums;
using WLCommon.Model;

namespace WLCommon.Matches
{
    /// <summary>
    /// Details sent to host to set up a bot.
    /// </summary>
    public class MatchSetupDetails
    {
        /// <summary>
        /// ID of the matchgame.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// List of players.
        /// </summary>
        public MatchPlayer[] Players { get; set; }

        /// <summary>
        /// Dota 2 game mode.
        /// </summary>
        public GameMode GameMode { get; set; }

        /// <summary>
        /// The bot to use.
        /// </summary>
        public Bot Bot { get; set; }

        /// <summary>
        /// Status of setup.
        /// </summary>
        public MatchSetupStatus Status { get; set; }
    }
}
