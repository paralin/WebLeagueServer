﻿using System;
using System.Linq;
using System.Reflection;
using log4net;
using SteamKit2.GC.Dota.Internal;
using WLNetwork.BotEnums;
using WLNetwork.Database;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;
using WLNetwork.Utils;

namespace WLNetwork.Bots
{
    public class BotController
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public BotInstance instance;
        private MatchSetupDetails game;

        public BotController(MatchSetupDetails game)
        {
            this.game = game;
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
        }

        private void GameStarted(object sender, EventArgs eventArgs)
        {
            var g =game.GetGame();
            if (g != null)
            {
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
                    case States.DotaLobbyUI:
                    {
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
                        break;
                    }
                    case States.DotaLobbyPlay:
                    {
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
            var someoneAbandoned = false;
            foreach (LeaverStatusArgs.Player plyr in args.Players)
            {
                MatchPlayer tplyr = game.Players.FirstOrDefault(m => m.SID == plyr.SteamID);
                if (tplyr != null && tplyr.Team < MatchTeam.Spectate)
                {
                    var plyrLeft = args.Lobby.left_members.Any(m => "" + m.id == plyr.SteamID);
                    tplyr.IsLeaver = plyr.Status != DOTALeaverStatus_t.DOTA_LEAVER_NONE || plyrLeft;
                    tplyr.LeaverReason = plyr.Status;
                    if (plyrLeft) someoneAbandoned = true;
                }
            }
            MatchGame g = game.GetGame();
            if (g != null)
            {
                g.Players = g.Players;
                if (someoneAbandoned)
                {
                    log.Warn("ABANDON FOR "+g.Id+", CLOSING GAME");
                    g.ProcessMatchResult(EMatchOutcome.k_EMatchOutcome_NotScored_Leaver);
                }
            }
        }

        public void MatchStatus(object sender, MatchStateArgs args)
        {
            game.State = args.State;
            if (game.Bot == null) return;
            log.Debug(game.Bot.Username + " -> match_state => " + args.State);
            MatchGame g = game.GetGame();
            if (g != null)
            {
                if (args.Status == CSODOTALobby.State.POSTGAME)
                {
                    g.Info.Status = WLNetwork.Matches.Enums.MatchStatus.Complete;
                    g.Info = g.Info;
                }
                else g.Setup = g.Setup;
            }
        }

        public void LobbyClear(object sender, EventArgs eventArgs)
        {
            //todo: do something here?
        }

        public void MatchOutcome(object sender, EMatchOutcome args)
        {
            if (game.Bot == null) return;
            MatchGame g = game.GetGame();
            if (g != null)
            {
                g.ProcessMatchResult(args);
            }
        }
    }
}