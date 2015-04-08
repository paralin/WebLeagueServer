using System;
using WLNetwork.Matches.Enums;

namespace WLNetwork.Model
{
    public class MatchCreateOptions
    {
        /// <summary>
        ///     What type of match?
        /// </summary>
        /// <value>The type of the match.</value>
        public MatchType MatchType { get; set; }

        /// <summary>
        ///     What game mode?
        /// </summary>
        public GameMode GameMode { get; set; }

        /// <summary>
        ///     If captains, opponent SID
        /// </summary>
        public string OpponentSID { get; set; }
    }

    public class MatchJoinOptions
    {
        /// <summary>
        ///     Match ID
        /// </summary>
        public Guid Id { get; set; }
    }

    public class FillChatPlayersOptions
    {
        public string ChatName { get; set; }
    }
}