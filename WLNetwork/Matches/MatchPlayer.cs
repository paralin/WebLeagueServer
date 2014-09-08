using WLNetwork.Matches.Interfaces;

namespace WLNetwork.Matches
{
    /// <summary>
    /// A player in a match
    /// </summary>
    public class MatchPlayer
    {
        /// <summary>
        /// SteamID
        /// </summary>
        public string SID { get; set; }
        
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Avatar
        /// </summary>
        public string Avatar { get; set; }

        /// <summary>
        /// Team
        /// </summary>
        public MatchTeam Team { get; set; }
    }
}
