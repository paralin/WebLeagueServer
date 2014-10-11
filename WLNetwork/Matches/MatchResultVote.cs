using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using SteamKit2.GC.Dota.Internal;
using WLNetwork.Database;
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
            RadiantWon = Votes.Count(m => m.Value) > Votes.Count(m => !m.Value);
            TransmitUpdate();
            Mongo.Results.Save(this);
        }

        public void Complete()
        {
            if (!VotingOpen) return;
            VoteTimer.Stop();
            VotingOpen = false;
            TransmitUpdate();
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
    }
}
