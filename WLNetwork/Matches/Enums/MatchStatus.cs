﻿namespace WLNetwork.Matches.Enums
{
    public enum MatchStatus
    {
        /// <summary>
        ///     Joining the game
        /// </summary>
        Players = 0,

        /// <summary>
        ///     Team selection for Captains
        /// </summary>
        Teams,

        /// <summary>
        ///     Joining the in-game lobby
        /// </summary>
        Lobby,

        /// <summary>
        ///     Game in progress, system will monitor for game completion. It will be in the database as well.
        /// </summary>
        Play,

        /// <summary>
        ///     Game is complete and the result is being fetched
        /// </summary>
        Complete
    }
}