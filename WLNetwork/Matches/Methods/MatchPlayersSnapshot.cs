using System;
using System.Linq;
using WLCommon.Matches;

namespace WLNetwork.Matches.Methods
{
    /// <summary>
    ///     Snapshot of players for a match
    /// </summary>
    public class MatchPlayersSnapshot
    {
        public const string Msg = "matchplayerssnapshot";

        /// <summary>
        ///     Add/update some channels.
        /// </summary>
        /// <param name="members"></param>
        public MatchPlayersSnapshot(MatchGame game)
        {
            Id = game.Id;
            Players = game.Players.ToArray();
        }

        public Guid Id { get; set; }

        /// <summary>
        ///     Matches to add/update
        /// </summary>
        public MatchPlayer[] Players { get; set; }
    }
}