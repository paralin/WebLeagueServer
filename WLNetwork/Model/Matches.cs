using WLNetwork.Matches.Enums;

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
        public GameType GameType { get; set; }

        /// <summary>
        /// What to name it?
        /// </summary>
        public string Name { get; set; }
    }
}
