using System.Collections.Generic;
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
using MatchType = WLNetwork.Matches.Enums.MatchType;
using WLNetwork.Rating;

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
        public EMatchResult Result { get; set; }

        /// <summary>
        /// The league ID.
        /// </summary>
        public string League { get; set; }

        /// <summary>
        /// League season ID
        /// </summary>
        public uint LeagueSeason { get; set; }

		/// <summary>
		/// Secondary league season IDs
		/// </summary>
		/// <value>The legaue secondary seasons.</value>
		public uint[] LeagueSecondarySeasons { get; set; }

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
        ///     Match type
        /// </summary>
        public MatchType MatchType { get; set; }

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
		/// Was this game ticketed.
		/// </summary>
		/// <value>The ticket ID.</value>
		public uint TicketId { get; set; }

        /// <summary>
        ///     Match data, we don't care much about this anymore
        /// </summary>
		[Obsolete]
        public CMsgDOTAMatch Match { get; set; }

        /// <summary>
        /// Date match completed
        /// </summary>
        public DateTime MatchCompleted { get; set; }

        /// <summary>
        /// Ended win streaks
        /// </summary>
        public Dictionary<string, uint> EndedWinStreaks { get; set; }

		/// <summary>
		/// Adjusts the result.
		/// </summary>
		/// <param name="newResult">New result.</param>
		public bool AdjustResult(EMatchResult newResult)
		{
			if (LeagueSecondarySeasons == null)
				LeagueSecondarySeasons = new uint[0];

			var seasons = LeagueSecondarySeasons.Concat (new uint[]{ LeagueSeason });
			int ratingChangeDire;
			int ratingChangeRadiant;
			if ((Result == EMatchResult.DireVictory && newResult == EMatchResult.RadVictory) || (Result == EMatchResult.RadVictory && newResult == EMatchResult.DireVictory)) {
				ratingChangeDire = (-RatingDire) + RatingRadiant;
				ratingChangeRadiant = (-RatingRadiant) + RatingDire;

				var rad = RatingRadiant;
				RatingRadiant = RatingDire;
				RatingDire = rad;

				Result = newResult;
				MatchCounted = true;
				RatingCalculator.CalculateRatingDelta(this);

				ApplyToUsers (ratingChangeRadiant, ratingChangeDire, newResult, seasons, false, true, false);
				return true;
			} else if (Result == EMatchResult.Unknown && (newResult == EMatchResult.RadVictory || newResult == EMatchResult.DireVictory)) {
        MatchCounted = true;
        Result = newResult;
				RatingCalculator.CalculateRatingDelta(this);
				ApplyRating(false, seasons, true);
				return true;
			}
			return false;
		}

		private void ApplyToUsers(int ratingRadiant, int ratingDire, EMatchResult result, IEnumerable<uint> seasons, bool punishLeavers = false, bool reverseWL = false, bool changeWinStreak = true)
		{
			foreach (var season in seasons)
			{
				string idstr = League + ":" + season;
				var radUpdate = Update.Inc("profile.leagues." + idstr + ".rating", ratingRadiant);
				var direUpdate = Update.Inc("profile.leagues." + idstr + ".rating", ratingDire);

				if (result == EMatchResult.RadVictory)
				{
					radUpdate = radUpdate.Inc ("profile.leagues." + idstr + ".wins", 1u);
					if(changeWinStreak)
						radUpdate = radUpdate.Inc("profile.leagues." + idstr + ".winStreak", 1u);
					direUpdate = direUpdate.Inc("profile.leagues." + idstr + ".losses", 1u);
					if (changeWinStreak)
						direUpdate = direUpdate.Set ("profile.leagues." + idstr + ".winStreak", 0u);
					if (reverseWL)
						direUpdate.Inc ("profile.leagues." + idstr + ".wins", -1);
				}
				else if (result == EMatchResult.DireVictory)
				{
					radUpdate = radUpdate.Inc("profile.leagues." + idstr + ".losses", 1);
					if (changeWinStreak)
						radUpdate = radUpdate.Set ("profile.leagues." + idstr + ".winStreak", 0u);
					direUpdate =
						direUpdate.Inc ("profile.leagues." + idstr + ".wins", 1);
					if(changeWinStreak)
						direUpdate = direUpdate.Inc("profile.leagues." + idstr + ".winStreak", 1u);
					if (reverseWL)
						direUpdate.Inc ("profile.leagues." + idstr + ".losses", -1);
				}

				lock (Mongo.ExclusiveLock)
				{
					Mongo.Users.Update(
						Query.In("steam.steamid",
							Players.Where(m => m.Team == MatchTeam.Radiant && (!punishLeavers || !m.IsLeaver))
							.Select(m => new BsonString(m.SID))
							.ToArray()),
						radUpdate, UpdateFlags.Multi);
					Mongo.Users.Update(
						Query.In("steam.steamid",
							Players.Where(m => m.Team == MatchTeam.Dire && (!punishLeavers || !m.IsLeaver))
							.Select(m => new BsonString(m.SID))
							.ToArray()),
						direUpdate, UpdateFlags.Multi);
				}
			}
		}

		public void ApplyRating(bool punishLeavers, IEnumerable<uint> seasons, bool ignoreWinStreaks = false)
        {
            if (MatchCounted)
            {
                EndedWinStreaks = new Dictionary<string, uint>();

				if(!ignoreWinStreaks)
                foreach (var plyr in Players.Where(m => m.Team == (Result == EMatchResult.RadVictory ? MatchTeam.Dire : MatchTeam.Radiant) && m.WinStreakBefore > 0))
                    EndedWinStreaks[plyr.SID] = plyr.WinStreakBefore;

                if (EndedWinStreaks.Values.Count > 0)
                {
                    var max = EndedWinStreaks.Max(m => m.Value);
                    if (max >= Settings.Default.MinWinStreakForRating)
                    {
                        StreakEndedRating = (uint)Math.Floor((Math.Log10((max - 2) * 0.02d) + 2.0d) * 10.0d);
                        if (Result == EMatchResult.DireVictory) RatingDire += (int)StreakEndedRating;
                        else RatingRadiant += (int)StreakEndedRating;
                    }
                }


				ApplyToUsers (RatingRadiant, RatingDire, Result, seasons, punishLeavers);

            }

            foreach (var cont in Matches.Find(m => m.User != null && Players.Any(x => x.SID == m.User.steam.steamid)))
                cont.ReloadUser();

            foreach (var cont in Chats.Find(m => m.User != null && Players.Any(x => x.SID == m.User.steam.steamid)))
                cont.ReloadUser();
        }

        public void Save()
        {
            lock (Mongo.ExclusiveLock) Mongo.Results.Save(this);
        }
    }
}
