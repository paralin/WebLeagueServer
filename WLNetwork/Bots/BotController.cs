using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Dota2.GC.Dota.Internal;
using log4net;
using Newtonsoft.Json;
using SteamKit2;
using WLNetwork.BotEnums;
using WLNetwork.Chat;
using WLNetwork.Database;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;
using WLNetwork.Model;
using WLNetwork.Properties;
using WLNetwork.Utils;
using Timer = System.Timers.Timer;
using Newtonsoft.Json.Linq;

namespace WLNetwork.Bots
{
    public class BotController
    {
        private ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public BotInstance instance;
        private MatchSetupDetails game;
        private Timer matchResultTimeout;

        public BotController(MatchSetupDetails game)
        {
            matchResultTimeout = new Timer(8000);
            matchResultTimeout.Elapsed += MatchResultTimeout;

            this.game = game;
            log = LogManager.GetLogger("BotController "+game.Id.ToString().Split('-')[0]);

            instance = new BotInstance(game);
            instance.StateUpdate += StateUpdate;
            instance.PlayerReady += PlayerReady;
            instance.MatchId += MatchId;
            instance.LeaverStatus += LeaverStatus;
            instance.MatchStatus += MatchStatus;
            instance.LobbyCleared += LobbyClear;
            instance.MatchOutcome += MatchOutcome;
            instance.UnknownPlayer += UnknownPlayer;
            instance.GameStarted += GameStarted;
            instance.FirstBloodHappened += FirstBloodHappened;
            instance.SpectatorCountUpdate += SpectatorCountUpdate;
            instance.HeroId += HeroId;
			instance.LobbyReady += LobbyReady;
			instance.LobbyPlaying += LobbyPlaying;
        }

        private int matchResultAttempts = 0;
        private void MatchResultTimeout(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            matchResultTimeout.Stop();
            if (outcomeProcessed) return;
            if (matchResultAttempts == 2)
            {
                log.Error("Unable to fetch match result, giving up.");
                outcomeProcessed = true;
                MatchGame g = game.GetGame();
                if(g != null) g.ProcessMatchResult(EMatchOutcome.k_EMatchOutcome_Unknown, null, true);
                return;
            }
            log.Debug("DOTA2 GC has not set match_outcome, checking match history...");
            if (game.MatchId == 0)
            {
                log.Warn("Postgame with unknown match id, waiting for match_outcome instead...");
            }
            else instance.FetchMatchResult(game.MatchId, match =>
            {
                if (outcomeProcessed) return;
                if (match == null)
                {
                    log.Warn("No match result, trying again in 10 seconds...");
                    matchResultAttempts++;
                    matchResultTimeout.Start();
                }
                else
                {
                    outcomeProcessed = true;
                    log.Debug("Fetched match result with outcome good_guys_win="+match.good_guys_win+".");
                    MatchGame g = game.GetGame();
                    if (g != null)
                    {
                        g.ProcessMatchResult(match.good_guys_win ? EMatchOutcome.k_EMatchOutcome_RadVictory : EMatchOutcome.k_EMatchOutcome_DireVictory, match);
                    }
                }
            });
        }

        void LobbyPlaying (object sender, EventArgs e)
        {
			if (game.Status == MatchSetupStatus.Done)
				return;
			log.Debug("Bot entered Play state "+game.Bot.Username);
			game.Status = MatchSetupStatus.Done;
			var match = game.GetGame();
			if (match != null)
			{
				if (match.Info.Status == Matches.Enums.MatchStatus.Lobby)
				{
					match.Info.Status = Matches.Enums.MatchStatus.Play;
					match.Info = match.Info;
				}
			}
			game.TransmitUpdate();
        }

        void LobbyReady (object sender, EventArgs e)
        {
            if (game.Status == MatchSetupStatus.Wait) return;

			log.Debug("Bot entered LobbyUI " + game.Bot.Username);
            hasStarted = false;
			game.Status = MatchSetupStatus.Wait;
			game.State = DOTA_GameState.DOTA_GAMERULES_STATE_INIT;
			var match = game.GetGame();
			if (match != null)
			{
				match.Info.Status = Matches.Enums.MatchStatus.Lobby;
				match.Info = match.Info;
			}
			game.TransmitUpdate();
			game.TransmitLobbyReady();
        }

