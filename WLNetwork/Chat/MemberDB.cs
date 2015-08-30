using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Timers;
using log4net;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using WLNetwork.Clients;
using WLNetwork.Database;
using WLNetwork.Model;
using WLNetwork.Utils;

namespace WLNetwork.Chat
{
    /// <summary>
    ///     Global store of users and their state
    /// </summary>
    public static class MemberDB
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        ///     Update timer for the DB
        /// </summary>
        public static Timer UpdateTimer;

        /// <summary>
        ///     User  Dictionary
        /// </summary>
        public static ObservableDictionary<string, ChatMember> Members = new ObservableDictionary<string, ChatMember>();

        private static bool alreadyUpdating;

        static MemberDB()
        {
            UpdateTimer = new Timer(15000);
            UpdateTimer.Elapsed += UpdateTimerOnElapsed;

            UpdateDB();
            UpdateTimer.Start();

            Members.CollectionChanged += MembersOnCollectionChanged;
        }

        /// <summary>
        ///     Transmit an update when the members change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private static void MembersOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (args.OldItems != null)
                Hubs.Chat.HubContext.Clients.All.GlobalMemberRemove(
                    args.OldItems.OfType<KeyValuePair<string, ChatMember>>().Select(m => m.Value.SteamID).ToArray());
            if (args.NewItems != null)
                Hubs.Chat.HubContext.Clients.All.GlobalMemberSnapshot(
                    args.NewItems.OfType<KeyValuePair<string, ChatMember>>().Select(m => m.Value).ToArray());
        }

        /// <summary>
        ///     Called periodically.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="elapsedEventArgs"></param>
        private static void UpdateTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            UpdateDB();
        }

        /// <summary>
        ///     Check for differences in the DB
        /// </summary>
        internal static void UpdateDB()
        {
            if (alreadyUpdating) return;
            alreadyUpdating = true;
            UpdateTimer.Stop();
            try
            {
                User[] users;
                lock (Mongo.ExclusiveLock)
                {
                    users = Mongo.Users.FindAs<User>(Query.NE("vouch", BsonNull.Value)).ToArray();
                }
                foreach (User user in users)
                {
                    ChatMember exist = null;
                    if (!Members.TryGetValue(user.steam.steamid, out exist))
                    {
                        log.Debug("MEMBER ADDED [" + user.Id + "]" + " [" + user.profile.name + "]");
                        // todo: avatar override?
                        var memb = Members[user.steam.steamid] = new ChatMember(user);
                        memb.PropertyChanged += MemberPropertyChanged;
                    }
                    else
                    {
                        // Check user and trigger any state updates
                        exist.UpdateFromUser(user);
                    }

                    BrowserClient cli;
                    if (BrowserClient.ClientsBySteamID.TryGetValue(user.Id, out cli))
                    {
                        cli.UpdateUser(user);
                    }
                }
                foreach (
                    ChatMember member in
                        Members.Values.Where(x => users.All(m => m.steam.steamid != x.SteamID)).ToArray())
                {
                    Members.Remove(member.SteamID);
                    log.Debug("MEMBER REMOVED [" + member.SteamID + "] [" + member.Name + "]");

                    // todo: Find any online members with this steam id
                    //BrowserClient memb;
                    //if (!BrowserClient.ClientsBySteamID.TryGetValue(member.SteamID)) continue;
                    //memb.Close();
                }
            }
            catch (Exception ex)
            {
                log.Error("Mongo connection failure? ", ex);
            }
            alreadyUpdating = false;
            UpdateTimer.Start();
        }

        /// <summary>
        ///     Called when a member's property is changed outside the constructor.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="propertyChangedEventArgs"></param>
        private static void MemberPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            var member = sender as ChatMember;
            if (member == null) return;
            Hubs.Chat.HubContext.Clients.All.GlobalMemberUpdate(member.SteamID, args.PropertyName,
                member.GetType().GetProperty(args.PropertyName).GetValue(member));
        }
    }
}