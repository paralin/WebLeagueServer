using System;
using System.Collections.Generic;
using System.Linq;
using Dota2.GC.Dota.Internal;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.Builders;
using WLNetwork.Clients;
using WLNetwork.Database;
using WLNetwork.Matches.Enums;
using WLNetwork.Properties;
using WLNetwork.Rating;
using WLNetwork.Chat;
using MatchType = WLNetwork.Matches.Enums.MatchType;

namespace WLNetwork.Matches
{
    [BsonIgnoreExtraElements]
    public class MatchResult
    {
        /// <summary>
        ///     Match ID
        /// </summary>
        public ulong Id { get; set; }

        /// <summary>
        ///     System match ID
        /// </summary>
        /// <value>The match identifier.</value>
        public string MatchId { get; set; }

        /// <summary>
        ///     Match outcome
        /// </summary>
        public EMatchResult Result { get; set; }

        /// <summary>
        ///     The league ID.
        /// </summary>
        public string League { get; set; }

        /// <summary>
        ///     League season ID
        /// </summary>
        public uint LeagueSeason { get; set; }

        /// <summary>
        ///     Secondary league season IDs
        /// </summary>
        /// <value>The legaue secondary seasons.</value>
        public uint[] LeagueSecondarySeasons { get; set; }

        /// <summary>
        ///     A version of MatchPlayer minified
        /// </summary>
        public MatchResultPlayer[] Players { get; set; }

        /// <summary>
        ///     Was the rating delta calculated regularly or not?
        /// </summary>
        public bool MatchCounted { get; set; }

        /// <summary>
        ///     Match type
        /// </summary>
        public MatchType MatchType { get; set; }

        /// <summary>
        ///     Bonus delta for streak ended, already applied to rating
        /// </summary>
        public uint StreakEndedRating { get; set; }

        /// <summary>
        ///     Was this game ticketed.
        /// </summary>
        /// <value>The ticket ID.</value>
        public uint TicketId { get; set; }

        /// <summary>
        ///     Match data, we don't care much about this anymore
        /// </summary>
        [Obsolete]
        public CMsgDOTAMatch Match { get; set; }

        /// <summary>
        ///     Date match completed
        /// </summary>
        public DateTime MatchCompleted { get; set; }

        /// <summary>
        ///     Ended win streaks
        /// </summary>
        public Dictionary<string, uint> EndedWinStreaks { get; set; }

        /// <summary>
        ///     Completely undo all changes made by saving the game
        /// </summary>
        public void VoidGame(EMatchResult nres, uint[] seasons)
        {
            // Reverse rating and don't add w/l but reverse old rating
            ApplyToUsers(Result, seasons, true, false, false, true);

            MatchCounted = false;
            Result = nres;
            StreakEndedRating = 0;
        }

        /// <summary>
        ///     Adjusts the result.
        /// </summary>
        /// <param name="newResult">New result.</param>
        public bool AdjustResult(EMatchResult newResult)
        {
            if (LeagueSecondarySeasons == null)
                LeagueSecondarySeasons = new uint[0];

            var seasons = LeagueSecondarySeasons.Concat(new uint[] {LeagueSeason}).ToArray();
            if ((Result == EMatchResult.DireVictory && newResult == EMatchResult.RadVictory) ||
                (Result == EMatchResult.RadVictory && newResult == EMatchResult.DireVictory))
            {
                VoidGame(EMatchResult.Unknown, seasons);

                Result = newResult;
                MatchCounted = true;
                RatingCalculator.CalculateRatingDelta(this);

                ApplyToUsers(newResult, seasons, false, false, true, false);
                return true;
            }
            else if ((Result == EMatchResult.Unknown || Result == EMatchResult.DontCount) &&
                     (newResult == EMatchResult.RadVictory || newResult == EMatchResult.DireVictory))
            {
                MatchCounted = true;
                Result = newResult;

                RatingCalculator.CalculateRatingDelta(this);

                ApplyRating(seasons, true);
                return true;
            }
            else if ((Result == EMatchResult.DireVictory || Result == EMatchResult.RadVictory) &&
                     (newResult == EMatchResult.DontCount || newResult == EMatchResult.Unknown))
            {
                VoidGame(newResult, seasons);
                return true;
            }
            return false;
        }

