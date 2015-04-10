using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using SteamKit2;
using SteamKit2.GC.Dota.Internal;
using WLNetwork.Database;
using WLNetwork.Matches.Enums;
using WLNetwork.Model;
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
        ///     Match data
        /// </summary>
        public CMsgDOTAMatch Match { get; set; }

        public void ApplyRating()
        {
            if (MatchCounted)
            {
                var radUpdate = Update<User>.Inc(p => p.profile.rating, RatingRadiant);
                if (Result == EMatchOutcome.k_EMatchOutcome_RadVictory) radUpdate.Inc(p => p.profile.wins, 1);
                else if (Result == EMatchOutcome.k_EMatchOutcome_DireVictory) radUpdate.Inc(p => p.profile.losses, 1);

                var direUpdate = Update<User>.Inc(p => p.profile.rating, RatingDire);
                if (Result == EMatchOutcome.k_EMatchOutcome_RadVictory) direUpdate.Inc(p => p.profile.losses, 1);
                else if (Result == EMatchOutcome.k_EMatchOutcome_DireVictory) direUpdate.Inc(p => p.profile.wins, 1);

                Mongo.Users.Update(
                    Query.In("steam.steamid",
                        Players.Where(m => m.Team == MatchTeam.Radiant).Select(m => new BsonString(m.SID)).ToArray()),
                    radUpdate, UpdateFlags.Multi);
                Mongo.Users.Update(
                    Query.In("steam.steamid",
                        Players.Where(m => m.Team == MatchTeam.Dire).Select(m => new BsonString(m.SID)).ToArray()),
                    direUpdate, UpdateFlags.Multi);
            }
            else if(Result != EMatchOutcome.k_EMatchOutcome_NotScored_NeverStarted && Result != EMatchOutcome.k_EMatchOutcome_NotScored_PoorNetworkConditions && Result != EMatchOutcome.k_EMatchOutcome_NotScored_ServerCrash)
            {
                // Punish the leaver scum!
                foreach (var abandoner in Players.Where(m => m.IsLeaver))
                {
                    Mongo.Users.Update(Query<User>.EQ(m => m.steam.steamid, abandoner.SID),
                        Update<User>.Inc(m => m.profile.abandons, 1).Inc(m => m.profile.rating, -30));
                }
            }

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