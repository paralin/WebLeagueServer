

using Dota2.GC.Dota.Internal;

namespace WLNetwork
{
    public class PlayerReadyArgs
    {
        public Player[] Players { get; set; }

        public class Player
        {
            public string SteamID { get; set; }
            public bool IsReady { get; set; }
        }
    }

    public class PlayerHeroArgs
    {
        public ulong steam_id { get; set; }
        public uint hero_id { get; set; }
    }

    public class MatchStateArgs
    {
        public DOTA_GameState State { get; set; }
        public CSODOTALobby.State Status { get; set; }
    }

    public class MatchIdArgs
    {
        public ulong match_id { get; set; }
    }

    public class MatchOutcomeArgs
    {
        public EMatchOutcome match_outcome { get; set; }
    }

    public class LobbyClearArgs
    {
    }

    public class LeaverStatusArgs
    {
        public Player[] Players { get; set; }
        public CSODOTALobby Lobby { get; set; }

        public class Player
        {
            public string SteamID { get; set; }
            public DOTALeaverStatus_t Status { get; set; }
        }
    }
}