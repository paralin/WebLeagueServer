﻿using System.Collections.Generic;
using System.Security.Principal;
using MongoDB.Bson.Serialization.Attributes;

namespace WLNetwork.Model
{
    public class UserIdentity : IIdentity
    {
        public UserIdentity(User user)
        {
            IsAuthenticated = true;
            Name = user.profile.name;
            AuthenticationType = "jwt";
            User = user;
        }

        public User User { get; set; }

        public string Name { get; private set; }
        public string AuthenticationType { get; private set; }
        public bool IsAuthenticated { get; private set; }
    }

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
        /// Leagues. key is leagueid:seasonid
        /// </summary>
        public Dictionary<string, LeagueProfile> leagues { get; set; }
    }

    /// <summary>
    /// A profile for a certain league
    /// </summary>
    [BsonIgnoreExtraElements]
    public class LeagueProfile
    {
        public uint rating { get; set; }
        public uint wins { get; set; }
        public uint losses { get; set; }
        public uint abandons { get; set; }
        public uint winStreak { get; set; }
        public uint lossStreak { get; set; }
    }
}