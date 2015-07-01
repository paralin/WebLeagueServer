using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dota2.Engine.Control;
using Dota2.Engine.Game;
using Dota2.Engine.Game.Entities.Dota;
using Dota2.GC.Dota.Internal;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SteamKit2;
using WLNetwork.Bots.LobbyBot;
using WLNetwork.Bots.LobbyBot.Enums;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;
using WLNetwork.Utils;
using MatchType = WLNetwork.Matches.Enums.MatchType;

namespace WLNetwork.Bots
{
    public class BotInstance
    {
        private ILog log;

        public event EventHandler LobbyCleared;
        public event EventHandler LobbyReady;
        public event EventHandler LobbyPlaying;
        public event EventHandler<EMatchOutcome> MatchOutcome;
        public event EventHandler<ulong> MatchId;
        public event EventHandler<MatchStateArgs> MatchStatus;
        public event EventHandler<LeaverStatusArgs> LeaverStatus;
        public event EventHandler<PlayerReadyArgs> PlayerReady;
        public event EventHandler<State> StateUpdate;
        public event EventHandler<ulong> UnknownPlayer;
        public event EventHandler GameStarted;
        public event EventHandler FirstBloodHappened;
        public event EventHandler<uint> SpectatorCountUpdate;
        public event EventHandler<PlayerHeroArgs> HeroId;
        public event EventHandler InvalidCreds;


        public Bot bot;

        private MatchSetupDetails Details;
        private HashSet<string> teamMsgSent = new HashSet<string>();

