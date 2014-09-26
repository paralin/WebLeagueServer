using System;
using System.Collections.Generic;
using System.Linq;
using Appccelerate.StateMachine;
using Appccelerate.StateMachine.Machine;
using JWT;
using KellermanSoftware.CompareNetObjects;
using log4net;
using Serilog;
using SteamKit2.GC.CSGO.Internal;
using SteamKit2.GC.Dota.Internal;
using WLBot.LobbyBot.Enums;
using WLCommon;
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


        private Dictionary<MatchSetupDetails, LobbyBot.LobbyBot> Bots = new Dictionary<MatchSetupDetails, LobbyBot.LobbyBot>(); 
        /// <summary>
        /// Create a new client with an XSocket URI.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="botid">ID of the bot in the database</param>
        /// <param name="secret">Corresponding secret of the bot in the db</param>
        public WLBotClient(string url, string botid, string secret)
        {
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
            if (running) return;
            running = true;
            if(shouldReconnect) client.Reconnect();
            else client.Open();
            shouldReconnect = true;
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
                        var plyr = details.Players.FirstOrDefault(m => m.SID == member.id+"");
                        if(plyr != null) players.Add(new PlayerReadyArgs.Player
                        {
                            IsReady = (member.team == DOTA_GC_TEAM.DOTA_GC_TEAM_BAD_GUYS && plyr.Team == MatchTeam.Dire) || (member.team == DOTA_GC_TEAM.DOTA_GC_TEAM_GOOD_GUYS && plyr.Team == MatchTeam.Radiant),
                            SteamID = plyr.SID
                        });
                        i++;
                    }
                    args.Players = players.ToArray();
                    args.Id = details.Id;
                    controller.Invoke("playerready", args);
                };
                bot.Start();
            });
            controller.On("clearsetup", (Guid id) =>
            {
                var ldet = Bots.Keys.FirstOrDefault(m => m.Id == id);
                if (ldet != null)
                {
                    var bot = Bots[ldet];
                    bot.Destroy();
                    Bots.Remove(ldet);
                }
            });
            controller.On("finalize", (Guid id) =>
            {
                var ldet = Bots.Keys.FirstOrDefault(m => m.Id == id);
                if (ldet != null)
                {
                    var bot = Bots[ldet];
                    bot.StartGameAndLeave();
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
    }
}
