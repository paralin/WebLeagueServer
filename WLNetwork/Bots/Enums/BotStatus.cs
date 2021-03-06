﻿namespace WLNetwork.Bots.Enums
{
    public enum BotAssignmentStatus
    {
        /// <summary>
        ///     Ready to be used
        /// </summary>
        Available,

        /// <summary>
        ///     Currently in use
        /// </summary>
        InUse,

        /// <summary>
        ///     Not a valid account (will be purged)
        /// </summary>
        Invalid
    }
}