        public BotInstance(MatchSetupDetails details)
        {
            Details = details;

            log = LogManager.GetLogger(details.Bot.Username);

            bot = new Bot(new SteamUser.LogOnDetails() {Username = details.Bot.Username, Password = details.Bot.Password}, contrs:new BotGameController(this));
            bot.InvalidCreds += (sender, args) =>
            {
                log.Warn("Steam reports invalid creds for "+details.Bot.Username+"!");
            };
            bot.StateTransitioned += (sender, transition) =>
            {
                StateUpdate?.Invoke(this, transition.Destination);

                if (transition.Destination != State.DotaMenu) return;
                if (Details.IsRecovered)
                {
                    Task.Run(() =>
                    {
                        Thread.Sleep(2000);
                        if (bot.DotaGCHandler.Lobby != null || bot.State != State.DotaMenu) return;
                        log.Debug("Lobby not recovered, assuming match ended...");
                        var g = details.GetGame();
                        g?.LobbyNotRecovered();
                    });
                }
                else
                {
                    log.Debug("Setting up the lobby with passcode [" + Details.Password + "]...");

                    var game = Details.GetGame();
                    var gameName = (game.Info.League ?? "FPL").ToUpper();
                    switch (game.Info.MatchType)
                    {
                        case MatchType.Captains:
                        case MatchType.StartGame:
                            gameName += " Match " + Details.Id.ToString().Substring(0, 4);
                            break;
                        case MatchType.OneVsOne:
                            var p1 = Details.Players.FirstOrDefault(m => m.Team == MatchTeam.Radiant);
                            var p2 = Details.Players.FirstOrDefault(m => m.Team == MatchTeam.Dire);
                            if (p1 != null && p2 != null) gameName += " " + p1.Name + " vs. " + p2.Name;
                            break;
                    }

                    var ldetails = new CMsgPracticeLobbySetDetails
                    {
                        allchat = Details.GameMode == GameMode.SOLOMID,
#if DEBUG
                        allow_cheats = true,
#else
                        allow_cheats = false,
#endif
                        allow_spectating = true,
                        dota_tv_delay = game.Info.MatchType == MatchType.OneVsOne ? LobbyDotaTVDelay.LobbyDotaTV_10 : LobbyDotaTVDelay.LobbyDotaTV_120,
                        fill_with_bots = false,
                        game_mode = (uint)(DOTA_GameMode)Details.GameMode,
                        game_name = gameName,
                        game_version = DOTAGameVersion.GAME_VERSION_CURRENT,
                        server_region = Details.Region
                    };
                    if (Details.TicketID != 0 && Details.GameMode != GameMode.SOLOMID) ldetails.leagueid = (uint)Details.TicketID;
                    bot.DotaGCHandler.CreateLobby(Details.Password, ldetails);
                }
            };
            bot.LobbyUpdate += (oldLobby, lobby) =>
            {
                try
                {
                    if (lobby == null)
                    {
                        LobbyCleared?.Invoke(this, EventArgs.Empty);
                        return;
                    }
                    if (lobby.pass_key != details.Password)
                    {
                        log.Warn("Lobby pass key " + lobby.pass_key + " is not the actual pass key " + details.Password + ", leaving the lobby.");
                        bot.LeaveLobby();
                        return;
                    }
                    if (lobby.state == CSODOTALobby.State.UI)
                    {
                        LobbyReady?.Invoke(this, EventArgs.Empty);
                        var args = new PlayerReadyArgs();
                        var players = new List<PlayerReadyArgs.Player>();
                        foreach (CDOTALobbyMember member in lobby.members.Where(m => m.id != bot.SteamUser.SteamID))
                        {
                            MatchPlayer plyr = details.Players.FirstOrDefault(m => m.SID == member.id + "");
                            if (plyr != null)
                            {
                                if (plyr.Team == MatchTeam.Spectate || plyr.Team == MatchTeam.Unassigned) continue;
                                var ready =
                                    (member.team == DOTA_GC_TEAM.DOTA_GC_TEAM_BAD_GUYS &&
                                     plyr.Team == MatchTeam.Dire) ||
                                    (member.team == DOTA_GC_TEAM.DOTA_GC_TEAM_GOOD_GUYS &&
                                     plyr.Team == MatchTeam.Radiant);
                                players.Add(new PlayerReadyArgs.Player
                                {
                                    IsReady = ready,
                                    SteamID = plyr.SID
                                });
                                if (!ready && !teamMsgSent.Contains(plyr.SID) &&
                                    (plyr.Team == MatchTeam.Dire || plyr.Team == MatchTeam.Radiant))
                                {
                                    bot.SendLobbyMessage(plyr.Name + " is on " +
                                                         (plyr.Team == MatchTeam.Dire ? "Dire" : "Radiant"));
                                    teamMsgSent.Add(plyr.SID);
                                }
                            }
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
#if DUMP_LOBBY_POSTGAME
                    else if (lobby.game_state >= DOTA_GameState.DOTA_GAMERULES_STATE_POST_GAME)
                        log.Debug(JObject.FromObject(lobby).ToString(Formatting.Indented));
#endif

                    if (lobby.state == CSODOTALobby.State.RUN)
                        if (LobbyPlaying != null) LobbyPlaying(this, EventArgs.Empty);

                    if (HeroId != null)
                    {
                        foreach (var memb in lobby.members.Where(memb => memb.hero_id != 0))
                        {
                            HeroId(this, new PlayerHeroArgs() {hero_id = memb.hero_id, steam_id = memb.id});
                        }
                    }
                    if (MatchStatus != null)
                        MatchStatus(this, new MatchStateArgs {State = lobby.game_state, Status = lobby.state});
                    if (MatchId != null && lobby.match_id != 0) MatchId(this, lobby.match_id);
                    if (lobby.match_outcome != EMatchOutcome.k_EMatchOutcome_Unknown && MatchOutcome != null)
                        MatchOutcome(this, lobby.match_outcome);
                    if (lobby.game_state == DOTA_GameState.DOTA_GAMERULES_STATE_HERO_SELECTION)
                        if (GameStarted != null) GameStarted(this, EventArgs.Empty);
                    if (lobby.first_blood_happened)
                        if (FirstBloodHappened != null) FirstBloodHappened(this, EventArgs.Empty);
                    if (SpectatorCountUpdate != null) SpectatorCountUpdate(this, lobby.num_spectators);
                }
                catch (Exception ex)
                {
                    log.Error("Unhandled exception in lobbyUpdate", ex);
                }
            };
        }

        public void Start()
        {
            bot.Start();
        }

        public void ServerOutcome(EMatchOutcome outc)
        {
            MatchOutcome?.Invoke(this, outc);
        }

        public void Stop(bool deleteLobby = false)
        {
            if (deleteLobby) bot.LeaveLobby();
            bot.Stop();
        }

        public void FetchMatchResult(ulong matchId, Action<CMsgDOTAMatch> cb)
        {
            bot.FetchMatchResult(matchId, cb);
        }

        public void StartMatch()
        {
            if (bot.DotaGCHandler.Lobby == null)
            {
                log.Fatal("StartMatch called but bot isn't in a lobby!");
                return;
            }

            // Kick anyone in team slots that isn't supposed to be there
            foreach (var member in from member in bot.DotaGCHandler.Lobby.members.Where(m=>m.id != bot.SteamUser.SteamID.ConvertToUInt64())
                let plyr = Details.Players.FirstOrDefault(m => m.SID == "" + member.id)
                where
                    plyr == null || (plyr.Team != MatchTeam.Dire && member.team == DOTA_GC_TEAM.DOTA_GC_TEAM_BAD_GUYS) ||
                    (plyr.Team != MatchTeam.Radiant && member.team == DOTA_GC_TEAM.DOTA_GC_TEAM_GOOD_GUYS)
                select member)
            {
                log.Warn("Kicking player " + member.id + " for being on team " + member.team + " when they shouldn't.");
                bot.DotaGCHandler.KickPlayerFromLobby(member.id.ToAccountID());
            }

            bot.DotaGCHandler.LaunchLobby();
        }

#region Client Controller

        /// <summary>
        /// Controls a spectator bot
        /// </summary>
        private class BotGameController : IDotaGameController
        {
            private ILog log;

            private ulong _steamId;
            private DotaGameState _state;
            private IDotaGameCommander _commander;

            private bool _hasSentHello = false;
            private DOTA_GameState oldState = DOTA_GameState.DOTA_GAMERULES_STATE_INIT;

            private BotInstance _inst;

            public BotGameController(BotInstance inst)
            {
                log = inst.log;
                _inst = inst;
            }

            /// <summary>
            /// Initialize the controller as the client begins to connect.
            /// </summary>
            /// <param name="id">Steam ID</param>
            /// <param name="state">Emulated DOTA game client state</param>
            /// <param name="commander">Command generator</param>
            public void Initialize(ulong id, DotaGameState state, IDotaGameCommander commander)
            {
                _steamId = id;
                _state = state;
                _commander = commander;
            }

            private void Say(string msg)
            {
                _commander.Submit("say \""+msg+"\"");
            }

            private bool hasSubmittedResult = false;

            /// <summary>
            /// Called every tick. Must return near-instantly.
            /// </summary>
            public void Tick()
            {
                if (!_state.EntityPool.Has<GameRules>()) return;
                var gr = _state.EntityPool.GetSingle<GameRules>();
                var gs = gr.GameState.Value;
                if (gs != oldState)
                {
                    log.DebugFormat("State {0} => {1}", oldState.ToString("G"), gs.ToString("G"));
                    oldState = gr.GameState.Value;
                }
                if (gs >= DOTA_GameState.DOTA_GAMERULES_STATE_HERO_SELECTION && !_hasSentHello && !_inst.Details.IsRecovered)
                {
                    _hasSentHello = true;
                    try
                    {
                        Say(string.Format("This is {0} match ID {1}!",
                            Leagues.LeagueDB.Leagues[_inst.Details.GetGame().Info.League].Name.Replace("FPL",
                                "FACEIT.com Pro League"), _inst.Details.Id.ToString().Substring(0, 4)));
                        log.DebugFormat("Sent message to all chat.");
                    }
                    catch (Exception ex)
                    {
                        log.Warn("Couldn't send welcome message.", ex);
                    }
                }
                if (!hasSubmittedResult && gs >= DOTA_GameState.DOTA_GAMERULES_STATE_POST_GAME &&
                    (gr.GameWinner.Value == GameRules.DOTA_ServerTeam.RADIANT ||
                     gr.GameWinner.Value == GameRules.DOTA_ServerTeam.DIRE))
                {
                    log.Debug("Game server has reported result "+gr.GameWinner.Value.ToString("G")+"!");
                    hasSubmittedResult = true;
                    _inst.ServerOutcome(gr.GameWinner.Value == GameRules.DOTA_ServerTeam.RADIANT ? EMatchOutcome.k_EMatchOutcome_RadVictory : EMatchOutcome.k_EMatchOutcome_DireVictory);
                }
                foreach (var msg in _state.ChatMessages)
                {
                    log.Debug("[ALLCHAT] " + msg.prefix + ": " + msg.text);
                    if (msg.text.Contains("!pause"))
                    {
                        if (gr.PauseTeam.Value == GameRules.DOTA_ServerTeam.DIRE ||
                            gr.PauseTeam.Value == GameRules.DOTA_ServerTeam.RADIANT)
                        {
                            Say("The game was paused by " +
                                (gr.PauseTeam.Value == GameRules.DOTA_ServerTeam.RADIANT ? "radiant." : "dire."));
                        }
#if ENABLE_PAUSE_COMMAND
                        else
                        {
                            Say("Pausing the game by request from " + msg.prefix + "!");
                            _commander.Submit("dota_pause");
                        }
#endif
                    }
#if ENABLE_WHOAMI
                    else if (msg.text.Contains("!whoami"))
                    {
                        Say("You are "+msg.prefix+"!");
                    }
#endif
#if ENABLE_GAMETIME
                    else if (msg.text.Contains("!time"))
                    {
                        Say("Current game time is "+gr.GameTime.Value+", game started at "+gr.GameStartTime.Value+".");
                    }else if (msg.text.Contains("!timeofday"))
                    {
                        Say("Time of day is: "+gr.NetTimeOfDay.Value);
                    }
#endif
                }
                _state.ChatMessages.Clear();
                foreach (var msg in _state.ChatEvents)
                {
                    log.Debug("[CHATEVENT] " + msg.type.ToString("G") + ": " + msg.value);
                    switch (msg.type)
                    {
#if ENABLE_UNNECESSARY_ALLCHAT
                        case DOTA_CHAT_MESSAGE.CHAT_MESSAGE_FIRSTBLOOD:
                            Say("Nice firstblood Kappa.");
                            break;
                        case DOTA_CHAT_MESSAGE.CHAT_MESSAGE_RECONNECT:
                        case DOTA_CHAT_MESSAGE.CHAT_MESSAGE_CONNECT:
                            Say("Welcome back "+msg.value+".");
                            break;
                        case DOTA_CHAT_MESSAGE.CHAT_MESSAGE_HERO_KILL:
                            Say("Wow, that guy is totally feeding.");
                            break;
                        case DOTA_CHAT_MESSAGE.CHAT_MESSAGE_TOWER_KILL:
                            Say("Boom! The tower went down.");
                            break;
#endif
                    }
                }
                _state.ChatEvents.Clear();
                /*foreach (var msg in _state.GameEvents)
                {
                    log.Debug("[GAMEEVENT] "+msg.eventid);
                }*/
                _state.GameEvents.Clear();
            }
        }
#endregion
    }
}