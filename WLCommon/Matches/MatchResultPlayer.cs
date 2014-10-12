using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2.GC.Dota.Internal;
using WLCommon.Matches.Enums;

namespace WLCommon.Matches
{
    public class MatchResultPlayer
    {
        public MatchResultPlayer(MatchPlayer player = null)
        {
            if (player != null)
            {
                this.SID = player.SID;
                this.Name = player.Name;
                this.Team = player.Team;
                this.IsCaptain = player.IsCaptain;
                this.IsLeaver = player.IsLeaver;
                this.LeaverReason = player.LeaverReason;
                this.RatingBefore = player.Rating;
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
        /// Team
        /// </summary>
        public MatchTeam Team { get; set; }

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

        /// <summary>
        /// Rating at the start of the match
        /// </summary>
        public int RatingBefore { get; set; }
    }
}
