using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WLCommon.Matches;

namespace WLNetwork.Matches.Methods
{
    /// <summary>
    /// Snapshot of players for a match
    /// </summary>
    public class MatchPlayersSnapshot
    {
        public const string Msg = "matchplayerssnapshot";

        public Guid Id { get; set; }

        /// <summary>
        /// Matches to add/update
        /// </summary>
        public MatchPlayer[] Players { get; set; }

        /// <summary>
        /// Add/update some channels.
        /// </summary>
        /// <param name="members"></param>
        public MatchPlayersSnapshot(MatchGame game)
        {
            this.Id = game.Id;
            this.Players = game.Players.ToArray();
        }
    }
}
