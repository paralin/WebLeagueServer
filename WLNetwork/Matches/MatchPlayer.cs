using WLCommon.Matches.Enums;
using WLCommon.Model;
using WLNetwork.Model;

namespace WLNetwork.Matches
{
    /// <summary>
    /// A player in a match
    /// </summary>
    public class MatchPlayer
    {
        public MatchPlayer(User user)
        {
            this.SID = user.steam.steamid;
            this.Name = user.profile.name;
            this.Avatar = user.steam.avatarfull;
            this.Team = MatchTeam.Dire;
        }

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
