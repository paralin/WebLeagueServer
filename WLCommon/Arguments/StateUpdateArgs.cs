using System;
using WLCommon.LobbyBot.Enums;

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
}
