﻿namespace WLCommon.BotEnums
{
    public enum States
    {
        Connecting,
        Disconnected,
        Connected,
        DisconnectNoRetry,
        DisconnectRetry,

        #region DOTA

        Dota,
        DotaConnect,
        DotaMenu,

        #region DOTALOBBY

        DotaLobby,
        DotaLobbyUI,
        DotaLobbyPlay

        #endregion

        #endregion
    }
}