        private void HeroId(object sender, PlayerHeroArgs playerHeroArgs)
        {
            //Find the player
            var player = game.Players.FirstOrDefault(m => m.SID == playerHeroArgs.steam_id + "");
            if (player == null)
            {
                log.Warn("Unable to find player for hero ID update! Steam ID: "+playerHeroArgs.steam_id);
                return;
            }

            if(player.Hero != null && player.Hero.Id == playerHeroArgs.hero_id) return;

            //Find the hero
            HeroInfo hero;
            if (!HeroCache.Heros.TryGetValue(playerHeroArgs.hero_id, out hero))
            {
                log.Warn("Unable to find hero ID "+playerHeroArgs.hero_id+"!");
                return;
            }

            //And we're done!
            player.Hero = hero;

            log.Debug("Hero ID "+hero.Id+" resolved to "+hero.fullName);
            
            //Let's not transmit this yet as when the match state updates it will be transmitted
            if (game.Players.Where(m => m.Team == MatchTeam.Dire || m.Team == MatchTeam.Radiant)
                .All(m => m.Hero != null))
            {
                var g = game.GetGame();
                if (g != null) g.Players = g.Players;
            }
        }

        private void SpectatorCountUpdate(object sender, uint u)
        {
            if (game.SpectatorCount == u) return;
            game.SpectatorCount = u;
            game.TransmitUpdate();
        }

        private void FirstBloodHappened(object sender, EventArgs eventArgs)
        {
            if (game.FirstBloodHappened) return;
            var g = game.GetGame();
            if (g != null)
            {
                game.FirstBloodHappened = true;
                game.TransmitUpdate();
                g.SaveActiveGame();
                if(g.Info.League != null) ChatChannel.SystemMessage(g.Info.League, "First blood was just spilled in match " + game.Id.ToString().Substring(0, 4) + "!");
            }
        }

        private bool hasStarted = false;
        private void GameStarted(object sender, EventArgs eventArgs)
        {
            if (hasStarted) return;
            hasStarted = true;
            var g =game.GetGame();
            if (g != null)
            {
                game.GameStartTime = DateTime.UtcNow;
                game.ServerSteamID = new SteamID(instance.bot.dota.Lobby.server_id).Render(true);
                game.TransmitUpdate();
                g.GameStarted();
                g.KickSpectators();
                g.SaveActiveGame();
            }
        }

        private void UnknownPlayer(object sender, ulong player)
        {
            if (game.Bot == null || game.Status == MatchSetupStatus.Done) return;
            log.Warn("Kicking unknown player "+player);
            var memb = instance.bot.dota.Lobby.members.FirstOrDefault(m => m.id == player);
            if(memb != null) instance.bot.SendLobbyMessage("Kicked "+memb.name+" from the lobby.");
            instance.bot.dota.KickPlayerFromLobby(player.ToAccountID());
        }

        public void StateUpdate(object sender, States states)
        {
            lock (game)
            {
                if(game.Bot != null)
                    log.Debug(game.Bot.Username + " -> state => " + states);
                switch (states)
                {
                    case States.DisconnectNoRetry:
                    {
                        log.Debug("Bot setup failed for " + game.Bot.Username + ", finding another...");
                        game.Bot.InUse = false;
                        game.Bot.Invalid = true;
                        Mongo.Bots.Save(game.Bot);
                        game.Cleanup();
                        break;
                    }
                }
            }
        }

        private bool lobbyReadySent = false;
        private bool lobbyUnReadySent = false;
        public void PlayerReady(object sender, PlayerReadyArgs playerReadyArgs)
        {
            if (game.Bot == null) return;
            MatchGame g = game.GetGame(); 
            bool anyNotReady = false;
            foreach (MatchPlayer plyr in game.Players)
            {
                plyr.Ready = playerReadyArgs.Players.Any(m => m.IsReady && m.SteamID == plyr.SID);
                if (!plyr.Ready) anyNotReady = true;
            }
            if (g != null)
            {
                g.Players = g.Players;
                //also change status
                if (!anyNotReady)
                {
                    if (!lobbyReadySent)
                    {
                        lobbyReadySent = true;
                        var plyr = g.Players.FirstOrDefault(m => m.IsCaptain);
                        if (plyr != null)
                            instance.bot.SendLobbyMessage("Lobby is ready, waiting for " + plyr.Name + " to start.");
                    }
                }
                else
                {
                    if (lobbyReadySent)
                    {
                        var plyr = g.Players.FirstOrDefault(m => !m.Ready);
                        if(plyr != null)
                            instance.bot.SendLobbyMessage("Lobby is no longer ready, waiting for " + plyr.Name + " to join "+(plyr.Team == MatchTeam.Dire ? "Dire" : "Radiant")+".");
                    }
                    lobbyReadySent = false;
                }
            }
        }

