using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using SteamKit2.GC.Dota.Internal;
using WLCommon.Arguments;
using WLCommon.DOTABot.Enums;
using WLCommon.Matches;
using WLCommon.Matches.Enums;
using WLNetwork.Bots;
using WLNetwork.Database;
using WLNetwork.Matches;
using XSockets.Core.Common.Socket.Attributes;
using XSockets.Core.Common.Socket.Event.Arguments;
using XSockets.Core.XSocket;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Controllers
{
    /// <summary>
    /// The controller for Dota BotHost
    /// </summary>
    [Authorize(Roles = "dotaBot")]
    public class DotaBot : XSocketController
    {
        /// <summary>
        /// Is this bot authed?
        /// </summary>
        public bool Authed { get { return this.ConnectionContext.IsAuthenticated&&this.ConnectionContext.User.IsInRole("dotaBot"); } }

        private bool _ready;
        public bool Ready { get { return Authed && _ready; } }

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public ObservableCollection<MatchSetupDetails> Setups = new ObservableCollection<MatchSetupDetails>();

        public DotaBot()
        {
            Setups = new ObservableCollection<MatchSetupDetails>();
            Setups.CollectionChanged += SetupsOnCollectionChanged;
            this.OnClose += HandleClosed;
        }

        private void HandleClosed(object sender, OnClientDisconnectArgs e)
        {
            Cleanup();
        }

        private void Cleanup()
        {
            foreach (var setup in Setups.ToArray())
            {
                setup.Cleanup();
            }
            Setups.CollectionChanged -= SetupsOnCollectionChanged;
            Setups.Clear();
            Setups = null;
            _ready = false;
        }

        public void StateUpdate(StateUpdateArgs args)
        {
            var game = Setups.FirstOrDefault(m => m.Id == args.Id);
            if (game == null)
            {
                //log.Warn("Bot state update for unknown match, "+args.Id+", commanding shutdown...");
                this.Invoke(args.Id, "clearsetup");
            }
            else
            {
                lock (game)
                {
                    log.Debug(game.Bot.Username + " -> state => " + args.State);
                    switch (args.State)
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
                            game.TransmitUpdate();
                            game.TransmitLobbyReady();
                            break;
                        }
                    }
                }
            }
        }

        public void PlayerReady(PlayerReadyArgs args)
        {
            var game = Setups.FirstOrDefault(m => m.Id == args.Id);
            if (game == null)
            {
                this.Invoke(args.Id, "clearsetup");
            }
            else
            {
                foreach (var plyr in game.Players)
                {
                    plyr.Ready = args.Players.Any(m => m.IsReady && m.SteamID == plyr.SID);
                }
                var g = game.GetGame();
                if (g != null)
                {
                    g.Players = g.Players;
                    //also change status
                }
            }
        }

        public void MatchId(MatchIdArgs args)
        {
            var game = Setups.FirstOrDefault(m => m.Id == args.Id);
            if (game == null)
            {
                this.Invoke(args.Id, "clearsetup");
            }
            else
            {
                log.Debug("MATCH ID FIXED "+args.Id+" "+args.match_id);
                game.MatchId = args.match_id;
            }
        }

        public void LeaverStatus(LeaverStatusArgs args)
        {
            var game = Setups.FirstOrDefault(m => m.Id == args.Id);
            if (game == null)
            {
                this.Invoke(args.Id, "clearsetup");
            }
            else
            {
                foreach (var plyr in args.Players)
                {
                    var tplyr = game.Players.FirstOrDefault(m => m.SID == plyr.SteamID);
                    if (tplyr != null)
                    {
                        tplyr.IsLeaver = plyr.Status != DOTALeaverStatus_t.DOTA_LEAVER_NONE;
                        tplyr.LeaverReason = plyr.Status;
                    }
                }
                var g = game.GetGame();
                if (g != null)
                {
                    g.Players = g.Players;
                }
            }
        }

        public void MatchStatus(MatchStateArgs args)
        {
            var game = Setups.FirstOrDefault(m => m.Id == args.Id);
            if (game == null)
            {
                this.Invoke(args.Id, "clearsetup");
            }
            else
            {
                game.State = args.State;
                //log.Debug(game.Bot.Username + " -> match_state => " + args.State);
                var g = game.GetGame();
                if (g != null)
                {
                    if (args.Status == CSODOTALobby.State.POSTGAME)
                    {
                        g.Info.Status = WLCommon.Matches.Enums.MatchStatus.Complete;
                        g.Info = g.Info;
                    }else g.Setup = g.Setup;
                }
            }
        }

        public void LobbyClear(LobbyClearArgs args)
        {
            var game = Setups.FirstOrDefault(m => m.Id == args.Id);
            if (game == null)
            {
                this.Invoke(args.Id, "clearsetup");
            }
        }

        public void MatchOutcome(MatchOutcomeArgs args)
        {
            var game = Setups.FirstOrDefault(m => m.Id == args.Id);
            if (game == null)
            {
                this.Invoke(args.Id, "clearsetup");
            }
            else
            {
                var g = game.GetGame();
                if (g != null)
                {
                    g.ProcessMatchResult(args.match_outcome);
                }
            }
        }

        public override bool OnAuthorization(IAuthorizeAttribute authorizeAttribute)
        {
            return this.Authed;
        }

        private void SetupsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                var newItems = e.NewItems.OfType<MatchSetupDetails>();
                foreach (var match in newItems)
                {
                    lock (match)
                    {
                        log.Debug("Starting bot setup for " + match.Id + " " + match.Bot.Username);
                        this.Invoke(match, "startsetup");
                    }
                }
            }
            if (e.OldItems != null)
            {
                var newItems = e.OldItems.OfType<MatchSetupDetails>();
                foreach (var match in newItems)
                {
                    lock (match)
                    {
                        log.Debug("Starting bot shutdown for " + match.Bot.Username);
                        this.Invoke(match.Id, "clearsetup");
                    }
                }
            }
        }

        /// <summary>
        /// Add this bot to the active bot host pool.
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public bool ReadyUp()
        {
            if (Authed) _ready = true;
            BotDB.ProcSetupQueue();
            return Ready;
        }

        public void Finalize(MatchGame game)
        {
             this.Invoke(game.Id, "finalize");
        }
    }
}
