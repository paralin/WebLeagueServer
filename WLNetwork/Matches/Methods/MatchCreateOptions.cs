using WLNetwork.Matches.Enums;

namespace WLNetwork.Matches.Methods
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

        /// <summary>
        /// League ID
        /// </summary>
        public string League { get; set; }

        /// <summary>
        /// League season
        /// </summary>
        public uint LeagueSeason { get; set; }

        /// <summary>
        /// Secondary league season
        /// </summary>
        public uint[] SecondaryLeagueSeason { get; set; }

        /// <summary>
        /// League ticket
        /// </summary>
        public uint LeagueTicket { get; set; }

        /// <summary>
        /// League region
        /// </summary>
        public uint LeagueRegion { get; set; }
    }
}