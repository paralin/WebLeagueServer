using System;
using System.Linq;
using WLCommon.Matches;
using WLCommon.Matches.Enums;
using WLNetwork.Bots;
using WLNetwork.Controllers;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Matches
{
    public static class MatchSetupDetailsEx
    {
        private static readonly Controllers.Matches Matches = new Controllers.Matches();

        public static MatchGame GetGame(this MatchSetupDetails details)
        {
            return MatchesController.Games.FirstOrDefault(m => m.Id == details.Id);
        }

        public static void Cleanup(this MatchSetupDetails details, bool shutdown = false)
        {
            MatchGame game = details.GetGame();
            if (details.Status >= MatchSetupStatus.Init && game != null)
            {
                MatchSetup setup = game.Setup;
                if (setup != null)
                {
                    Guid cont = setup.ControllerGuid;
                    if (Guid.Empty != cont)
                    {
                        DotaBot ccont =
                            BotDB.BotController.Find(
                                m => m.PersistentId == cont && m.Ready && m.Setups != null && m.Setups.Contains(details))
                                .FirstOrDefault();
                        if (ccont != null)
                        {
                            ccont.Setups.Remove(details);
                        }
                    }
                    BotDB.SetupQueue.Remove(setup);
                    game.Setup = null;
                    if (!shutdown) game.StartSetup();
                }
            }
            lock (details)
            {
                if (details.Bot != null)
                {
                    details.Bot.InUse = false;
                    details.Bot = null;
                }
            }
        }

        public static void TransmitUpdate(this MatchSetupDetails detials)
        {
            MatchGame game = detials.GetGame();
            if (game != null)
            {
                MatchGame match = MatchesController.Games.FirstOrDefault(m => m.Id == game.Id);
                if (match != null) match.Setup = match.Setup;
            }
        }

        public static void TransmitLobbyReady(this MatchSetupDetails detials)
        {
            MatchGame game = detials.GetGame();
            if (game == null) return;
            MatchGame match = MatchesController.Games.FirstOrDefault(m => m.Id == game.Id);
            if (match == null) return;
            Matches.InvokeTo(m => m.Match == match, "onlobbyready");
        }
    }
}