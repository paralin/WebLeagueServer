using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Appccelerate.StateMachine;
using Appccelerate.StateMachine.Machine;
using JWT;
using KellermanSoftware.CompareNetObjects;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SteamKit2.GC.Dota.Internal;
using WLBot.LobbyBot.Enums;
using WLBot.Utils;
using WLCommon.Arguments;
using WLCommon.LobbyBot.Enums;
using WLCommon.Matches;
using WLCommon.Matches.Enums;
using WLCommon.Model;
using XSockets.Client40;
using XSockets.Client40.Common.Interfaces;

namespace WLBot.Client
{
    /// <summary>
    /// Client to communicate with the network server. Also manages bots.
    /// </summary>
    public class WLBotClient
    {
        private static readonly log4net.ILog log =
log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private XSocketClient client;
        public IController controller;
        private bool shouldReconnect = false;
        private bool running = false;
        private Timer reconnectTimer;


        private Dictionary<MatchSetupDetails, LobbyBot.LobbyBot> Bots = new Dictionary<MatchSetupDetails, LobbyBot.LobbyBot>(); 
        /// <summary>
        /// Create a new client with an XSocket URI.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="botid">ID of the bot in the database</param>
        /// <param name="secret">Corresponding secret of the bot in the db</param>
        public WLBotClient(string url, string botid, string secret)
        {
            reconnectTimer = new Timer(5000);
            reconnectTimer.Elapsed += (sender, args) => Start();
            client = new XSocketClient(url, "http://localhost", "dotabot");
            controller = client.Controller("dotabot");
            client.QueryString["bid"] = botid;
            Guid data = Guid.NewGuid();
            client.QueryString["bdata"] = data.ToString();
            client.QueryString["btoken"] =
                JWT.JsonWebToken.Encode(new BotToken() {Id = botid, SecretData = data.ToString()}, secret,
                    JwtHashAlgorithm.HS256);
            InitClient();
        }

        /// <summary>
        /// Connect and also autoreconnect
        /// </summary>
        public void Start()
        {
            running = true;
            reconnectTimer.Stop();
            try
            {
                if (shouldReconnect) client.Reconnect();
                else client.Open();
            }
            catch (Exception ex)
            {
                log.Error("Issue connecting, will try again.", ex);
                reconnectTimer.Start();
            }
            finally
            {
                shouldReconnect = true;
            }
        }

        /// <summary>
        /// Disconnect and stop everything
        /// </summary>
        public void Stop()
        {
            running = false;
            client.Disconnect();
        }

