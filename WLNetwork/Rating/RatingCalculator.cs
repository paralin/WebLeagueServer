using System;
using System.Linq;
using SteamKit2.GC.Dota.Internal;
using WLCommon.Matches;
using WLCommon.Matches.Enums;
using WLNetwork.Matches;

namespace WLNetwork.Rating
{
    public static class RatingCalculator
    {
        /// <summary>
        ///     Base MMR for new players
        /// </summary>
        private const int BaseMmr = 1200;

        /// <summary>
        ///     Minimum MMR archievable by a player
        /// </summary>
        private const int MmrFloor = 100;

        /// <summary>
        ///     Maximum MMR archievable by a player
        /// </summary>
        private const int MmrRoof = 5000;

        public const int TEAM_PLAYERS = 3;

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

            if (data.Result > EMatchOutcome.k_EMatchOutcome_DireVictory ||
                data.Players.Count(m => m.Team == MatchTeam.Dire) == 0 ||
                data.Players.Count(m => m.Team == MatchTeam.Radiant) == 0)
            {
                data.RatingDire = 0;
                data.RatingRadiant = 0;
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
            if (data.Result == EMatchOutcome.k_EMatchOutcome_RadVictory)
            {
                incRadiant = (int) Math.Round(radiantFactor.Factor*(1.0 - radiantWinProb));
                incDire = (int) Math.Round(direFactor.Factor*-direWinProb);
            }
            else
            {
                incRadiant = (int) Math.Round(radiantFactor.Factor*-radiantWinProb);
                incDire = (int) Math.Round(direFactor.Factor*(1.0 - direWinProb));
            }

            data.RatingDire = incDire;
            data.RatingRadiant = incRadiant;
        }

        private struct KFactor
        {
            public int MinMmr { get; set; }
            public int MaxMmr { get; set; }
            public int Factor { get; set; }
        }
    }
}