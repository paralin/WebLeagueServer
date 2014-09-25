using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using Serilog;
using WLCommon.Arguments;
using WLCommon.LobbyBot.Enums;
using WLCommon.Matches;
using WLCommon.Matches.Enums;
using WLCommon.Model;
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
                log.Warn("Bot state update for unknown match, "+args.Id+", commanding shutdown...");
                this.Invoke(args.Id, "clearsetup");
            }
            else
            {
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
                        log.Debug("Bot lobby setup finished for " + game.Bot.Username);
                        game.Status = MatchSetupStatus.Wait;
                        game.TransmitUpdate();
                        break;
                    }
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
                    log.Debug("Starting bot setup for "+match.Id+" "+match.Bot.Username);
                    this.Invoke(match, "startsetup");
                }
            }
            if (e.OldItems != null)
            {
                var newItems = e.OldItems.OfType<MatchSetupDetails>();
                foreach (var match in newItems)
                {
                    log.Debug("Starting bot shutdown for " + match.Bot.Username);
                    this.Invoke(match.Id, "clearsetup");
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
    }
}
