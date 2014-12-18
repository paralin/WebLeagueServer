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
        private static Controllers.Matches Matches = new Controllers.Matches();
        private static Controllers.Chat Chats = new Controllers.Chat();
        /// <summary>
        /// Match ID
        /// </summary>
        public ulong Id { get; set; }

        /// <summary>
        /// Match outcome
        /// </summary>
        public EMatchOutcome Result { get; set; }

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

        public void ApplyRating()
        {
            if (RatingRadiant != 0)
                Mongo.Users.Update(
                    Query.In("steam.steamid",
                        Players.Where(m => m.Team == MatchTeam.Radiant).Select(m => new BsonString(m.SID)).ToArray()),
                    Update.Inc("profile.rating", RatingRadiant));
            if(RatingDire != 0)
                Mongo.Users.Update(
                    Query.In("steam.steamid",
                        Players.Where(m => m.Team == MatchTeam.Dire).Select(m => new BsonString(m.SID)).ToArray()),
                    Update.Inc("profile.rating", RatingDire));
            foreach (var cont in Matches.Find(m => m.User != null && Players.Any(x => x.SID == m.User.steam.steamid)))
            {
                cont.ReloadUser();
            }
            foreach (var cont in Chats.Find(m => m.User != null && Players.Any(x => x.SID == m.User.steam.steamid)))
            {
                cont.ReloadUser();
            }
        }

        public void Save()
        {
            Mongo.Results.Save(this);
        }
    }
}
