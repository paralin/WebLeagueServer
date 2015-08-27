using Dota2.GC.Dota.Internal;

namespace WLNetwork.Bots.Data
{
    public class MatchStateArgs
    {
        public DOTA_GameState State { get; set; }
        public CSODOTALobby.State Status { get; set; }
    }
}
