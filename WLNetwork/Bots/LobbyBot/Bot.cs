using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dota2.Base.Data;
using Dota2.GC;
using Dota2.GC.Dota.Internal;
using Dota2.GC.Internal;
using log4net;
using Stateless;
using SteamKit2;
using WLNetwork.Bots.LobbyBot.Enums;
using WLNetwork.Utils;
using Timer = System.Timers.Timer;

#if USE_GAME_ENGINE
using Dota2.Engine;
using Dota2.Engine.Control;
using Dota2.Engine.Session.State.Enums;
#endif

namespace WLNetwork.Bots.LobbyBot
{
    /// <summary>
    ///     An instance of a DOTA 2 lobby/game bot
    /// </summary>
    public class Bot
    {
        #region Clients

        /// <summary>
        ///     Steam client
        /// </summary>
        public SteamClient SteamClient { get; private set; }

        /// <summary>
        ///     Steam users handler
        /// </summary>
        public SteamUser SteamUser { get; private set; }

        /// <summary>
        ///     Steam friends
        /// </summary>
        public SteamFriends SteamFriends { get; private set; }

        /// <summary>
        ///     Dota2 game coordinator
        /// </summary>
        public DotaGCHandler DotaGCHandler { get; private set; }

        /// <summary>
        ///     Which engine to use?
        /// </summary>
        public ESourceEngine Engine { get; set; }

        /// <summary>
        ///     The lobby before the current update
        /// </summary>
        public CSODOTALobby Lobby { get; private set; }

#if USE_GAME_ENGINE
    /// <summary>
    /// DOTA 2 game client
    /// </summary>
        public DotaGameClient GameClient { get; private set; }
#endif

        /// <summary>
        ///     State of the bot
        /// </summary>
        public State State => _state.State;

        /// <summary>
        ///     Called when state transitions
        /// </summary>
        public event EventHandler<StateMachine<State, Trigger>.Transition> StateTransitioned;

        /// <summary>
        ///     Called when the lobby is updated.
        /// </summary>
        public event EventHandler<CSODOTALobby> LobbyUpdate;

        /// <summary>
        ///     Called when invalid credentials
        /// </summary>
        public event EventHandler InvalidCreds;

        #endregion

        #region Private

        private readonly SteamUser.LogOnDetails _logonDetails;
        private readonly ILog log;
        private StateMachine<State, Trigger> _state;
        private readonly Timer _reconnectTimer;
        private CallbackManager _cbManager;
        private readonly bool _shouldReconnect = true;
        private int _cbThreadCtr;
        private Thread _procThread;
        private int _connAttempts;
        private const int maxAttempts = 3;
        private ulong lobbyChannelId;
        private string _lobbyPassword;
#if USE_GAME_ENGINE
        private IDotaGameController[] _controllers;
#endif

        private readonly Dictionary<ulong, Action<CMsgDOTAMatch>> Callbacks =
            new Dictionary<ulong, Action<CMsgDOTAMatch>>();

        private ulong MatchId;

        private bool _isRunning;

        #endregion

        #region Constructor

