using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Appccelerate.StateMachine;
using Appccelerate.StateMachine.Machine;
using KellermanSoftware.CompareNetObjects;
using log4net;
using Newtonsoft.Json;
using SteamKit2;
using SteamKit2.GC.Dota.Internal;
using WLBot.LobbyBot.Enums;
using WLBot.Utils;
using WLCommon.LobbyBot.Enums;
using WLCommon.Matches;

namespace WLBot.LobbyBot
{
    public class LobbyBot
    {
        #region Private Variables
        private ILog log;
        private SteamClient client;
        private MatchSetupDetails setupDetails;
        private SteamUser.LogOnDetails details;
        private DotaGCHandler dota;

        private ulong lobbyChannelId;
        private SteamFriends friends;
        public ActiveStateMachine<States, Events> fsm;

        protected bool isRunning = false;
        private CallbackManager manager;

        private bool dontRecreateLobby;

        private Thread procThread;
        private bool reconnect;
        private System.Timers.Timer reconnectTimer = new System.Timers.Timer(5000);
        private SteamUser user;
        private uint GCVersion;
        #endregion

        public delegate void LobbyUpdateHandler(CSODOTALobby lobby, ComparisonResult differences);

        public event LobbyUpdateHandler LobbyUpdate;

        private Dictionary<ulong, Action<CMsgDOTAMatch>> Callbacks = new Dictionary<ulong, Action<CMsgDOTAMatch>>(); 

        /// <summary>
        /// Setup a new bot with some details.
        /// </summary>
        /// <param name="details"></param>
        /// <param name="extensions">any extensions you want on the state machine.</param>
        public LobbyBot(MatchSetupDetails details, params IExtension<States, Events>[] extensions)
        {
            this.reconnect = true;
            this.setupDetails = details;
            this.details = new SteamUser.LogOnDetails()
            {
                Username = details.Bot.Username,
                Password = details.Bot.Password
            };
            this.log = LogManager.GetLogger("LobbyBot " + details.Bot.Username);
            log.Debug("Initializing a new LobbyBot, username: " + details.Bot.Username);
            reconnectTimer.Elapsed += (sender, args) =>
            {
                reconnectTimer.Stop();
                fsm.Fire(Events.AttemptReconnect);
            };
            fsm = new ActiveStateMachine<States, Events>();
            foreach (var ext in extensions) fsm.AddExtension(ext);
            fsm.DefineHierarchyOn(States.Connecting)
                .WithHistoryType(HistoryType.None);
            fsm.DefineHierarchyOn(States.Connected)
                .WithHistoryType(HistoryType.None)
                .WithInitialSubState(States.Dota);
            fsm.DefineHierarchyOn(States.Dota)
                .WithHistoryType(HistoryType.None)
                .WithInitialSubState(States.DotaConnect)
                .WithSubState(States.DotaMenu)
                .WithSubState(States.DotaLobby);
            fsm.DefineHierarchyOn(States.Disconnected)
                .WithHistoryType(HistoryType.None)
                .WithInitialSubState(States.DisconnectNoRetry)
                .WithSubState(States.DisconnectRetry);
            fsm.DefineHierarchyOn(States.DotaLobby)
               .WithHistoryType(HistoryType.None)
               .WithInitialSubState(States.DotaLobbyUI)
               .WithSubState(States.DotaLobbyPlay);
            fsm.In(States.Connecting)
                .ExecuteOnEntry(InitAndConnect)
                .On(Events.Connected).Goto(States.Connected)
                .On(Events.Disconnected).Goto(States.DisconnectRetry)
                .On(Events.LogonFailSteamGuard).Goto(States.DisconnectNoRetry) //.Execute(() => reconnect = false)
                .On(Events.LogonFailBadCreds).Goto(States.DisconnectNoRetry);
            fsm.In(States.Connected)
                .ExecuteOnExit(DisconnectAndCleanup)
                .On(Events.Disconnected).If(ShouldReconnect).Goto(States.Connecting)
                .Otherwise().Goto(States.Disconnected);
            fsm.In(States.Disconnected)
                .ExecuteOnEntry(DisconnectAndCleanup)
                .ExecuteOnExit(ClearReconnectTimer)
                .On(Events.AttemptReconnect).Goto(States.Connecting);
            fsm.In(States.DisconnectRetry)
                .ExecuteOnEntry(StartReconnectTimer);
            fsm.In(States.Dota)
                .ExecuteOnExit(DisconnectDota)
                .On(Events.DotaJoinedLobby).Goto(States.DotaLobby);
            fsm.In(States.DotaConnect)
                .ExecuteOnEntry(ConnectDota)
                .On(Events.DotaGCReady).Goto(States.DotaMenu);
            fsm.In(States.DotaMenu)
                 .ExecuteOnEntry(SetOnlinePresence)
                 .ExecuteOnEntry(CreateLobby);
            fsm.In(States.DotaLobby)
                .ExecuteOnEntry(EnterLobbyChat)
                .ExecuteOnEntry(EnterBroadcastChannel)
                .On(Events.DotaLeftLobby).Goto(States.DotaMenu).Execute(LeaveChatChannel);
            fsm.In(States.DotaLobbyUI);
            fsm.Initialize(States.Connecting);
        }

