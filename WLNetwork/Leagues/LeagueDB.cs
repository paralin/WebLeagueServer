using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Timers;
using KellermanSoftware.CompareNetObjects;
using log4net;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using WLNetwork.Chat;
using WLNetwork.Chat.Methods;
using WLNetwork.Database;
using WLNetwork.Model;
using WLNetwork.Utils;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Leagues
{
    /// <summary>
    /// Global store of leagues
    /// </summary>
    public static class LeagueDB
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly Controllers.Matches Matches = new Controllers.Matches();

        /// <summary>
        /// Update timer for the DB
        /// </summary>
        public static Timer UpdateTimer;

        /// <summary>
        ///    League Dictionary
        /// </summary>
        public static ObservableDictionary<string, League> Leagues = new ObservableDictionary<string, League>();

        /// <summary>
        /// Any updated this update
        /// </summary>
        private static bool AnyUpdated = false;

        static LeagueDB()
        {
            UpdateTimer = new Timer(10000);
            UpdateTimer.Elapsed += UpdateTimerOnElapsed;

            UpdateDB();
            UpdateTimer.Start();

            Leagues.CollectionChanged += LeaguesOnCollectionChanged;
        }

        /// <summary>
        /// Transmit an update when the leagues change.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private static void LeaguesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            AnyUpdated = true;
        }

        private static void UpdateTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            UpdateTimer.Stop();
            UpdateDB();
            UpdateTimer.Start();
        }

        /// <summary>
        ///     Check for differences in the DB
        /// </summary>
        internal static void UpdateDB()
        {
            AnyUpdated = false;
            MongoCursor<League> leagues = Mongo.Leagues.FindAs<League>(Query.NE("Archived", true));
            CompareLogic logic = new CompareLogic();
            try
            {
                foreach (League league in leagues)
                {
                    League exist = null;
                    if (!Leagues.TryGetValue(league.Id, out exist))
                    {
                        log.Debug("LEAGUE ADDED [" + league.Id + "]" + " [" + league.Name + "]");
                        Leagues[league.Id] = league;
                        AnyUpdated = true;
                    }
                    else
                    {
                        // Check for any changes
                        var res = logic.Compare(league, exist);
                        if (!res.AreEqual)
                        {
                            Leagues[league.Id] = league;
                            log.Debug("LEAGUE UPDATED [" + league.Id + "] " + res.DifferencesString);
                            AnyUpdated = true;
                        }
                    }
                }
                foreach (League league in Leagues.Values.Where(x => leagues.All(m => m.Id != x.Id)))
                {
                    Leagues.Remove(league.Id);
                    log.Debug("LEAGUE REMOVED/ARCHIVED [" + league.Id + "] [" + league.Name + "]");
                    AnyUpdated = true;
                }
            }
            catch (Exception ex)
            {
                log.Error("Mongo connection failure? ", ex);
            }
            if (AnyUpdated)
            {
                Matches.InvokeToAll("refreshleagues");
            }
        }
    }
}
