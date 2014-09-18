using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using MongoDB.Driver.Builders;
using WLCommon.Bots.Methods;
using WLCommon.Model;
using WLNetwork.Controllers;
using WLNetwork.Database;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Bots
{
    /// <summary>
    /// Keeps track of all of the bots.
    /// </summary>
    public static class BotDB
    {
        private static readonly log4net.ILog log =
   log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static Timer UpdateTimer;

        private static readonly DotaBot BotController = new DotaBot();

        /// <summary>
        /// Bot Dictionary
        /// </summary>
        public static ConcurrentDictionary<string, Bot> Bots = new ConcurrentDictionary<string, Bot>();

        static BotDB()
        {
            UpdateTimer = new Timer(15000);
            UpdateTimer.Elapsed += UpdateTimerOnElapsed;
            UpdateDB();
        }

        private static void UpdateTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            UpdateDB();
        }

        /// <summary>
        /// Check for differences in the DB
        /// </summary>
        internal static void UpdateDB()
        {
            var bots = Mongo.Bots.FindAs<Bot>(Query.Or(Query.NotExists("Invalid"), Query.EQ("Invalid", false)));
            foreach (var bot in bots)
            {
                Bot exist = null;
                if (!Bots.TryGetValue(bot.Id, out exist))
                {
                    log.Debug("BOT ADDED [" + bot.Id + "]" + " [" + bot.Username + "]");
                    Bots[bot.Id] = bot;
                }
                else if (exist.Username != bot.Username || exist.Password != bot.Password)
                {
                    log.Debug("BOT UPDATE USERNAME ["+exist.Username+"] => ["+bot.Username+"] PASSWORD ["+exist.Password+"] => ["+bot.Password+"]");
                    Bots[bot.Id] = bot;
                }
            }
            foreach (var bot in Bots.Values.Where(bot => bots.All(m => m.Id != bot.Id)))
            {
                Bot outBot;
                Bots.TryRemove(bot.Id, out outBot);
                log.Debug("BOT REMOVED/INVALID ["+bot.Id+"] ["+bot.Username+"]");
            }
        }
    }
}