        public void CreateLobby()
        {
            if (dontRecreateLobby) return;
            dontRecreateLobby = true;
            leaveLobby();
            log.Debug("Setting up the lobby with passcode [" + setupDetails.Password + "]...");
            var ldetails = new CMsgPracticeLobbySetDetails
            {
                allchat = false,
                allow_cheats = false,
                allow_spectating = true,
                dota_tv_delay = LobbyDotaTVDelay.LobbyDotaTV_10,
                fill_with_bots = false,
                game_mode = (uint) (DOTA_GameMode) setupDetails.GameMode,
                game_name = "Subscriber Game",
                game_version = DOTAGameVersion.GAME_VERSION_CURRENT,
                server_region = 1
            };
            dota.CreateLobby(setupDetails.Password, ldetails);
        }

        public void Start()
        {
            fsm.Start();
        }

        private void RequestLobbyRefresh()
        {
            dota.RequestSubscriptionRefresh(3, dota.Lobby.lobby_id);
        }

        private void ClearReconnectTimer()
        {
            reconnectTimer.Stop();
        }

        private void DisconnectDota()
        {
            dota.CloseDota();
        }

        public void leaveLobby()
        {
            if (dota.Lobby != null)
            {
                this.dontRecreateLobby = true;
                log.Debug("Leaving lobby.");
            }
            dota.AbandonGame();
            dota.LeaveLobby();
            LeaveChatChannel();
        }

        private ulong MatchId;
        public void FetchMatchResult(ulong match_id, Action<CMsgDOTAMatch> callback)
        {
            MatchId = match_id;
            Callbacks[match_id] = callback;
            dota.RequestMatchResult(match_id);
        }

        private void LeaveChatChannel()
        {
            if (this.lobbyChannelId != 0)
            {
                dota.LeaveChatChannel(lobbyChannelId);
                this.lobbyChannelId = 0;
            }
        }

        private void EnterLobbyChat()
        {
            dota.JoinChatChannel("Lobby_" + dota.Lobby.lobby_id, DOTAChatChannelType_t.DOTAChannelType_Lobby);
        }

        private void EnterBroadcastChannel()
        {
            //dota.JoinBroadcastChannel();
            dota.JoinCoachSlot();
        }

        private void SwitchTeam(DOTA_GC_TEAM team = DOTA_GC_TEAM.DOTA_GC_TEAM_GOOD_GUYS)
        {
            dota.JoinTeam(team, 2);
        }

        private void StartReconnectTimer()
        {
            reconnectTimer.Start();
        }

