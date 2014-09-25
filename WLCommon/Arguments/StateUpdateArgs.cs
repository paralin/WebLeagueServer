using System;
using WLCommon.LobbyBot.Enums;

namespace WLCommon.Arguments
{
    public class StateUpdateArgs
    {
        public Guid Id { get; set; }
        public States State { get; set; }
    }
}