        private void InitClient()
        {
            controller.OnOpen += async (sender, args) =>
            {
                log.Debug("Connected to master.");
                var status = await controller.Invoke<bool>("readyup");
                if (status)
                {
                    log.Info("Authenticated and ready.");
                }
                else
                {
                    log.Error("Some issue authenticating. the host does not recognize this bot host.");
                }
            };
            controller.On("startsetup", (MatchSetupDetails details) =>
            {
                var bot = new LobbyBot.LobbyBot(details, new WLBotExtension(details, this));
                Bots.Add(details, bot);
                bot.LobbyUpdate += delegate(CSODOTALobby lobby, ComparisonResult differences)
                {
                    if (lobby == null)
                    {
                        controller.Invoke("lobbyclear", new LobbyClearArgs(){Id=details.Id});
                        return;
                    }
                    if (lobby.state == CSODOTALobby.State.UI)
                    {
                        PlayerReadyArgs args = new PlayerReadyArgs();
                        var members =
                            lobby.members.Where(
                                m =>
                                    m.team == DOTA_GC_TEAM.DOTA_GC_TEAM_BAD_GUYS ||
                                    m.team == DOTA_GC_TEAM.DOTA_GC_TEAM_GOOD_GUYS);
                        var players = new List<PlayerReadyArgs.Player>();
                        int i = 0;
                        foreach (var member in members)
                        {
                            var plyr = details.Players.FirstOrDefault(m => m.SID == member.id + "");
                            if (plyr != null)
                                players.Add(new PlayerReadyArgs.Player
                                {
                                    IsReady =
                                        (member.team == DOTA_GC_TEAM.DOTA_GC_TEAM_BAD_GUYS &&
                                         plyr.Team == MatchTeam.Dire) ||
                                        (member.team == DOTA_GC_TEAM.DOTA_GC_TEAM_GOOD_GUYS &&
                                         plyr.Team == MatchTeam.Radiant),
                                    SteamID = plyr.SID
                                });
                            i++;
                        }
                        args.Players = players.ToArray();
                        args.Id = details.Id;
                        controller.Invoke("playerready", args);
                    }
                    else if (lobby.state == CSODOTALobby.State.RUN &&
                             lobby.game_state > DOTA_GameState.DOTA_GAMERULES_STATE_WAIT_FOR_PLAYERS_TO_LOAD &&
                             lobby.game_state < DOTA_GameState.DOTA_GAMERULES_STATE_POST_GAME)
                    {
                        if (differences.Differences.Any(m => m.Object1TypeName == "DOTALeaverStatus_t"))
                        {
                            LeaverStatusArgs args = new LeaverStatusArgs();
                            args.Players = lobby.members.Select(m=>new LeaverStatusArgs.Player(){Status = m.leaver_status, SteamID = ""+m.id}).ToArray();
                            args.Id = details.Id;
                            controller.Invoke("leaverstatus", args);
                        }
                    }
                    else if (lobby.game_state == DOTA_GameState.DOTA_GAMERULES_STATE_POST_GAME)
                    {
                        log.Debug(JObject.FromObject(lobby).ToString(Formatting.Indented));
                    }
                    controller.Invoke("matchstatus", new MatchStateArgs(){Id = details.Id, State = lobby.game_state, Status = lobby.state});
                    if (differences.Differences.Any(m => m.PropertyName == ".match_id"))
                    {
                        controller.Invoke("matchid", new MatchIdArgs(){Id = details.Id, match_id = lobby.match_id});
                    }
                    if (differences.Differences.Any(m => m.PropertyName == ".match_outcome") && lobby.match_outcome != EMatchOutcome.k_EMatchOutcome_Unknown)
                    {
                        controller.Invoke("matchoutcome", new MatchOutcomeArgs() { Id = details.Id, match_outcome = lobby.match_outcome });
                    }
                };
                bot.Start();
            });
            controller.On("clearsetup", (Guid id) =>
            {
                var ldet = Bots.Keys.FirstOrDefault(m => m.Id == id);
                if (ldet != null)
                {
                    var bot = Bots[ldet];
                    bot.leaveLobby();
                    bot.Destroy();
                    Bots.Remove(ldet);
                }
            });
            controller.On("leavelobby", (Guid id) =>
            {
                var ldet = Bots.Keys.FirstOrDefault(m => m.Id == id);
                if (ldet != null)
                {
                    var bot = Bots[ldet];
                    bot.leaveLobby();
                }
            });
            controller.On("fetchmatchresult", (FetchMatchResultArgs args) =>
            {
                var ldet = Bots.Keys.FirstOrDefault(m => m.Id == args.Id);
                if (ldet != null)
                {
                    var bot = Bots[ldet];
                    bot.FetchMatchResult(args.MatchId, match =>
                    {
                        args.Match = match;
                        controller.Invoke("matchresult", args);
                    });
                }
            });
            controller.On("finalize", (Guid id) =>
            {
                var ldet = Bots.Keys.FirstOrDefault(m => m.Id == id);
                if (ldet != null)
                {
                    var bot = Bots[ldet];
                    bot.StartGame();
                }
            });
            controller.OnClose += (sender, args) =>
            {
                log.Debug("Disconnected, clearing all bots.");
                foreach (var bv in Bots.ToArray())
                {
                    bv.Value.Destroy();
                }
                Bots.Clear();
            };
        }
    }

    public class WLBotExtension : IExtension<States, Events>
    {
        private ILog log;
        private MatchSetupDetails details;
        private WLBotClient client;

