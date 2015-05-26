using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;

namespace WLNetwork.Model
{
    /// <summary>
    /// A league.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class League
    {
        /// <summary>
        /// ID of the league
        /// <example>fplna</example>
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Name of the league, e.g. "
        /// <example>FACEIT Pro League: North America</example>
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Can games be played towards rating for this league
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// If archived, no chat is created and the league is not visible in the UI.
        /// </summary>
        public bool Archived { get; set; }

        /// <summary>
        /// Current season index in the <see cref="Seasons"/> array.
        /// </summary>
        public uint CurrentSeason { get; set; }

        /// <summary>
        /// Seasons
        /// </summary>
        public List<LeagueSeason> Seasons { get; set; }
    }

    /// <summary>
    /// A season in a league.
    /// </summary>
    public class LeagueSeason
    {
        /// <summary>
        /// Name of the season.
        /// <example>Season 1</example>
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Prizepool
        /// <example>20,000</example>
        /// </summary>
        public uint Prizepool { get; set; }

        /// <summary>
        /// Prizepool currency
        /// <example>€</example>
        /// </summary>
        public string PrizepoolCurrency { get; set; }

        /// <summary>
        /// Start date
        /// </summary>
        public DateTime Start { get; set; }

        /// <summary>
        /// End date.
        /// </summary>
        public DateTime End { get; set; }

        /// <summary>
        /// Ticket number/leagueID for this league season.
        /// </summary>
        public uint Ticket { get; set; }
    }
}
