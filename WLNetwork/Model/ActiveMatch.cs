using System;
using MongoDB.Bson.Serialization.Attributes;
using WLNetwork.Matches;

namespace WLNetwork.Model
{
    /// <summary>
    /// A stored match that needs to be recovered.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class ActiveMatch
    {
        /// <summary>
        ///     Match ID
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        ///     Setup Details
        /// </summary>
        public MatchSetupDetails Details { get; set; }

        /// <summary>
        ///     Info
        /// </summary>
        public MatchGameInfo Info { get; set; }
    }
}