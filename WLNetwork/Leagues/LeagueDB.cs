using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Timers;
using KellermanSoftware.CompareNetObjects;
using log4net;
using MongoDB.Driver.Builders;
using WLNetwork.Chat;
using WLNetwork.Database;
using WLNetwork.Model;
using WLNetwork.Utils;

namespace WLNetwork.Leagues
{
    /// <summary>
    /// Global store of leagues
    /// </summary>
    public static class LeagueDB
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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
            UpdateTimer = new Timer(20000);
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
            League[] leagues;
            var logic = new CompareLogic() { Config = new ComparisonConfig() { MaxDifferences = 100 } };
            try
            {
                lock (Mongo.ExclusiveLock)
                {
                    leagues = Mongo.Leagues.FindAs<League>(Query.NE("Archived", true)).ToArray();
                }
                foreach (var league in leagues)
                {
                    League exist = null;
                    if (!Leagues.TryGetValue(league.Id, out exist))
                    {
                        log.Debug("LEAGUE ADDED [" + league.Id + "]" + " [" + league.Name + "]");

                        //Check for mandatory (code breaking) fields
                        var dirty = false;
                        if (league.SecondaryCurrentSeason == null)
                        {
                            league.SecondaryCurrentSeason = new List<uint>();
                            dirty = true;
                        }
                        if (dirty) Mongo.Leagues.Update(Query<League>.EQ(m => m.Id, league.Id), Update<League>.Set(m => m.SecondaryCurrentSeason, league.SecondaryCurrentSeason));

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
                            if (res.Differences.Any(m => m.PropertyName.Contains("Motd")) && league.MotdMessages != null)
                            {
                                ChatChannel.TransmitMOTD(league.Id, league);
                            }
                            AnyUpdated = true;
                        }
                    }
                }
                foreach (League league in Leagues.Values.Where(x => leagues.All(m => m.Id != x.Id)).ToArray())
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
                Hubs.Matches.HubContext.Clients.All.RefreshLeagues();
            }
        }
    }
}
