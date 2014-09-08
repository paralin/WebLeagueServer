﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLNetwork.Matches.Enums
{
    public enum MatchStatus : int
    {
        /// <summary>
        /// Joining the game
        /// </summary>
        Players=0,

        /// <summary>
        /// Joining the in-game lobby
        /// </summary>
        Lobby,

        /// <summary>
        /// Game in progress, system will monitor for game completion. It will be in the database as well.
        /// </summary>
        Play,

        /// <summary>
        /// Game is complete and should be ignored. Ultimately the match should be out of the server memory at this point
        /// </summary>
        Complete
    }
}