        /// <summary>
        ///     Create a new game bot
        /// </summary>
        /// <param name="details">Auth info</param>
        /// <param name="reconnectDelay">
        ///     Delay between reconnection attempts to steam, set to a negative value to disable
        ///     reconnecting
        /// </param>
        /// <param name="contrs">Game controllers</param>
        public Bot(SteamUser.LogOnDetails details, double reconnectDelay = 2000
#if USE_GAME_ENGINE
            ,params IDotaGameController[] contrs
#endif
            )
        {
#if USE_GAME_ENGINE
            _controllers = contrs;
#endif

            log = LogManager.GetLogger("Bot " + details.Username);
            log.Debug("Initializing a new LobbyBot w/username " + details.Username);

            _logonDetails = details;

            if (reconnectDelay < 0)
            {
                reconnectDelay = 10;
                _shouldReconnect = false;
            }

            _reconnectTimer = new Timer(reconnectDelay);
            _reconnectTimer.Elapsed += (sender, args) => _state.Fire(Trigger.ConnectRequested);

            _state = new StateMachine<State, Trigger>(State.SignedOff);
            _state.OnTransitioned((transition =>
            {
                log.DebugFormat("{0} => {1}", transition.Source.ToString("G"), transition.Destination.ToString("G"));
                StateTransitioned?.Invoke(this, transition);
            }));

            _state.Configure(State.Conceived)
                .Permit(Trigger.ShutdownRequested, State.SignedOff);

            _state.Configure(State.SignedOff)
                .SubstateOf(State.Conceived)
                .Ignore(Trigger.SteamDisconnected)
                .OnEntryFrom(Trigger.SteamInvalidCreds, () => InvalidCreds?.Invoke(this, EventArgs.Empty))
                .PermitIf(Trigger.ConnectRequested, State.Steam, () => _isRunning);

            _state.Configure(State.RetryConnection)
                .SubstateOf(State.SignedOff)
                .OnExit(() => _reconnectTimer.Stop())
                .OnEntry(() => _reconnectTimer.Start())
                .Permit(Trigger.ConnectRequested, State.Steam);

            _state.Configure(State.Steam)
                .SubstateOf(State.Conceived)
                .Permit(Trigger.SteamConnected, State.Dota)
                .PermitDynamic(Trigger.SteamDisconnected,
                    () => _shouldReconnect ? State.RetryConnection : State.SignedOff)
                .Permit(Trigger.SteamInvalidCreds, State.SignedOff)
                .OnEntry(StartSteamConnection)
                .OnExit(ReleaseSteamConnection);

            _state.Configure(State.Dota)
                .SubstateOf(State.Steam)
                .Permit(Trigger.DotaConnected, State.DotaMenu)
                .PermitReentry(Trigger.DotaDisconnected)
                .Permit(Trigger.DotaEnteredLobbyUI, State.DotaLobby)
                .Permit(Trigger.DotaEnteredLobbyPlay, State.DotaPlay)
                .OnEntryFrom(Trigger.SteamConnected, StartDotaGCConnection);

            _state.Configure(State.DotaMenu)
                .SubstateOf(State.Dota)
                .Permit(Trigger.DotaEnteredLobbyUI, State.DotaLobby)
                .Permit(Trigger.DotaEnteredLobbyPlay, State.DotaPlay);

            _state.Configure(State.DotaLobby)
                .SubstateOf(State.Dota)
                .Ignore(Trigger.DotaEnteredLobbyUI)
                .Permit(Trigger.DotaEnteredLobbyPlay, State.DotaPlay)
                .Permit(Trigger.DotaNoLobby, State.DotaMenu)
                .OnEntry(JoinLobbySlot)
                .OnEntry(JoinLobbyChat)
                .OnExit(LeaveLobbyChat);

            _state.Configure(State.DotaPlay)
                .SubstateOf(State.Dota)
                .Ignore(Trigger.DotaEnteredLobbyPlay)
                .Permit(Trigger.DotaEnteredLobbyUI, State.DotaLobby)
                .Permit(Trigger.DotaNoLobby, State.DotaMenu)
                .OnEntry(() => _connAttempts = 0)
#if USE_GAME_ENGINE
#if ENABLE_GAME_CONNECTION
                .OnEntry(() =>
                {
                    //Brief delay before starting connection
                    Task.Run(() =>
                    {
                        Thread.Sleep(500);
                        if(_state.State == State.DotaPlay) StartDotaGameConnection();
                    });
                })
#endif
                .OnExit(ReleaseDotaGameConnection)
#endif
                ;
        }

        #endregion

        #region Bot Specific Implementation

        /// <summary>
        ///     Join the correct slot
        /// </summary>
        private void JoinLobbySlot()
        {
            //DotaGCHandler.JoinTeam(DOTA_GC_TEAM.DOTA_GC_TEAM_GOOD_GUYS);
            // DotaGCHandler.JoinBroadcastChannel();
        }