        /// <summary>
        ///     Recalculate a match result
        /// </summary>
        public void RecalculateResult()
        {
            if (Result != EMatchResult.DireVictory && Result != EMatchResult.RadVictory) return;

            if (LeagueSecondarySeasons == null)
                LeagueSecondarySeasons = new uint[0];

            var seasons = LeagueSecondarySeasons.Concat(new uint[] {LeagueSeason}).ToArray();
            var res = Result;
            VoidGame(EMatchResult.Unknown, seasons);
            Result = res;
            MatchCounted = true;
            RatingCalculator.CalculateRatingDelta(this);

            ApplyRating(seasons, true);
            Save();
        }

        private void ApplyToUsers(EMatchResult result, uint[] seasons, bool reverseWL = false,
            bool changeWinStreak = true, bool addWL = true, bool reverseRating = false)
        {
            foreach (var player in Players)
            {
                UpdateBuilder update = new UpdateBuilder();
                foreach (var season in seasons)
                {
                    string idstr = League + ":" + season;
                    string lroot = "profile.leagues." + idstr;

                    update = update.Inc(lroot + ".rating", (player.RatingChange*(reverseRating ? -1 : 1)));

                    if ((result == EMatchResult.RadVictory && player.Team == MatchTeam.Radiant) ||
                        (result == EMatchResult.DireVictory && player.Team == MatchTeam.Dire)) // if they won
                    {
                        if (addWL)
                            update = update.Inc(lroot + ".wins", 1u);
                        if (changeWinStreak)
                            update = update.Inc(lroot + ".winStreak", 1u);
                        if (reverseWL)
                            update = update.Inc(lroot + ".wins", -1);
                    }
                    else if ((result == EMatchResult.DireVictory && player.Team == MatchTeam.Radiant) ||
                             (result == EMatchResult.RadVictory && player.Team == MatchTeam.Dire))
                    {
                        if (addWL)
                            update = update.Inc(lroot + ".losses", 1);
                        if (changeWinStreak)
                            update = update.Set(lroot + ".winStreak", 0u);
                        if (reverseWL)
                            update = update.Inc(lroot + ".losses", -1);
                    }
                }
                lock (Mongo.ExclusiveLock)
                {
                    Mongo.Users.Update(
                        Query.EQ("steam.steamid", player.SID),
                        update);
                }
            }
        } 
        public void ApplyRating(uint[] seasons, bool ignoreWinStreaks = false)
        {
            if (MatchCounted)
            {
                EndedWinStreaks = new Dictionary<string, uint>();

                if (!ignoreWinStreaks)
                    foreach (
                        var plyr in
                            Players.Where(
                                m =>
                                    m.Team == (Result == EMatchResult.RadVictory ? MatchTeam.Dire : MatchTeam.Radiant) &&
                                    m.WinStreakBefore > 0))
                        EndedWinStreaks[plyr.SID] = plyr.WinStreakBefore;

                if (EndedWinStreaks.Values.Count > 0 && !ignoreWinStreaks)
                {
                    var max = EndedWinStreaks.Max(m => m.Value);
                    if (max >= Settings.Default.MinWinStreakForRating)
                    {
                        StreakEndedRating = (uint) Math.Floor((Math.Log10((max - 2)*0.02d) + 2.0d)*10.0d);
                        foreach (
                            var player in
                                Players.Where(
                                    m =>
                                        (m.Team == MatchTeam.Dire && Result == EMatchResult.DireVictory) ||
                                        (m.Team == MatchTeam.Radiant && Result == EMatchResult.RadVictory)))
                            player.RatingChange += (int) StreakEndedRating;
                    }
                }

                ApplyToUsers(Result, seasons);
            }

            MemberDB.UpdateDB();
            foreach (var client in Players)
            {
                BrowserClient cli;
                if (!BrowserClient.ClientsBySteamID.TryGetValue(client.SID, out cli)) continue;
                cli.ReloadUser();
            }
        }

        public void Save()
        {
            lock (Mongo.ExclusiveLock) Mongo.Results.Save(this);
        }
    }
}