        private static void SteamThread(object state)
        {
            LobbyBot bot = state as LobbyBot;
            if (bot == null) return;
            while (bot.isRunning && bot.manager != null)
            {
                bot.manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
        }

        private bool ShouldReconnect()
        {
            return isRunning && reconnect;
        }

        private void SetOnlinePresence()
        {
            friends.SetPersonaState(EPersonaState.Online);
            friends.SetPersonaName("SubGames Bot");
        }

        private void InitAndConnect()
        {
            if (client == null)
            {
                client = new SteamClient();
                user = client.GetHandler<SteamUser>();
                friends = client.GetHandler<SteamFriends>();
                dota = client.GetHandler<DotaGCHandler>();
                manager = new CallbackManager(client);
                isRunning = true;
                new Callback<SteamClient.ConnectedCallback>(c =>
                {
                    if (c.Result != EResult.OK)
                    {
                        fsm.FirePriority(Events.Disconnected);
                        isRunning = false;
                        return;
                    }

                    user.LogOn(details);
                }, manager);
                new Callback<SteamClient.DisconnectedCallback>(c => fsm.Fire(Events.Disconnected), manager);
                new Callback<SteamUser.LoggedOnCallback>(c =>
                {
                    if (c.Result != EResult.OK)
                    {
                        if (c.Result == EResult.AccountLogonDenied)
                        {
                            fsm.Fire(Events.LogonFailSteamGuard);
                            return;
                        }
                        fsm.Fire(Events.LogonFailBadCreds);
                    }
                    else
                    {
                        fsm.Fire(Events.Connected);
                    }
                }, manager);
                new Callback<DotaGCHandler.MatchResultResponse>(c =>
                {
                    Action<CMsgDOTAMatch> cb;
                    ulong id;
                    id = c.result.match != null ? c.result.match.match_id : MatchId;
                    if (!Callbacks.TryGetValue(id, out cb)) return;
                    Callbacks.Remove(id);
                    cb(c.result.match);
                }, manager);
                new Callback<DotaGCHandler.GCWelcomeCallback>(c =>
                {
                    log.Debug("GC welcome, version " + c.Version);
                    this.GCVersion = c.Version;
                    fsm.Fire(Events.DotaGCReady);
                }, manager);
                new Callback<DotaGCHandler.UnhandledDotaGCCallback>(
                    c => log.Debug("Unknown GC message: " + c.Message.MsgType), manager);
                new Callback<SteamFriends.FriendsListCallback>(c => log.Debug(c.FriendList), manager);
                new Callback<DotaGCHandler.PracticeLobbySnapshot>(c =>
                {
                    log.DebugFormat("Lobby snapshot received with state: {0}", c.lobby.state);
                    log.Debug(JsonConvert.SerializeObject(c.lobby));
                    fsm.Fire(Events.DotaJoinedLobby);
                }, manager);
                new Callback<DotaGCHandler.PingRequest>(c =>
                {
                    log.Debug("GC Sent a ping request. Sending pong!");
                    dota.Pong();
                }, manager);
                new Callback<DotaGCHandler.JoinChatChannelResponse>(
                    c => log.Debug("Joined chat " + c.result.channel_name), manager);
                new Callback<DotaGCHandler.ChatMessage>(
                    c => log.DebugFormat("{0} => {1}: {2}", c.result.channel_id, c.result.persona_name, c.result.text),
                    manager);
                new Callback<DotaGCHandler.OtherJoinedChannel>(
                    c =>
                        log.DebugFormat("User with name {0} joined channel {1}.", c.result.persona_name,
                            c.result.channel_id), manager);
                new Callback<DotaGCHandler.OtherLeftChannel>(
                    c =>
                        log.DebugFormat("User with steamid {0} left channel {1}.", c.result.steam_id,
                            c.result.channel_id), manager);
                new Callback<DotaGCHandler.CacheUnsubscribed>(c =>
                {
                    log.Debug("Bot has left/been kicked from the lobby.");
                    fsm.Fire(Events.DotaLeftLobby);
                }, manager);
                new Callback<DotaGCHandler.Popup>(c =>
                {
                    log.DebugFormat("Received message (popup) from GC: {0}", c.result.id);
                    if (c.result.id == CMsgDOTAPopup.PopupID.KICKED_FROM_LOBBY)
                    {
                        log.Debug("Kicked from the lobby!");
                    }
                }, manager);
                //new Callback<DotaGCHandler.LiveLeagueGameUpdate>(c => log.DebugFormat("Tournament games: {0}", c.result.live_league_games), manager);
                new Callback<DotaGCHandler.PracticeLobbyUpdate>(c =>
                {
                    var diffs = Diff.Compare(c.oldLobby, c.lobby);
                    var dstrings = new List<string>(diffs.Differences.Count);
                    dstrings.AddRange(
                        diffs.Differences.Select(
                            diff =>
                                string.Format("{0}: {1} => {2}", diff.PropertyName, diff.Object1Value, diff.Object2Value)));
                    if (dstrings.Count > 0)
                    {
                        var msg = "Update: " + string.Join(", ", dstrings);
                        log.Debug(msg);
                        if (LobbyUpdate != null) LobbyUpdate(dota.Lobby, diffs);
                    }
                }, manager);
            }
            client.Connect();
            procThread = new Thread(SteamThread);
            procThread.Start(this);
        }

        private void ConnectDota()
        {
            log.Debug("Attempting to connect to Dota...");
            dota.LaunchDota();
        }

        public void DisconnectAndCleanup()
        {
            isRunning = false;
            if (client != null)
            {
                if (user != null)
                {
                    if(dota != null) dota.LeaveLobby();
                    user.LogOff();
                    user = null;
                }
                if (client.IsConnected) client.Disconnect();
                client.ClearHandlers();
                client = null;
            }
        }

        public void Destroy()
        {
            manager.Unregister();
            manager = null;
            if (fsm != null)
            {
                fsm.Stop();
                fsm.ClearExtensions();
                fsm = null;
            }
            reconnect = false;
            DisconnectAndCleanup();
            user = null;
            client = null;
            friends = null;
            dota = null;
            manager = null;
            log.Debug("Bot destroyed due to remote command.");
        }

        public void StartGameAndLeave()
        {
            dota.LaunchLobby();
            dota.AbandonGame();
            dota.LeaveLobby();
        }

        public void StartGame()
        {
            dota.LaunchLobby();
        }
    }
}
