using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace WLNetwork.Model
{
    /// <summary>
    ///     A league.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class League
    {
        /// <summary>
        ///     ID of the league
        ///     <example>fplna</example>
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        ///     Name of the league, e.g. "
        ///     <example>FACEIT Pro League: North America</example>
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Can games be played towards rating for this league
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        ///     If archived, no chat is created and the league is not visible in the UI.
        /// </summary>
        public bool Archived { get; set; }

        /// <summary>
        ///     Current season index in the <see cref="Seasons" /> array.
        /// </summary>
        public uint CurrentSeason { get; set; }

        /// <summary>
        ///     Secondary current seasons
        /// </summary>
        public List<uint> SecondaryCurrentSeason { get; set; }

        /// <summary>
        ///     Seasons
        /// </summary>
        public List<LeagueSeason> Seasons { get; set; }

        /// <summary>
        ///     Region
        /// </summary>
        public uint Region { get; set; }

        /// <summary>
        ///     Require teamspeak
        /// </summary>
        public bool RequireTeamspeak { get; set; }

        /// <summary>
        ///     Motd messages
        /// </summary>
        public string[] MotdMessages { get; set; }

        /// <summary>
        ///     Decay settings
        /// </summary>
        public LeagueDecay Decay { get; set; }
    }

    /// <summary>
    ///    League decay settings.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class LeagueDecay
    {
        /// <summary>
        ///     How long until the decay starts, in minutes.
        /// </summary>
        public uint DecayStart { get; set; }

        /// <summary>
        ///     Rate to decay, in pts/hour.
        /// </summary>
        public uint DecayRate { get; set; }
    }

    /// <summary>
    ///     A season in a league.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class LeagueSeason
    {
        /// <summary>
        ///     Name of the season.
        ///     <example>Season 1</example>
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Prizepool currency
        ///     <example>€</example>
        /// </summary>
        public string PrizepoolCurrency { get; set; }

        /// <summary>
        ///     Prizepool distribution
        /// </summary>
        public List<int> PrizepoolDist { get; set; }

        /// <summary>
        ///     Start date
        /// </summary>
        public DateTime Start { get; set; }

        /// <summary>
        ///     End date.
        /// </summary>
        public DateTime End { get; set; }

        /// <summary>
        ///     Ticket number/leagueID for this league season.
        /// </summary>
        public uint Ticket { get; set; }
    }
}
