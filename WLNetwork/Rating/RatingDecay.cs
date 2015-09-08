using System;
using System.Linq;
using System.Reflection;
using log4net;
using MongoDB.Driver.Builders;
using WLNetwork.Database;
using WLNetwork.Model;

namespace WLNetwork.Rating
{
    public static class RatingDecay
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        ///     Calculates and applies rating decay for a league.
        ///     Safe to call at any frequency.
        /// </summary>
        public static void CalculateDecay(League league)
        {
            var now = DateTime.UtcNow;
            if (league.Decay == null || !league.IsActive) return;
            var decay = league.Decay;
            var season = league.Seasons[(int) league.CurrentSeason];
            if (season.Start > now) return;

            var pid = league.Id + ":" + league.CurrentSeason;
            var pidk = "profile.leagues." + pid + ".";

            // Find all users with this league
            var users = Mongo.Users.Find(Query.Exists(pidk + "lastGame")).ToArray();
            foreach (var user in users)
            {
                var lprof = user.profile.leagues[pid];
                var decayStart = lprof.lastGame.AddMinutes(decay.DecayStart);
                if (now < decayStart) continue;
                if (decay.LowerThreshold != 0 && lprof.rating <= decay.LowerThreshold) continue;

                // Check how many hours after we are
                // Add 1 hour to immediately take some pts away
                var points =
                    (int) (Math.Floor(((now - decayStart).Add(TimeSpan.FromHours(1))).TotalHours)*decay.DecayRate);

                // Check how many we need to remove (or if negative, add)
                var delta = -(points - lprof.decaySinceLast);
                if (delta == 0) continue;
                log.Debug("Applying decay of " + delta + " to " + user.profile.name + "...");
                var upd = Update.Set(pidk + "decaySinceLast", points)
                    .Inc(pidk + "rating", delta);
                Mongo.Users.Update(Query<User>.EQ(m => m.Id, user.Id), upd);
            }
        }
    }
}