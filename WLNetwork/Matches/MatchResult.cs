﻿using System.Collections.Generic;
using System.Linq;
using Dota2.GC.Dota.Internal;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using TentacleSoftware.TeamSpeakQuery;
using WLNetwork.Database;
using WLNetwork.Matches.Enums;
using WLNetwork.Model;
using WLNetwork.Properties;
using XSockets.Core.XSocket.Helpers;
using System;
using MongoDB.Bson.Serialization.Attributes;

namespace WLNetwork.Matches
{
    [BsonIgnoreExtraElements]
    public class MatchResult
    {
        private static readonly Controllers.Matches Matches = new Controllers.Matches();
        private static readonly Controllers.Chat Chats = new Controllers.Chat();

        /// <summary>
        ///     Match ID
        /// </summary>
        public ulong Id { get; set; }

        /// <summary>
        /// System match ID
        /// </summary>
        /// <value>The match identifier.</value>
        public string MatchId { get; set; }

        /// <summary>
        ///     Match outcome
        /// </summary>
        public EMatchOutcome Result { get; set; }

        /// <summary>
        ///     A version of MatchPlayer minified
        /// </summary>
        public MatchResultPlayer[] Players { get; set; }

        /// <summary>
        ///    Was the rating delta calculated regularly or not?
        /// </summary>
        public bool MatchCounted { get; set; }

        /// <summary>
        ///     Rating change for dire
        /// </summary>
        public int RatingDire { get; set; }

        /// <summary>
        ///     Rating change for radiant
        /// </summary>
        public int RatingRadiant { get; set; }

        /// <summary>
        ///     Rating change overall
        /// </summary>
        /// <value>The rating delta.</value>
        public int RatingDelta { get; set; }

        /// <summary>
        /// Bonus delta for streak ended, already applied to rating
        /// </summary>
        public uint StreakEndedRating { get; set; }

        /// <summary>
        ///     Match data
        /// </summary>
        public CMsgDOTAMatch Match { get; set; }

        /// <summary>
        /// Date match completed
        /// </summary>
        public DateTime MatchCompleted { get; set; }

        /// <summary>
        /// Ended win streaks
        /// </summary>
        public Dictionary<string, uint> EndedWinStreaks { get; set; }

        public void ApplyRating()
        {
            if (MatchCounted)
            {
                EndedWinStreaks = new Dictionary<string, uint>();

                foreach (var plyr in Players.Where(m => m.Team == (Result == EMatchOutcome.k_EMatchOutcome_RadVictory ? MatchTeam.Dire : MatchTeam.Radiant) && m.WinStreakBefore > 0))
                    EndedWinStreaks[plyr.SID] = plyr.WinStreakBefore;

                if (EndedWinStreaks.Values.Count > 0)
                {
                    var max = EndedWinStreaks.Max(m => m.Value);
                    if (max >= Settings.Default.MinWinStreakForRating)
                    {
                        StreakEndedRating = (uint)Math.Floor((Math.Log10((max-2)*0.02d)+2.0d)*10.0d);
                        if (Result == EMatchOutcome.k_EMatchOutcome_DireVictory) RatingDire += (int)StreakEndedRating;
                        else RatingRadiant += (int)StreakEndedRating;
                    }
                }

                var radUpdate = Update<User>.Inc(p => p.profile.rating, RatingRadiant);
                var direUpdate = Update<User>.Inc(p => p.profile.rating, RatingDire);
                if (Result == EMatchOutcome.k_EMatchOutcome_RadVictory)
                {
                    radUpdate.Inc(p => p.profile.wins, 1).Inc(p => p.profile.winStreak, 1);
                    direUpdate.Set(p=>p.profile.winStreak, 0u).Inc(p => p.profile.losses, 1);
                }
                else if (Result == EMatchOutcome.k_EMatchOutcome_DireVictory)
                {
                    radUpdate.Set(m => m.profile.winStreak, 0u).Inc(p => p.profile.losses, 1);
                    direUpdate.Inc(p => p.profile.wins, 1).Inc(p=>p.profile.winStreak, 1);
                }

                Mongo.Users.Update(
                    Query.In("steam.steamid",
                        Players.Where(m => m.Team == MatchTeam.Radiant && !m.IsLeaver)
                            .Select(m => new BsonString(m.SID))
                            .ToArray()),
                    radUpdate, UpdateFlags.Multi);
                Mongo.Users.Update(
                    Query.In("steam.steamid",
                        Players.Where(m => m.Team == MatchTeam.Dire && !m.IsLeaver)
                            .Select(m => new BsonString(m.SID))
                            .ToArray()),
                    direUpdate, UpdateFlags.Multi);
            }

            Mongo.Users.Update(
                Query.In("steam.steamid",
                    Players.Where(m => m.IsLeaver).Select(m => new BsonString(m.SID)).ToArray()),
                Update<User>.Inc(m => m.profile.abandons, 1).Inc(m => m.profile.rating, -25), UpdateFlags.Multi);

            foreach (var cont in Matches.Find(m => m.User != null && Players.Any(x => x.SID == m.User.steam.steamid)))
                cont.ReloadUser();

            foreach (var cont in Chats.Find(m => m.User != null && Players.Any(x => x.SID == m.User.steam.steamid)))
                cont.ReloadUser();
        }

        public void Save()
        {
            Mongo.Results.Save(this);
        }
    }
}