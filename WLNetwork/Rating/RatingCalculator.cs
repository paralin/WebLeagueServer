using System;
using System.Linq;
using Dota2.GC.Dota.Internal;
using WLNetwork.Chat;
using WLNetwork.Matches;
using WLNetwork.Matches.Enums;

namespace WLNetwork.Rating
{
    public static class RatingCalculator
    {
        /// <summary>
        ///     Base MMR for new players
        /// </summary>
        public const int BaseMmr = 1200;

        /// <summary>
        ///     Minimum MMR archievable by a player
        /// </summary>
        private const int MmrFloor = 100;

        /// <summary>
        ///     Maximum MMR archievable by a player
        /// </summary>
        private const int MmrRoof = 5000;

        /// <summary>
        ///     Factors to calculate MMR after match
        /// </summary>
        private static readonly KFactor[] KFactors =
        {
            new KFactor {MinMmr = MmrFloor, MaxMmr = 2099, Factor = 32},
            new KFactor {MinMmr = 2100, MaxMmr = 3399, Factor = 24},
            new KFactor {MinMmr = 3400, MaxMmr = MmrRoof, Factor = 16}
        };

        /// <summary>
        ///     Calculate the delta MMR for both teams
        /// </summary>
        /// <param name="data"></param>
        public static void CalculateRatingDelta(MatchResult data)
        {
            foreach (MatchResultPlayer plyr in data.Players)
            {
                if (plyr.RatingBefore == 0) plyr.RatingBefore = BaseMmr;
            }

            if (data.Result > EMatchResult.DireVictory ||
                data.Players.Count(m => m.Team == MatchTeam.Dire) == 0 ||
                data.Players.Count(m => m.Team == MatchTeam.Radiant) == 0)
            {
                foreach (var plyr in data.Players) plyr.RatingChange = 0;
                return;
            }

            //avg the MMR
            double radiantAvg = data.Players.Where(m => m.Team == MatchTeam.Radiant).Average(m => m.RatingBefore);
            double direAvg = data.Players.Where(m => m.Team == MatchTeam.Dire).Average(m => m.RatingBefore);

            //calculate probability to win
            double qa = Math.Pow(10, (radiantAvg/400.0));
            double qb = Math.Pow(10, (direAvg/400.0));
            double radiantWinProb = qa/(qa + qb);
            double direWinProb = qb/(qa + qb);

            //get factors for increment or decrement
            KFactor radiantFactor = KFactors.First(a => radiantAvg >= a.MinMmr && radiantAvg <= a.MaxMmr);
            KFactor direFactor = KFactors.First(a => direAvg >= a.MinMmr && direAvg <= a.MaxMmr);

            //calculate the increments and decrements based on win only
            int incRadiant = 0;
            int incDire = 0;
            if (data.Result == EMatchResult.RadVictory)
            {
                incRadiant = (int) Math.Round(radiantFactor.Factor*(1.0 - radiantWinProb));
                incDire = (int) Math.Round(direFactor.Factor*-direWinProb);
            }
            else
            {
                incRadiant = (int) Math.Round(radiantFactor.Factor*-radiantWinProb);
                incDire = (int) Math.Round(direFactor.Factor*(1.0 - direWinProb));
            }

            foreach (var plyr in data.Players.Where(m => m.Team == MatchTeam.Dire)) plyr.RatingChange = incDire;
            foreach (var plyr in data.Players.Where(m => m.Team == MatchTeam.Radiant)) plyr.RatingChange = incRadiant;

#if !DISABLE_SECOND_FACTOR
            //Now apply second factor

            // rating of the first place player
            var plyrs =
                MemberDB.Members.Values.Where(
                    m => m.LeagueProfiles != null && m.LeagueProfiles.ContainsKey(data.League + ":" + data.LeagueSeason)).Select(m=>m.LeagueProfiles[data.League+":"+data.LeagueSeason]).ToArray();

            int elofp = BaseMmr;
            //int eloavg = BaseMmr;
            int elomin = BaseMmr;
            if (plyrs.Any())
            {
                elofp = plyrs.Max(m => m.rating);
                //eloavg = (int)Math.Round(plyrs.Average(m => m.rating));
                elomin = plyrs.Min(m => m.rating);
            }

            var good_guys_win = data.Result == EMatchResult.RadVictory;

            foreach (var plyr in data.Players.Where(m => m.Team == MatchTeam.Dire || m.Team == MatchTeam.Radiant))
            {
                // If they won
                int f2 = (int)Math.Round(((double)(elofp - plyr.RatingBefore))/600.0*8.0);
                if ((plyr.Team == MatchTeam.Dire && !good_guys_win) || (plyr.Team == MatchTeam.Radiant && good_guys_win))
                {
                    double wsf = Math.Min(1.0 + (0.1*((double)plyr.WinStreakBefore)), 1.4);
                    plyr.RatingChange = (int) Math.Round((plyr.RatingChange + f2)*wsf);
                }
                else
                {
                    //plyr.RatingChange = (int)Math.Round(Math.Min(-1.0, f2 + (double)plyr.RatingChange));
                    plyr.RatingChange = (int)Math.Min(-1.0, f2 + plyr.RatingChange);
                }
            }
#endif
        }

        private struct KFactor
        {
            public int MinMmr { get; set; }
            public int MaxMmr { get; set; }
            public int Factor { get; set; }
        }
    }
}
