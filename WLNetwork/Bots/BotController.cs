using System;
using System.Linq;
using System.Reflection;
using System.Timers;
using Dota2.GC.Dota.Internal;
using log4net;
using SteamKit2;
using WLNetwork.BotEnums;
using WLNetwork.Chat;
using WLNetwork.Database;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;
using WLNetwork.Model;
using WLNetwork.Utils;

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
            matchResultTimeout = new Timer(5000);
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

        private void MatchResultTimeout(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            matchResultTimeout.Stop();
            if (outcomeProcessed) return;
            log.Debug("DOTA2 GC is taking a long time to set match_outcome, checking match history...");
            if (game.MatchId == 0)
            {
                log.Warn("Postgame with unknown match id, waiting for match_outcome instead...");
            }
            else instance.FetchMatchResult(game.MatchId, match =>
            {
                if (outcomeProcessed) return;
                if (match == null)
                {
                    log.Warn("No match result, waiting for match_outcome...");
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
			if (game.Status == MatchSetupStatus.Wait)
				return;
			log.Debug("Bot entered LobbyUI " + game.Bot.Username);
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
            game.SpectatorCount = u;
            game.TransmitUpdate();
        }

        private void FirstBloodHappened(object sender, EventArgs eventArgs)
        {
            var g = game.GetGame();
            if (g != null)
            {
                if (game.FirstBloodHappened) return;
                game.FirstBloodHappened = true;
                game.TransmitUpdate();
                g.SaveActiveGame();
                if(g.Info.League != null) ChatChannel.SystemMessage(g.Info.League, "First blood was just spilled in match " + game.Id.ToString().Substring(0, 4) + "!");
            }
        }

        private void GameStarted(object sender, EventArgs eventArgs)
        {
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
            if (game.Bot == null) return;
            log.Warn("Kicking unknown player "+player);
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

        public void PlayerReady(object sender, PlayerReadyArgs playerReadyArgs)
        {
            if (game.Bot == null) return;
            foreach (MatchPlayer plyr in game.Players)
            {
                plyr.Ready = playerReadyArgs.Players.Any(m => m.IsReady && m.SteamID == plyr.SID);
            }
            MatchGame g = game.GetGame();
            if (g != null)
            {
                g.Players = g.Players;
                //also change status
            }
        }

        public void MatchId(object sender, ulong id)
        {
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

        public void MatchStatus(object sender, MatchStateArgs args)
        {
            if (game.State == args.State) return;
            game.State = args.State;
            if (game.Bot == null) return;
            log.Debug(game.Bot.Username + " -> match_state => " + args.State);
            MatchGame g = game.GetGame();
            if (g != null)
            {
                if (args.State == DOTA_GameState.DOTA_GAMERULES_STATE_POST_GAME)
                {
                    g.Info.Status = WLNetwork.Matches.Enums.MatchStatus.Complete;
                    g.Info = g.Info;
                    matchResultTimeout.Start();
                }
                else g.Setup = g.Setup;
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
