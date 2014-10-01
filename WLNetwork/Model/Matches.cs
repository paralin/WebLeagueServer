using System;
using WLCommon.Matches.Enums;

namespace WLNetwork.Model
{
    public class MatchCreateOptions
    {
        /// <summary>
        /// What kind of match?
        /// </summary>
        public MatchType MatchType { get; set; }

        /// <summary>
        /// What game mode?
        /// </summary>
        public GameMode GameMode { get; set; }

        /// <summary>
        /// What to name it?
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// If captains, opponent SID
        /// </summary>
        public string OpponentSID { get; set; }
    }

    public class MatchJoinOptions
    {
        /// <summary>
        /// Match ID
        /// </summary>
        public Guid Id { get; set; }
    }
}
