using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace WLNetwork.Model
{
    /// <summary>
    ///     A user stored in the database, auth through passport.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class User
    {
        public int __v { get; set; }
        public string Id { get; set; }
        public string[] authItems { get; set; }
        public Profile profile { get; set; }
        public SteamService steam { get; set; }
        public Vouch vouch { get; set; }
        public string[] channels { get; set; }
        public string[] tsuniqueids { get; set; }
        public string tsonetimeid { get; set; }
        public bool tsonline { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class Vouch
    {
        public int __v { get; set; }
        public string Id { get; set; }
        public string name { get; set; }
        public string teamname { get; set; }
        public string teamavatar { get; set; }
        public string avatar { get; set; }
        public string[] leagues { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class SteamService
    {
        public string steamid { get; set; }
        public int communityvisibilitystate { get; set; }
        public int profilestate { get; set; }
        public string personaname { get; set; }
        public long lastlogoff { get; set; }
        public int commentpermission { get; set; }
        public string profileurl { get; set; }
        public string avatar { get; set; }
        public string avatarmedium { get; set; }
        public string avatarfull { get; set; }
        public int personastate { get; set; }
        public string realname { get; set; }
        public string primaryclanid { get; set; }
        public long timecreated { get; set; }
        public int personastateflags { get; set; }
        public string gameextrainfo { get; set; }
        public string gameid { get; set; }
        public string loccountrycode { get; set; }
        public string locstatecode { get; set; }
        public int loccityid { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class Profile
    {
        public string name { get; set; }

        /// <summary>
        ///     Leagues. key is leagueid:seasonid
        /// </summary>
        public Dictionary<string, LeagueProfile> leagues { get; set; }
    }

    /// <summary>
    ///     A profile for a certain league
    /// </summary>
    [BsonIgnoreExtraElements]
    public class LeagueProfile
    {
        public int rating { get; set; }
        public int wins { get; set; }
        public int losses { get; set; }
        public int abandons { get; set; }
        public int winStreak { get; set; }
        public int lossStreak { get; set; }
        public DateTime lastGame { get; set; }
        public int decaySinceLast { get; set; }
    }
}