        /// <summary>
        ///     Send a lobby chat message
        /// </summary>
        /// <param name="msg"></param>
        public void SendLobbyMessage(string msg)
        {
            if (lobbyChannelId == 0) return;
            DotaGCHandler.SendChannelMessage(lobbyChannelId, msg);
        }

        /// <summary>
        ///     Join the lobby chat channel
        /// </summary>
        private void JoinLobbyChat()
        {
            if (DotaGCHandler.Lobby == null)
            {
                log.Warn("JoinLobbyChat called with no lobby!");
                return;
            }

            DotaGCHandler.JoinChatChannel("Lobby_" + DotaGCHandler.Lobby.lobby_id,
                DOTAChatChannelType_t.DOTAChannelType_Lobby);
        }

        /// <summary>
        ///     Leave a lobby chat channel
        /// </summary>
        private void LeaveLobbyChat()
        {
            if (lobbyChannelId == 0) return;
            DotaGCHandler.LeaveChatChannel(lobbyChannelId);
            lobbyChannelId = 0;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        ///     Start connecting to Steam
        /// </summary>
        private void StartSteamConnection()
        {
            ReleaseSteamConnection();

            var c = SteamClient = new SteamClient();

            DotaGCHandler.Bootstrap(c, Games.DOTA2, Engine);

            SteamUser = c.GetHandler<SteamUser>();
            SteamFriends = c.GetHandler<SteamFriends>();

            var cb = _cbManager = new CallbackManager(c);

            SetupSteamCallbacks(cb);
            SetupDotaGCCallbacks(cb);
#if USE_GAME_ENGINE
            SetupDotaClientCallbacks(cb);
#endif

            c.Connect();
            _cbThreadCtr++;
            _procThread = new Thread(SteamThread);
            _procThread.Start(this);
        }

        /// <summary>
        ///     Make sure every client is shutdown completely
        /// </summary>
        private void ReleaseSteamConnection()
        {
            ReleaseDotaGCConnection();

            SteamFriends?.SetPersonaState(EPersonaState.Offline);
            SteamFriends = null;

            SteamUser?.LogOff();
            SteamUser = null;

            SteamClient?.Disconnect();
            SteamClient = null;

            _cbThreadCtr++;
        }

        /// <summary>
        ///     Start connecting to the DOTA 2 game coordinator
        /// </summary>
        private void StartDotaGCConnection()
        {
            DotaGCHandler = SteamClient.GetHandler<DotaGCHandler>();
            DotaGCHandler.Start();
#if USE_GAME_ENGINE
            var cli = GameClient = new DotaGameClient(DotaGCHandler, _cbManager);
            foreach (var cont in _controllers) cli.RegisterController(cont);
#endif
        }

        /// <summary>
        ///     Completely disconnect from the DOTA gc
        /// </summary>
        private void ReleaseDotaGCConnection()
        {
#if USE_GAME_ENGINE
            ReleaseDotaGameConnection();

            GameClient?.Disconnect();
            GameClient?.Dispose();
            GameClient = null;
#endif

            DotaGCHandler?.Stop();
            DotaGCHandler = null;
        }

#if USE_GAME_ENGINE
    /// <summary>
    /// Start connecting to the DOTA 2 game server
    /// </summary>
        private void StartDotaGameConnection()
        {
            ReleaseDotaGameConnection();

            if (DotaGCHandler.Lobby.connect.Length == 0 || DotaGCHandler.Lobby.connect.StartsWith("="))
            {
                log.Warn("Connect address is Datagram routed or empty [" + DotaGCHandler.Lobby.connect + "] - not connecting to game server.");
                return;
            }
            _connAttempts++;
            GameClient.Connect();
        }

        /// <summary>
        /// Completely disconnect from the DOTA 2 game server
        /// </summary>
        private void ReleaseDotaGameConnection()
        {
            GameClient?.Disconnect();
        }
#endif

        private void UpdatePersona()
        {
            var cname = SteamFriends.GetPersonaName();
            var tname = "WebLeagueBot";
            if (cname != tname)
            {
                log.DebugFormat("Changed persona name to {0} from {1}.", tname, cname);
                SteamFriends.SetPersonaName(tname);
            }
            SteamFriends.SetPersonaState(EPersonaState.Online);
        }

        /// <summary>
        ///     Internal thread
        /// </summary>
        /// <param name="state"></param>
        private static void SteamThread(object state)
        {
            Bot bot = state as Bot;
            int tid = bot._cbThreadCtr;
            var ts = TimeSpan.FromSeconds(1);
            while (tid == bot._cbThreadCtr)
            {
                try
                {
                    bot._cbManager.RunWaitCallbacks(ts);
                }
                catch (Exception ex)
                {
                    bot.log.Error("Error in Steam thread!", ex);
                }
            }
        }

        #endregion

        #region Callbacks

        /// <summary>
        ///     Setup steam client callbacks
        /// </summary>
        /// <param name="cb"></param>
        private void SetupSteamCallbacks(CallbackManager cb)
        {
            // Handle general connection stuff
            cb.Add<SteamUser.AccountInfoCallback>(a =>
            {
                log.DebugFormat("Current name is: {0}, flags {1}, ", a.PersonaName, a.AccountFlags.ToString("G"));
                UpdatePersona();
            });
            cb.Add<SteamClient.ConnectedCallback>(a => SteamUser?.LogOn(_logonDetails));
            cb.Add<SteamClient.DisconnectedCallback>(a => _state?.Fire(Trigger.SteamDisconnected));
            cb.Add<SteamUser.LoggedOnCallback>(a =>
            {
                log.DebugFormat("Steam signin result: {0}", a.Result.ToString("G"));
                switch (a.Result)
                {
                    case EResult.OK:
                        _state?.Fire(Trigger.SteamConnected);
                        break;

                    case EResult.ServiceUnavailable:
                    case EResult.ServiceReadOnly:
                    case EResult.TryAnotherCM:
                    case EResult.AccountLoginDeniedThrottle:
                    case EResult.AlreadyLoggedInElsewhere:
                    case EResult.BadResponse:
                    case EResult.Busy:
                    case EResult.ConnectFailed:
                        _state?.Fire(Trigger.SteamDisconnected); //retry state
                        break;
                    default:
                        _state?.Fire(Trigger.SteamInvalidCreds);
                        break;
                }
            });
        }

        /// <summary>
        ///     Setup DOTA 2 game coordinator callbacks
        /// </summary>
        /// <param name="cb">Manager</param>
        private void SetupDotaGCCallbacks(CallbackManager cb)
        {
            cb.Add<DotaGCHandler.GCWelcomeCallback>(a =>
            {
                log.Debug("GC session welcomed");
                _state.Fire(Trigger.DotaConnected);
            });
            cb.Add<DotaGCHandler.ConnectionStatus>(a =>
            {
                log.DebugFormat("GC connection status: {0}", a.result.status.ToString("G"));
                _state.Fire(a.result.status == GCConnectionStatus.GCConnectionStatus_HAVE_SESSION
                    ? Trigger.DotaConnected
                    : Trigger.DotaDisconnected);
            });
            cb.Add<DotaGCHandler.Popup>(a => log.DebugFormat("GC popup message: {0}", a.result.id.ToString("G")));
            cb.Add<DotaGCHandler.PracticeLobbySnapshot>(a => HandleLobbyUpdate(a.lobby));
            cb.Add<DotaGCHandler.PracticeLobbyLeave>(a => HandleLobbyUpdate(null));
            cb.Add<DotaGCHandler.PracticeLobbyUpdate>(a => HandleLobbyUpdate(a.lobby));
            cb.Add<DotaGCHandler.JoinChatChannelResponse>(a =>
            {
                if (DotaGCHandler.Lobby != null && a.result.channel_id != 0 &&
                    a.result.channel_name == "Lobby_" + DotaGCHandler.Lobby.lobby_id)
                    lobbyChannelId = a.result.channel_id;
            });
            cb.Add<DotaGCHandler.ChatMessage>(
                a =>
                {
                    log.DebugFormat("[Chat][" +
                                    (a.result.channel_id == lobbyChannelId ? "Lobby" : a.result.channel_id + "") + "] " +
                                    a.result.persona_name + ": " + a.result.text);
                    if (a.result.channel_id != lobbyChannelId) return;
                    if (a.result.text.Contains("!start")) DotaGCHandler.LaunchLobby();
                });
            cb.Add<DotaGCHandler.MatchResultResponse>(c =>
            {
                Action<CMsgDOTAMatch> cbx;
                ulong id;
                id = c.result.match != null ? c.result.match.match_id : MatchId;
                if (!Callbacks.TryGetValue(id, out cbx)) return;
                Callbacks.Remove(id);
                cbx(c.result.match);
            });
        }

#if USE_GAME_ENGINE
    /// <summary>
    /// Setup the DOTA client callbacks
    /// </summary>
    /// <param name="cb"></param>
        private void SetupDotaClientCallbacks(CallbackManager cb)
        {
            cb.Add<DotaGameClient.HandshakeRejected>(rej =>
            {
                if (GameClient == null) return;
                log.ErrorFormat("Connection to the game rejected with reason {0}. Attempts {1}/{2}.", rej.reason, _connAttempts, maxAttempts);
            });
            cb.Add<DotaGameClient.SessionStateTransition>(tra =>
            {
                if (GameClient == null) return;
                log.Debug("[GameClient] " + tra.OldStatus.ToString("G") + " => " + tra.NewStatus.ToString("G"));

                if (tra.NewStatus == States.PLAY)
                {
                    _connAttempts = 0;
                }

                if (tra.NewStatus != States.DISCONNECTED || _state.State != State.DotaPlay) return;
                log.WarnFormat("Client has disconnected, attempts {0}/{1}.{2}", _connAttempts, maxAttempts, _connAttempts < maxAttempts ? " Retrying." : " Not retrying.");
                if (_connAttempts < maxAttempts)
                    StartDotaGameConnection();
            });
            cb.Add<DotaGameClient.LogMessage>(msg =>
            {
                if (GameClient == null) return;
                log.Debug("[GameClient] " + msg.message);
            });
        }
#endif

        private void HandleLobbyUpdate(CSODOTALobby lobby)
        {
            if (Lobby == null && lobby != null)
            {
                log.DebugFormat("Entered lobby {0} with state {1}.", lobby.lobby_id, lobby.state.ToString("G"));
            }
            else if (Lobby != null && lobby == null)
            {
                log.DebugFormat("Exited lobby {0}.", Lobby.lobby_id);
            }

            if (lobby != null)
                _state.Fire(lobby.state == CSODOTALobby.State.UI || string.IsNullOrEmpty(lobby.connect)
                    ? Trigger.DotaEnteredLobbyUI
                    : Trigger.DotaEnteredLobbyPlay);
            else
                _state.Fire(Trigger.DotaNoLobby);

            LobbyUpdate?.Invoke(Lobby, lobby);
            Lobby = lobby;
        }

        #endregion

        #region Public Methods

        /// <summary>
        ///     Start the bot
        /// </summary>
        public void Start()
        {
            _isRunning = true;
            _state.Fire(Trigger.ConnectRequested);
        }

        /// <summary>
        ///     Shutdown the bot completely
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _state.Fire(Trigger.ShutdownRequested);
        }

        public void Dispose()
        {
            Stop();
            ReleaseSteamConnection();
            _state = null;
        }

        public void FetchMatchResult(ulong match_id, Action<CMsgDOTAMatch> callback)
        {
            // Set timeout
            Task.Run(() =>
            {
                Thread.Sleep(5000);
                if (!Callbacks.ContainsKey(match_id)) return;
                log.Debug("Match result fetch for " + match_id + " timed out!");
                Callbacks[match_id](null);
                Callbacks.Remove(match_id);
            });

            MatchId = match_id;
            Callbacks[match_id] = callback;
            DotaGCHandler.RequestMatchResult(match_id);
        }

        public void LeaveLobby()
        {
            DotaGCHandler.AbandonGame();
            DotaGCHandler.LeaveLobby();
        }

        #endregion
    }
}