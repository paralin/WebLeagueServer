using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLNetwork.Matches.Methods
{
    /// <summary>
    /// Add or update some match players.
    /// </summary>
    public class MatchPlayerUpd
    {
        public const string Msg = "matchplayerupd";

        /// <summary>
        /// ID of the match
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Players to add/update
        /// </summary>
        public MatchPlayer[] players { get; set; }

        /// <summary>
        /// Add/update some channels.
        /// </summary>
        /// <param name="members"></param>
        public MatchPlayerUpd(Guid id, params MatchPlayer[] players)
        {
            this.Id = id;
            this.players = players;
        }
    }

    public class MatchPlayerRm
    {
        public const string Msg = "matchplayerrm";

        /// <summary>
        /// ID of the match
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// IDS of the players (steam id)
        /// </summary>
        public string[] ids { get; set; }

        /// <summary>
        /// Create a remove op with some members.
        /// </summary>
        /// <param name="mems"></param>
        public MatchPlayerRm(Guid matchId, params MatchPlayer[] plyrs)
        {
            this.Id = matchId;
            this.ids = new string[plyrs.Length];
            int i = 0;
            foreach (var plyr in plyrs)
            {
                this.ids[i] = plyr.SID;
                i++;
            }
        }
    }
}
