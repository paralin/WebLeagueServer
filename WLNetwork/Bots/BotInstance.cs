using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Appccelerate.StateMachine;
using Appccelerate.StateMachine.Machine;
using Dota2.GC.Dota.Internal;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WLNetwork.BotEnums;
using WLNetwork.Bots.DOTABot;
using WLNetwork.Bots.DOTABot.Enums;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;

namespace WLNetwork.Bots
{
    public class BotInstance
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public event EventHandler LobbyCleared;
		public event EventHandler LobbyReady;
		public event EventHandler LobbyPlaying;
        public event EventHandler<EMatchOutcome> MatchOutcome;
        public event EventHandler<ulong> MatchId;
        public event EventHandler<MatchStateArgs> MatchStatus;
        public event EventHandler<LeaverStatusArgs> LeaverStatus;
        public event EventHandler<PlayerReadyArgs> PlayerReady;
        public event EventHandler<States> StateUpdate;
        public event EventHandler<ulong> UnknownPlayer;
        public event EventHandler GameStarted;
        public event EventHandler FirstBloodHappened;
        public event EventHandler<uint> SpectatorCountUpdate;
        public event EventHandler<PlayerHeroArgs> HeroId;

        public LobbyBot bot;

        public BotInstance(MatchSetupDetails details)
        {
            bot = new LobbyBot(details, new WlBotExtension(details, this));
            bot.LobbyUpdate += (lobby, differences) =>
            {
                try {
                if (lobby == null)
                {
                    if (LobbyCleared != null) LobbyCleared(this, EventArgs.Empty);
                    return;
				}
                if (lobby.state == CSODOTALobby.State.UI)
                {
					if(LobbyReady != null) LobbyReady(this, EventArgs.Empty);
                    var args = new PlayerReadyArgs();
                    IEnumerable<CDOTALobbyMember> members =
                        lobby.members.Where(
                            m =>
                                m.team == DOTA_GC_TEAM.DOTA_GC_TEAM_BAD_GUYS ||
                                m.team == DOTA_GC_TEAM.DOTA_GC_TEAM_GOOD_GUYS);
                    var players = new List<PlayerReadyArgs.Player>();
                    foreach (CDOTALobbyMember member in members)
                    {
                        MatchPlayer plyr = details.Players.FirstOrDefault(m => m.SID == member.id + "");
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
                        else
                        {
                            if (UnknownPlayer != null)
                                UnknownPlayer(this, member.id);
                        }
                    }
                    args.Players = players.ToArray();
                    if (PlayerReady != null) PlayerReady(this, args);
                }
                else if (lobby.state == CSODOTALobby.State.RUN &&
                         lobby.game_state > DOTA_GameState.DOTA_GAMERULES_STATE_WAIT_FOR_PLAYERS_TO_LOAD &&
                         lobby.game_state < DOTA_GameState.DOTA_GAMERULES_STATE_POST_GAME)
                    if (differences.Differences.Any(m => m.Object1TypeName == "DOTALeaverStatus_t" || m.PropertyName == ".left_members"))
                    {
                        var args = new LeaverStatusArgs
                        {
                            Players = lobby.members.Concat(lobby.left_members).Select(
                                m => new LeaverStatusArgs.Player {Status = m.leaver_status, SteamID = "" + m.id})
                                .ToArray(),
                            Lobby = lobby
                        };
                        if (LeaverStatus != null) LeaverStatus(this, args);
                    }
                else if (lobby.game_state == DOTA_GameState.DOTA_GAMERULES_STATE_POST_GAME)
                    log.Debug(JObject.FromObject(lobby).ToString(Formatting.Indented));

				if(lobby.state == CSODOTALobby.State.RUN) if(LobbyPlaying != null) LobbyPlaying(this, EventArgs.Empty);

                var membRegex = new Regex("(members[)([0-9]+)(])");
                foreach (var diff in differences.Differences.Where(m => m.PropertyName.Contains("hero_id")))
                {
                    var match = membRegex.Match(diff.PropertyName);
                    if (match.Success)
                    {
                        int idx = int.Parse(match.Groups[1].Value.Split('[')[1]);
                        var memb = lobby.members[idx];
                        if (HeroId != null) HeroId(this, new PlayerHeroArgs() { hero_id = memb.hero_id, steam_id = memb.id });
                    }
                }

                if (MatchStatus != null)
                    MatchStatus(this, new MatchStateArgs {State = lobby.game_state, Status = lobby.state});
                if (differences.Differences.Any(m => m.PropertyName == ".match_id"))
                    if (MatchId != null) MatchId(this, lobby.match_id);
                if (lobby.match_outcome != EMatchOutcome.k_EMatchOutcome_Unknown && lobby.match_id != 0 && lobby.game_state > DOTA_GameState.DOTA_GAMERULES_STATE_GAME_IN_PROGRESS)
                    if (MatchOutcome != null) MatchOutcome(this, lobby.match_outcome);
                if (differences.Differences.Any(m=>m.PropertyName == ".game_state") && lobby.game_state == DOTA_GameState.DOTA_GAMERULES_STATE_HERO_SELECTION)
                    if (GameStarted != null) GameStarted(this, EventArgs.Empty);
                if(differences.Differences.Any(m=>m.PropertyName == ".first_blood_happened") && lobby.first_blood_happened)
                    if (FirstBloodHappened != null) FirstBloodHappened(this, EventArgs.Empty);
                if(differences.Differences.Any(m=>m.PropertyName == ".num_spectators"))
                    if (SpectatorCountUpdate != null) SpectatorCountUpdate(this, lobby.num_spectators);
                }catch(Exception ex)
                {
                    log.Error("Unhandled exception in lobbyUpdate", ex);
                }
            };
        }

