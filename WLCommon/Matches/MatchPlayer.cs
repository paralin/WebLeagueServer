using SteamKit2.GC.Dota.Internal;
using WLCommon.Matches.Enums;
using WLCommon.Model;

namespace WLCommon.Matches
{
    /// <summary>
    /// A player in a match
    /// </summary>
    public class MatchPlayer
    {
        public MatchPlayer(User user=null)
        {
            if (user != null)
            {
                this.SID = user.steam.steamid;
                this.Name = user.profile.name;
                this.Avatar = user.steam.avatarfull;
                this.Team = MatchTeam.Dire;
            }
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

        /// <summary>
        /// Is ready in the match?
        /// </summary>
        public bool Ready { get; set; }

        /// <summary>
        /// Is a captain?
        /// </summary>
        public bool IsCaptain { get; set; }

        /// <summary>
        /// Is this person a leaver?
        /// </summary>
        public bool IsLeaver { get; set; }

        /// <summary>
        /// Reason they left
        /// </summary>
        public DOTALeaverStatus_t LeaverReason { get; set; }
    }
}
