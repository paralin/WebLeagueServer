using Dota2.GC.Dota.Internal;
using WLNetwork.Matches.Enums;

namespace WLNetwork.Matches
{
    public class MatchResultPlayer
    {
        public MatchResultPlayer(MatchPlayer player = null)
        {
            if (player != null)
            {
                SID = player.SID;
                Name = player.Name;
                Team = player.Team;
                IsCaptain = player.IsCaptain;
                IsLeaver = player.IsLeaver;
                LeaverReason = player.LeaverReason;
                RatingBefore = player.Rating;
                WinStreakBefore = player.WinStreak;
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
        ///     Team
        /// </summary>
        public MatchTeam Team { get; set; }

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
        public uint RatingBefore { get; set; }

        /// <summary>
        ///     Win streak before this match
        /// </summary>
        public uint WinStreakBefore { get; set; }
    }
}