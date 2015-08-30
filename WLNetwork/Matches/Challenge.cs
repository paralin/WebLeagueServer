using WLNetwork.Matches.Enums;

namespace WLNetwork.Matches
{
    public class Challenge
    {
        /// <summary>
        ///     Challenger name
        /// </summary>
        public string ChallengerName { get; set; }

        /// <summary>
        ///     The steam id of the person challenging
        /// </summary>
        public string ChallengerSID { get; set; }

        /// <summary>
        ///     The challenged person
        /// </summary>
        public string ChallengedSID { get; set; }

        /// <summary>
        ///     Name of the challenged
        /// </summary>
        public string ChallengedName { get; set; }

        /// <summary>
        ///     League ID
        /// </summary>
        public string League { get; set; }

        /// <summary>
        ///     Game mode
        /// </summary>
        public GameMode GameMode { get; set; }

        /// <summary>
        ///     You can also do 1v1 challenge
        /// </summary>
        public MatchType MatchType { get; set; }
    }
}