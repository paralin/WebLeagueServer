using Dota2.GC.Dota.Internal;

namespace WLNetwork.Bots.Data
{
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