using System;
using SteamKit2.GC.Dota.Internal;
using WLCommon.DOTABot.Enums;

namespace WLCommon.Arguments
{
    public class StateUpdateArgs
    {
        public Guid Id { get; set; }
        public States State { get; set; }
    }
    public class PlayerReadyArgs
    {
        public Guid Id { get; set; }
        public Player[] Players { get; set; }

        public class Player
        {
            public string SteamID { get; set; }
            public bool IsReady { get; set; }
        }
    }

    public class MatchStateArgs
    {
        public Guid Id { get; set; }
        public DOTA_GameState State { get; set; }
        public CSODOTALobby.State Status { get; set; }
    }

    public class FetchMatchResultArgs
    {
        public Guid Id { get; set; }
        public ulong MatchId { get; set; }
        public CMsgDOTAMatch Match { get; set; }
    }

    public class MatchIdArgs
    {
        public Guid Id { get; set; }
        public ulong match_id { get; set; }
    }

    public class MatchOutcomeArgs
    {
        public Guid Id { get; set; }
        public EMatchOutcome match_outcome { get; set; }
    }

    public class LobbyClearArgs
    {
        public Guid Id { get; set; }
    }

    public class LeaverStatusArgs
    {
        public Guid Id { get; set; }
        public Player[] Players { get; set; }

        public class Player
        {
            public string SteamID { get; set; }
            public DOTALeaverStatus_t Status { get; set; }
        }
    }
}
