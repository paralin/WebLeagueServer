using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Timers;
using log4net;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using WLNetwork.Chat.Methods;
using WLNetwork.Database;
using WLNetwork.Model;
using WLNetwork.Utils;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Chat
{
    /// <summary>
    /// Global store of users and their state
    /// </summary>
    public static class MemberDB
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Controllers.Chat Chat = new Controllers.Chat();

        /// <summary>
        /// Update timer for the DB
        /// </summary>
        public static Timer UpdateTimer;

        /// <summary>
        ///    User  Dictionary
        /// </summary>
        public static ObservableDictionary<string, ChatMember> Members = new ObservableDictionary<string, ChatMember>();

        static MemberDB()
        {
            UpdateTimer = new Timer(15000);
            UpdateTimer.Elapsed += UpdateTimerOnElapsed;

            UpdateDB();
            UpdateTimer.Start();

            Members.CollectionChanged += MembersOnCollectionChanged;
        }

        /// <summary>
        /// Transmit an update when the members change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private static void MembersOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if(args.OldItems != null)
                Chat.InvokeTo(m=>m.User != null, new GlobalMemberRm(args.OldItems.OfType<ChatMember>().Select(m=>m.SteamID).ToArray()), GlobalMemberRm.Msg);
            if(args.NewItems != null)
                Chat.InvokeTo(m=>m.User != null, new GlobalMemberSnapshot(args.NewItems.OfType<ChatMember>().ToArray()), GlobalMemberSnapshot.Msg);
        }

        private static void UpdateTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            UpdateDB();
        }

        private static bool alreadyUpdating = false;

        /// <summary>
        ///     Check for differences in the DB
        /// </summary>
        internal static void UpdateDB()
        {
            if(alreadyUpdating) return;
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

                    var cont = Chat.Find(m => m.User != null && m.User.Id == user.Id).FirstOrDefault();
                    if(cont != null) cont.User = user;
                }
                foreach (ChatMember member in Members.Values.Where(x => users.All(m => m.steam.steamid != x.SteamID)).ToArray())
                {
                    Members.Remove(member.SteamID);
                    log.Debug("MEMBER REMOVED [" + member.SteamID + "] [" + member.Name + "]");

                    // Find any online members with this steam id
                    var memb = Chat.Find(m => m.User != null && m.User.steam.steamid == member.SteamID).FirstOrDefault();
                    if(memb != null) memb.Close();
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
        /// Called when a member's property is changed outside the constructor.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="propertyChangedEventArgs"></param>
        private static void MemberPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            var member = sender as ChatMember;
            if (member == null) return;
            Chat.InvokeTo(m=>m.User != null, new GlobalMemberUpdate(member.SteamID, args.PropertyName, member.GetType().GetProperty(args.PropertyName).GetValue(member)), GlobalMemberUpdate.Msg);
        }
    }
}
