using System.Collections.Generic;
using WLNetwork.Matches.Interfaces;

namespace WLNetwork.Matches
{
    /// <summary>
    /// A instance of a match.
    /// </summary>
    public class MatchGame
    {
        /// <summary>
        /// Public info about the match.
        /// </summary>
        public MatchGameInfo Info { get; set; }

        /// <summary>
        /// Game type.
        /// </summary>
        public GameType GameType { get; set; }

        /// <summary>
        /// Players
        /// </summary>
        public List<MatchPlayer> Players { get; set; } 
    }

    /// <summary>
    /// Match information.
    /// </summary>
    public class MatchGameInfo
    {
        public string Name { get; set; }
        public MatchType Mode { get; set; }
        public bool Public { get; set; }
    }
}
