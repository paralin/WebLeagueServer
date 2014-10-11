﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Linq;
using WLCommon.Matches.Enums;
using WLCommon.Model;
using WLNetwork.Controllers;
using WLNetwork.Database;
using WLNetwork.Matches;
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

        internal static readonly DotaBot BotController = new DotaBot();

        /// <summary>
        /// Bot Dictionary
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
            UpdateDB();
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
            foreach (var setup in SetupQueue.ToArray())
            {
                var dirty = false;
                var newStatus = MatchSetupStatus.Queue;
                var bot = FindAvailableBot();
                if (bot != null)
                {
                    newStatus = MatchSetupStatus.QueueHost;
                    var cont = BotController.Find(m => m.Ready).OrderByDescending(m => m.Setups.Count).FirstOrDefault();
                    if (cont != null)
                    {
                        lock (setup)
                        {
                            setup.ControllerGuid = cont.PersistentId;
                            setup.Details.Status = MatchSetupStatus.Init;
                            setup.Details.TransmitUpdate();
                            setup.Details.Bot = bot;
                            setup.Details.Bot.InUse = true;
                            SetupQueue.Remove(setup);
                            cont.Setups.Add(setup.Details);
                            return;
                        }
                    }
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
            ProcSetupQueue();
        }
    }
}