        public WLBotExtension(MatchSetupDetails details, WLBotClient client)
        {
            log = LogManager.GetLogger("LobbyBotE " + details.Bot.Username);
            this.details = details;
            this.client = client;
        }

        public void StartedStateMachine(IStateMachineInformation<States, Events> stateMachine)
        {
            
        }

        public void StoppedStateMachine(IStateMachineInformation<States, Events> stateMachine)
        {
            
        }

        public void EventQueued(IStateMachineInformation<States, Events> stateMachine, Events eventId, object eventArgument)
        {
            
        }

        public void EventQueuedWithPriority(IStateMachineInformation<States, Events> stateMachine, Events eventId, object eventArgument)
        {
            
        }

        public void SwitchedState(IStateMachineInformation<States, Events> stateMachine, IState<States, Events> oldState, IState<States, Events> newState)
        {
            log.Debug("Switched state to "+newState.Id);
            if (client.controller != null)
            {
                client.controller.Invoke("stateupdate", new StateUpdateArgs{State=newState.Id, Id=details.Id});
            }
        }

        public void InitializingStateMachine(IStateMachineInformation<States, Events> stateMachine, ref States initialState)
        {
            
        }

        public void InitializedStateMachine(IStateMachineInformation<States, Events> stateMachine, States initialState)
        {
            
        }

        public void EnteringInitialState(IStateMachineInformation<States, Events> stateMachine, States state)
        {
            
        }

        public void EnteredInitialState(IStateMachineInformation<States, Events> stateMachine, States state, ITransitionContext<States, Events> context)
        {
            
        }

        public void FiringEvent(IStateMachineInformation<States, Events> stateMachine, ref Events eventId, ref object eventArgument)
        {
            
        }

        public void FiredEvent(IStateMachineInformation<States, Events> stateMachine, ITransitionContext<States, Events> context)
        {
            
        }

        public void HandlingEntryActionException(IStateMachineInformation<States, Events> stateMachine, IState<States, Events> state, ITransitionContext<States, Events> context,
            ref Exception exception)
        {
            
        }

        public void HandledEntryActionException(IStateMachineInformation<States, Events> stateMachine, IState<States, Events> state, ITransitionContext<States, Events> context,
            Exception exception)
        {
            
        }

        public void HandlingExitActionException(IStateMachineInformation<States, Events> stateMachine, IState<States, Events> state, ITransitionContext<States, Events> context,
            ref Exception exception)
        {
            
        }

        public void HandledExitActionException(IStateMachineInformation<States, Events> stateMachine, IState<States, Events> state, ITransitionContext<States, Events> context,
            Exception exception)
        {
           
        }

        public void HandlingGuardException(IStateMachineInformation<States, Events> stateMachine, ITransition<States, Events> transition,
            ITransitionContext<States, Events> transitionContext, ref Exception exception)
        {
            
        }

        public void HandledGuardException(IStateMachineInformation<States, Events> stateMachine, ITransition<States, Events> transition,
            ITransitionContext<States, Events> transitionContext, Exception exception)
        {
            
        }

        public void HandlingTransitionException(IStateMachineInformation<States, Events> stateMachine, ITransition<States, Events> transition,
            ITransitionContext<States, Events> context, ref Exception exception)
        {
            
        }

        public void HandledTransitionException(IStateMachineInformation<States, Events> stateMachine, ITransition<States, Events> transition,
            ITransitionContext<States, Events> transitionContext, Exception exception)
        {
            
        }

        public void SkippedTransition(IStateMachineInformation<States, Events> stateMachineInformation, ITransition<States, Events> transition,
            ITransitionContext<States, Events> context)
        {
            
        }

        public void ExecutedTransition(IStateMachineInformation<States, Events> stateMachineInformation, ITransition<States, Events> transition,
            ITransitionContext<States, Events> context)
        {
           
        }

		public void ExecutingTransition(IStateMachineInformation<States, Events> stateMachineInformation, ITransition<States, Events> transition,
            ITransitionContext<States, Events> context)
        {
           
        }
    }
}
