using System;
using System.Linq;

namespace WLNetwork.Matches.Methods
{
    /// <summary>
    ///     Snapshot of players for a match
    /// </summary>
    public class MatchPlayersSnapshot
    {
        public const string Msg = "matchplayerssnapshot";

        /// <summary>
        ///     Update players object
        /// </summary>
        /// <param name="members"></param>
        public MatchPlayersSnapshot(MatchGame game)
        {
            Id = game.Id;
            Players = game.Players.ToArray();
        }

        public Guid Id { get; set; }

        /// <summary>
        ///     Players
        /// </summary>
        public MatchPlayer[] Players { get; set; }
    }
}