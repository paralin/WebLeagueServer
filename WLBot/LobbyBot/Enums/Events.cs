using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLBot.LobbyBot.Enums
{
    public enum Events
    {
        Connected,
        Disconnected,
        DotaGCReady,
        DotaToMainMenu,
        DotaJoinedLobby,
        DotaLeftLobby,
        DotaCreatedLobby,
        DotaGCDisconnect,
        LogonFailSteamGuard,
        LogonFailBadCreds,
        AttemptReconnect,
        DotaEnterLobbyUI,
        DotaEnterLobbyRun
    }
}
