using SteamKit2.GC.Dota.Internal;
using WLNetwork.Matches.Enums;
using WLNetwork.Model;

namespace WLNetwork.Matches
{
    /// <summary>
    ///     A player in a match
    /// </summary>
    public class MatchPlayer
    {
        public MatchPlayer(User user = null)
        {
            if (user != null)
            {
                SID = user.steam.steamid;
                Name = user.profile.name;
                Avatar = user.steam.avatarfull;
                Team = MatchTeam.Dire;
                Rating = user.profile.rating;
            }
        }

        /// <summary>
        ///     SteamID
        /// </summary>
        public string SID { get; set; }

        /// <summary>
        ///     Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Avatar
        /// </summary>
        public string Avatar { get; set; }

        /// <summary>
        ///     Team
        /// </summary>
        public MatchTeam Team { get; set; }

        /// <summary>
        ///     Is ready in the match?
        /// </summary>
        public bool Ready { get; set; }

        /// <summary>
        ///     Is a captain?
        /// </summary>
        public bool IsCaptain { get; set; }

        /// <summary>
        ///     Is this person a leaver?
        /// </summary>
        public bool IsLeaver { get; set; }

        /// <summary>
        ///     Reason they left
        /// </summary>
        public DOTALeaverStatus_t LeaverReason { get; set; }

        /// <summary>
        ///     Rating at the start of the match
        /// </summary>
        public int Rating { get; set; }
    }
}