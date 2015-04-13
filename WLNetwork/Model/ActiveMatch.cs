using System;
using MongoDB.Bson.Serialization.Attributes;
using WLNetwork.Matches;

namespace WLNetwork.Model
{
    [BsonIgnoreExtraElements]
    public class ActiveMatch
    {
        /// <summary>
        /// Match ID
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Setup Details
        /// </summary>
        public MatchSetupDetails Details { get; set; }

        /// <summary>
        /// Players
        /// </summary>
        public MatchPlayer[] Players { get; set; }

        /// <summary>
        /// Info
        /// </summary>
        public MatchGameInfo Info { get; set; }
    }
}
