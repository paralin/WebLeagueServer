using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Newtonsoft.Json;
using SteamKit2.GC.Dota.Internal;
using WLCommon.Matches;
using WLCommon.Matches.Enums;
using WLNetwork.Database;
using WLNetwork.Rating;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Matches
{
    public class MatchResult
    {
        private static readonly Controllers.Matches Matches = new Controllers.Matches();

        /// <summary>
        /// Match ID
        /// </summary>
        public ulong Id { get; set; }

        /// <summary>
        /// Has rating applied yet?
        /// </summary>
        public bool RatingApplied { get; set; }

        /// <summary>
        /// Requires voting (automatic result impossible)?
        /// </summary>
        public bool RequiresVote { get; set; }

        /// <summary>
        /// Voting still open?
        /// </summary>
        public bool VotingOpen { get; set; }

        /// <summary>
        /// Match result data
        /// </summary>
        public CMsgDOTAMatch Result { get; set; }

        /// <summary>
        /// Time vote ends
        /// </summary>
        public DateTime VotingEnds { get; set; }

        /// <summary>
        /// Did radiant win?
        /// </summary>
        public bool RadiantWon { get; set; }

        /// <summary>
        /// Votes dict
        /// </summary>
        public Dictionary<string, bool> Votes { get; set; }

        /// <summary>
        /// A version of MatchPlayer minified
        /// </summary>
        public MatchResultPlayer[] Players { get; set; }

        /// <summary>
        /// Rating change for dire
        /// </summary>
        public int RatingDire { get; set; }

        /// <summary>
        /// Rating change for radiant
        /// </summary>
        public int RatingRadiant { get; set; }

            /// <summary>
        /// The timer for voting
        /// </summary>
        [BsonIgnore]
        [JsonIgnore]
        public Timer VoteTimer { get; set; }

        public MatchResult()
        {
            VoteTimer = new Timer(180000);
            VoteTimer.Elapsed += (sender, args) => Complete();
            if(Votes != null) Votes = new Dictionary<string, bool>();
        }

        public void StartVote()
        {
            if (!RequiresVote) return;
            VotingEnds = DateTime.UtcNow + TimeSpan.FromMinutes(3);
            VoteTimer.Stop();
            VoteTimer.Start();
            RatingCalculator.CalculateRatingDelta(this);
            VotingOpen = true;
            TransmitUpdate();
            Mongo.Results.Save(this);
        }

        public void UpdateVote(string steamid, bool radiantwon)
        {
            if (!VotingOpen) return;
            if(Votes == null) Votes = new Dictionary<string, bool>();
            if (Votes.ContainsKey(steamid) && Votes[steamid] == radiantwon) return;
            Votes[steamid] = radiantwon;
            var newWon = Votes.Count(m => m.Value) > Votes.Count(m => !m.Value);
            if (newWon != RadiantWon)
            {
                RadiantWon = newWon;
                RatingCalculator.CalculateRatingDelta(this);
            }
            TransmitUpdate();
            Mongo.Results.Save(this);
        }

        public void ApplyRating()
        {
            if (RatingApplied) return;
            Mongo.Users.Update(
                Query.In("steam.steamid",
                    Players.Where(m => m.Team == MatchTeam.Radiant).Select(m => new BsonString(m.SID)).ToArray()),
                Update.Inc("profile.rating", RatingRadiant));
            Mongo.Users.Update(
                Query.In("steam.steamid",
                    Players.Where(m => m.Team == MatchTeam.Dire).Select(m => new BsonString(m.SID)).ToArray()),
                Update.Inc("profile.rating", RatingDire));
            RatingApplied = true;
            foreach (var cont in Matches.Find(m => m.User!=null && Players.Any(x=>x.SID == m.User.steam.steamid)))
            {
                cont.ReloadUser();
            }
        }

        public void Complete()
        {
            if (!VotingOpen) return;
            RatingCalculator.CalculateRatingDelta(this);
            VoteTimer.Stop();
            VotingOpen = false;
            TransmitUpdate();
            ApplyRating();
            Mongo.Results.Save(this);
        }

        ~MatchResult()
        {
            Complete();
        }

        public void TransmitUpdate()
        {
            foreach (var cont in Matches.Find(m => m.Result == this))
            {
                cont.Result = this;
            }
        }

        public void CheckEarlyComplete()
        {
            if (!Matches.Find(m => m.Result == this).Any())  Complete();
        }
    }
}