        public void MatchId(object sender, ulong id)
        {
            if (id == game.MatchId) return;
            log.Debug("MATCH ID FIXED " +game.Id+ " " + id);
            game.MatchId = id;
        }

        public void LeaverStatus(object sender, LeaverStatusArgs args)
        {
            if (game.Bot == null) return;
            //var someoneAbandoned = false;
            foreach (LeaverStatusArgs.Player plyr in args.Players)
            {
                MatchPlayer tplyr = game.Players.FirstOrDefault(m => m.SID == plyr.SteamID);
                if (tplyr != null && tplyr.Team < MatchTeam.Spectate)
                {
                    var plyrLeft = args.Lobby.left_members.Any(m => "" + m.id == plyr.SteamID);
                    tplyr.IsLeaver = plyr.Status != DOTALeaverStatus_t.DOTA_LEAVER_NONE || plyrLeft;
                    tplyr.LeaverReason = plyr.Status;
                    //if (plyrLeft) someoneAbandoned = true;
                }
            }
            MatchGame g = game.GetGame();
            if (g != null)
            {
                g.Players = g.Players;
				/*
                if (someoneAbandoned)
                {
                    log.Warn("ABANDON FOR "+g.Id+", CLOSING GAME");
                    g.ProcessMatchResult(EMatchOutcome.k_EMatchOutcome_NotScored_Leaver);
                }
                */
            }
        }

        private bool startedResultCheck = false;
        public void MatchStatus(object sender, MatchStateArgs args)
        {
            MatchGame g = game.GetGame();
            if (g == null) return;
            if (game.State != args.State)
            {
                game.State = args.State;
                g.Setup = g.Setup;
                if (game.Bot != null)
                    log.Debug(game.Bot.Username + " -> match_state => " + args.State);
            }
            g.Setup = g.Setup;
            if ((args.State >= DOTA_GameState.DOTA_GAMERULES_STATE_POST_GAME ||
                args.Status == CSODOTALobby.State.POSTGAME) && g.Info.Status == Matches.Enums.MatchStatus.Play)
            {
                g.Info.Status = Matches.Enums.MatchStatus.Complete;
                g.Info = g.Info;

                if (!outcomeProcessed && !startedResultCheck)
                {
                    startedResultCheck = true;
                    Task.Run(() =>
                    {
                        Thread.Sleep(1500);
                        if (!outcomeProcessed) AttemptAPIResult();
                    });
                }
            }
            else g.Setup = g.Setup;
        }

        /// <summary>
        /// Attempt to fetch from web api
        /// </summary>
        private async void AttemptAPIResult()
        {
            MatchGame g = game.GetGame();
            if (g == null || outcomeProcessed) return;
            if (game.TicketID == 0) MatchResultTimeout(this, null);
            else
            {
                log.Debug("Attempting API result fetch...");
                bool mres = true;
                bool fetchedSuccess = false;
                try
                {
                    using (var httpClient = new HttpClient())
                    {
                        var res =
                            await
                                httpClient.GetStringAsync(
                                    string.Format(
                                        "https://api.steampowered.com/IDOTA2Match_570/GetMatchDetails/v001/?key={0}&match_id={1}",
                                        Settings.Default.SteamAPI, game.MatchId));
                        var pars = JObject.Parse(res);
                        mres = pars["result"]["radiant_win"].Value<bool>();
                        fetchedSuccess = true;
                    }
                }
                catch (ArgumentNullException)
                {
                    log.Warn("Valve doesn't have a web API result.");
                }
                catch (Exception ex)
                {
                    log.Error("Unable to download match result.", ex);
                }
                if (outcomeProcessed) return;
                if (!fetchedSuccess)
                {
                    log.Warn("Match result via api failed, using in-game result fetch...");
                    MatchResultTimeout(this, null);
                }
                else
                {
                    log.Debug("Successfully fetched result via web api, radiant won = "+mres);
                    outcomeProcessed = true;
                    g.ProcessMatchResult(mres ? EMatchOutcome.k_EMatchOutcome_RadVictory :  EMatchOutcome.k_EMatchOutcome_DireVictory, null, true);
                }
            }
        }

        public void LobbyClear(object sender, EventArgs eventArgs)
        {
            //todo: do something here?
        }

        bool outcomeProcessed = false;
        public void MatchOutcome(object sender, EMatchOutcome args)
        {
            if (outcomeProcessed || game.MatchId == 0)
                return;
            outcomeProcessed = true;
            matchResultTimeout.Start();
            if (game.Bot == null) return;
            MatchGame g = game.GetGame();
            if (g != null)
            {
                g.ProcessMatchResult(args);
            }
        }
    }
}
