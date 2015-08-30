using System;
using Dota2.GC.Dota.Internal;
using WLNetwork.Matches.Enums;
using WLNetwork.Model;

namespace WLNetwork.Matches
{
    /// <summary>
    ///     Details sent to host to set up a bot.
    /// </summary>
    public class MatchSetupDetails
    {
        /// <summary>
        ///     ID of the matchgame.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        ///     List of players.
        /// </summary>
        public MatchPlayer[] Players { get; set; }

        /// <summary>
        ///     Dota 2 game mode.
        /// </summary>
        public GameMode GameMode { get; set; }

        /// <summary>
        ///     The bot to use.
        /// </summary>
        public Bot Bot { get; set; }

        /// <summary>
        ///     Status of setup.
        /// </summary>
        public MatchSetupStatus Status { get; set; }

        /// <summary>
        ///     Password for the lobby.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        ///     Current game state
        /// </summary>
        public DOTA_GameState State { get; set; }

        /// <summary>
        ///     Match ID
        /// </summary>
        public ulong MatchId { get; set; }

        /// <summary>
        ///     Live match spectator count.
        /// </summary>
        public uint SpectatorCount { get; set; }

        /// <summary>
        ///     First blood happened?
        /// </summary>
        public bool FirstBloodHappened { get; set; }

        /// <summary>
        ///     Time game started
        /// </summary>
        public DateTime GameStartTime { get; set; }

        /// <summary>
        ///     Is this a recovered in progress match
        /// </summary>
        public bool IsRecovered { get; set; }

        /// <summary>
        ///     The server steam id
        /// </summary>
        public string ServerSteamID { get; set; }

        /// <summary>
        ///     Ticket ID
        /// </summary>
        public uint TicketID { get; set; }

        /// <summary>
        ///     Server region
        /// </summary>
        public uint Region { get; set; }
    }
}