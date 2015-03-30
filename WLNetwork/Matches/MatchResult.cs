using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using SteamKit2.GC.Dota.Internal;
using WLNetwork.Database;
using WLNetwork.Matches.Enums;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Matches
{
    public class MatchResult
    {
        private static readonly Controllers.Matches Matches = new Controllers.Matches();
        private static readonly Controllers.Chat Chats = new Controllers.Chat();

        /// <summary>
        ///     Match ID
        /// </summary>
        public ulong Id { get; set; }

        /// <summary>
        ///     Match outcome
        /// </summary>
        public EMatchOutcome Result { get; set; }

        /// <summary>
        ///     A version of MatchPlayer minified
        /// </summary>
        public MatchResultPlayer[] Players { get; set; }

        /// <summary>
        ///     Rating change for dire
        /// </summary>
        public int RatingDire { get; set; }

        /// <summary>
        ///     Rating change for radiant
        /// </summary>
        public int RatingRadiant { get; set; }

        public void ApplyRating()
        {
            if (RatingRadiant != 0)
                Mongo.Users.Update(
                    Query.In("steam.steamid",
                        Players.Where(m => m.Team == MatchTeam.Radiant).Select(m => new BsonString(m.SID)).ToArray()),
                    Update.Inc("profile.rating", RatingRadiant), UpdateFlags.Multi);
            if (RatingDire != 0)
                Mongo.Users.Update(
                    Query.In("steam.steamid",
                        Players.Where(m => m.Team == MatchTeam.Dire).Select(m => new BsonString(m.SID)).ToArray()),
                    Update.Inc("profile.rating", RatingDire), UpdateFlags.Multi);
            foreach (
                Controllers.Matches cont in
                    Matches.Find(m => m.User != null && Players.Any(x => x.SID == m.User.steam.steamid)))
            {
                cont.ReloadUser();
            }
            foreach (
                Controllers.Chat cont in
                    Chats.Find(m => m.User != null && Players.Any(x => x.SID == m.User.steam.steamid)))
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