        public void Start()
        {
            bot.Start();
        }

        public void Stop()
        {
            //bot.leaveLobby();
            bot.Destroy();
        }

        public void LeaveLobby()
        {
            bot.leaveLobby();
        }

        public void FetchMatchResult(ulong matchId, Action<CMsgDOTAMatch> cb)
        {
            bot.FetchMatchResult(matchId, cb);
        }

        public void StartMatch()
        {
            bot.StartGame();
        }

        private class WlBotExtension : IExtension<States, Events>
        {
            private readonly BotInstance client;
            private readonly MatchSetupDetails details;
            private readonly ILog log;

            public WlBotExtension(MatchSetupDetails details, BotInstance client)
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

            public void EventQueued(IStateMachineInformation<States, Events> stateMachine, Events eventId,
                object eventArgument)
            {
            }

            public void EventQueuedWithPriority(IStateMachineInformation<States, Events> stateMachine, Events eventId,
                object eventArgument)
            {
            }

            public void SwitchedState(IStateMachineInformation<States, Events> stateMachine,
                IState<States, Events> oldState,
                IState<States, Events> newState)
            {
                log.Debug("Switched state to " + newState.Id);
                if(client.StateUpdate != null) client.StateUpdate(client, newState.Id);
            }

            public void InitializingStateMachine(IStateMachineInformation<States, Events> stateMachine,
                ref States initialState)
            {
            }

            public void InitializedStateMachine(IStateMachineInformation<States, Events> stateMachine,
                States initialState)
            {
            }

            public void EnteringInitialState(IStateMachineInformation<States, Events> stateMachine, States state)
            {
            }

            public void EnteredInitialState(IStateMachineInformation<States, Events> stateMachine, States state,
                ITransitionContext<States, Events> context)
            {
            }

            public void FiringEvent(IStateMachineInformation<States, Events> stateMachine, ref Events eventId,
                ref object eventArgument)
            {
            }

            public void FiredEvent(IStateMachineInformation<States, Events> stateMachine,
                ITransitionContext<States, Events> context)
            {
            }

            public void HandlingEntryActionException(IStateMachineInformation<States, Events> stateMachine,
                IState<States, Events> state, ITransitionContext<States, Events> context,
                ref Exception exception)
            {
            }

            public void HandledEntryActionException(IStateMachineInformation<States, Events> stateMachine,
                IState<States, Events> state, ITransitionContext<States, Events> context,
                Exception exception)
            {
            }

            public void HandlingExitActionException(IStateMachineInformation<States, Events> stateMachine,
                IState<States, Events> state, ITransitionContext<States, Events> context,
                ref Exception exception)
            {
            }

            public void HandledExitActionException(IStateMachineInformation<States, Events> stateMachine,
                IState<States, Events> state, ITransitionContext<States, Events> context,
                Exception exception)
            {
            }

            public void HandlingGuardException(IStateMachineInformation<States, Events> stateMachine,
                ITransition<States, Events> transition,
                ITransitionContext<States, Events> transitionContext, ref Exception exception)
            {
            }

            public void HandledGuardException(IStateMachineInformation<States, Events> stateMachine,
                ITransition<States, Events> transition,
                ITransitionContext<States, Events> transitionContext, Exception exception)
            {
            }

            public void HandlingTransitionException(IStateMachineInformation<States, Events> stateMachine,
                ITransition<States, Events> transition,
                ITransitionContext<States, Events> context, ref Exception exception)
            {
            }

            public void HandledTransitionException(IStateMachineInformation<States, Events> stateMachine,
                ITransition<States, Events> transition,
                ITransitionContext<States, Events> transitionContext, Exception exception)
            {
            }

            public void SkippedTransition(IStateMachineInformation<States, Events> stateMachineInformation,
                ITransition<States, Events> transition,
                ITransitionContext<States, Events> context)
            {
            }

            public void ExecutedTransition(IStateMachineInformation<States, Events> stateMachineInformation,
                ITransition<States, Events> transition,
                ITransitionContext<States, Events> context)
            {
            }

            public void ExecutingTransition(IStateMachineInformation<States, Events> stateMachineInformation,
                ITransition<States, Events> transition,
                ITransitionContext<States, Events> context)
            {
            }
        }
    }
}
