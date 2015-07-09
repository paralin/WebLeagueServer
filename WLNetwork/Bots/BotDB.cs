using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Timers;
using log4net;
using MongoDB.Driver;
using WLNetwork.Controllers;
using WLNetwork.Database;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;
using WLNetwork.Model;
using XSockets.Core.XSocket.Helpers;
using MongoDB.Driver.Builders;

namespace WLNetwork.Bots
{
    /// <summary>
    ///     Keeps track of all of the bots.
    /// </summary>
    public static class BotDB
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static Timer UpdateTimer;

        /// <summary>
        ///     Bot Dictionary
        /// </summary>
        public static ConcurrentDictionary<string, Bot> Bots = new ConcurrentDictionary<string, Bot>();

        public static HashSet<MatchSetup> SetupQueue = new HashSet<MatchSetup>();

        static BotDB()
        {
            UpdateTimer = new Timer(15000);
            UpdateTimer.Elapsed += UpdateTimerOnElapsed;
            UpdateDB();
            UpdateTimer.Start();
        }

        private static void UpdateTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            UpdateTimer.Stop();
            UpdateDB();
            UpdateTimer.Start();
        }

        private static Bot FindAvailableBot()
        {
            return Bots.Values.FirstOrDefault(m => !m.Invalid && !m.InUse);
        }

        public static void RegisterSetup(MatchSetup setup)
        {
            SetupQueue.Add(setup);
            ProcSetupQueue();
        }

        public static void ProcSetupQueue()
        {
            foreach (MatchSetup setup in SetupQueue.ToArray())
            {
                bool dirty = false;
                var newStatus = MatchSetupStatus.Queue;
                Bot bot = FindAvailableBot();
                if (bot != null)
                {
                    setup.Details.Status = MatchSetupStatus.Init;
                    setup.Details.TransmitUpdate();
                    setup.Details.Bot = bot;
                    setup.Details.Bot.InUse = true;
                    SetupQueue.Remove(setup);
                    var game = setup.Details.GetGame();
                    if (game != null)
                    {
                        game.SetBotController(new BotController(setup.Details));
                        game.GetBotController().instance.Start();
                    }
                    return;
                }
                if (newStatus != setup.Details.Status)
                {
                    setup.Details.Status = newStatus;
                    dirty = true;
                }
                if (dirty)
                {
                    setup.Details.TransmitUpdate();
                }
            }
        }

        /// <summary>
        ///     Check for differences in the DB
        /// </summary>
        internal static void UpdateDB()
        {
            Bot[] bots;
            lock(Mongo.ExclusiveLock) bots = Mongo.Bots.FindAs<Bot>(Query.Or(Query.NotExists("Invalid"), Query.EQ("Invalid", false))).ToArray();
            try
            {
                foreach (Bot bot in bots)
                {
                    Bot exist = null;
                    if (!Bots.TryGetValue(bot.Id, out exist))
                    {
                        log.Debug("BOT ADDED [" + bot.Id + "]" + " [" + bot.Username + "]");
                        Bots[bot.Id] = bot;
                    }
                    else if (exist.Username != bot.Username || exist.Password != bot.Password)
                    {
                        log.Debug("BOT UPDATE USERNAME [" + exist.Username + "] => [" + bot.Username + "] PASSWORD [" +
                                  exist.Password + "] => [" + bot.Password + "]");
                        Bots[bot.Id] = bot;
                    }
                }
                foreach (Bot bot in Bots.Values.Where(bot => bots.All(m => m.Id != bot.Id)).ToArray())
                {
                    Bot outBot;
                    Bots.TryRemove(bot.Id, out outBot);
                    log.Debug("BOT REMOVED/INVALID [" + bot.Id + "] [" + bot.Username + "]");
                }
                ProcSetupQueue();
            }
            catch (Exception ex)
            {
                log.Error("Mongo connection failure? ", ex);
            }
        }
    }
}
