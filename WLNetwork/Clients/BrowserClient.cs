using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Timers;
using JWT;
using log4net;
using Microsoft.AspNet.SignalR.Hubs;
using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;
using WLNetwork.Challenge;
using WLNetwork.Chat;
using WLNetwork.Chat.Enums;
using WLNetwork.Database;
using WLNetwork.Leagues;
using WLNetwork.Matches;
using WLNetwork.Model;
using WLNetwork.Properties;

namespace WLNetwork.Clients
{
    /// <summary>
    ///     Instance of a browser client.
    /// </summary>
    public class BrowserClient
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        ///     Dictionary of all clients.
        /// </summary>
        public static ConcurrentDictionary<string, BrowserClient> Clients =
            new ConcurrentDictionary<string, BrowserClient>();

        /// <summary>
        ///     Clients by Steam ID
        /// </summary>
        //todo: check usage and make sure Groups is used always
        public static ConcurrentDictionary<string, BrowserClient> ClientsBySteamID = new ConcurrentDictionary<string, BrowserClient>();

        /// <summary>
        ///     Timer until the challenge times out.
        /// </summary>
        public readonly Timer ChallengeTimer;

        private readonly bool userped = false;
        private User _user;

        /// <summary>
        ///     Current channels list.
        /// </summary>
        public ObservableCollection<ChatChannel> Channels = new ObservableCollection<ChatChannel>();

        /// <summary>
        ///     All currently connected admin clients.
        /// </summary>
        public dynamic AdminClients => Hubs.Admin.HubContext.Clients.Group(_user.steam.steamid);

        /// <summary>
        ///     All currently connected chat clients.
        /// </summary>
        public dynamic ChatClients => Hubs.Chat.HubContext.Clients.Group(_user.steam.steamid);

        /// <summary>
        ///     All currently connected match clients.
        /// </summary>
        public dynamic MatchClients => Hubs.Matches.HubContext.Clients.Group(_user.steam.steamid);

        /// <summary>
        ///     Current ChatMember match.
        /// </summary>
        private ChatMember member;

        /// <summary>
        ///     Create a new browserclient
        /// </summary>
        /// <param name="user">User</param>
        /// <param name="ctx">Caller context</param>
        private BrowserClient(User user)
        {
            ChallengeTimer = new Timer(2000);
            ChallengeTimer.Elapsed += (sender, args) =>
            {
                if (Challenge != null)
                {
                    Challenge.Discard();
                    BrowserClient tcont;
                    if (ClientsBySteamID.TryGetValue(Challenge.ChallengerSID, out tcont))
                        tcont.ChallengeTimer?.Stop();
                }
                ChallengeTimer.Stop();
            };
            _user = user;
            Channels.CollectionChanged += ChatChannelOnCollectionChanged;
            ReloadUser();
            if (member == null) return;
            member.State = UserState.ONLINE;
            member.StateDesc = "Online";
        }

        /// <summary>
        ///     Gets the local user.
        /// </summary>
        public User User => _user;

        /// <summary>
        ///     The active match the player is in.
        /// </summary>
        public MatchGame Match => MatchesController.Games.FirstOrDefault(m => m.Players.Any(x => x.SID == _user.steam.steamid));

        /// <summary>
        ///     The active challenge the player is in.
        /// </summary>
        public Challenge.Challenge Challenge => ChallengeController.Challenges.Values.FirstOrDefault(m=>m.ChallengedSID == _user.steam.steamid || m.ChallengerSID == _user.steam.steamid);

        /// <summary>
        ///     Build a browserclient from a CallerContext
        /// </summary>
        /// <param name="ctx"></param>
        public static void HandleConnection(HubCallerContext ctx)
        {
            if (Clients.ContainsKey(ctx.ConnectionId)) return;
            log.Debug("CONNECTED [" + ctx.ConnectionId + "]");

            // Check the authentication
            try
            {
                string token = ctx.QueryString["token"];
                string jsonPayload = JsonWebToken.Decode(token, Settings.Default.AuthSecret);
                var atoken = JObject.Parse(jsonPayload).ToObject<AuthToken>();
                try
                {
                    var user =
                        Mongo.Users.FindOneAs<User>(Query.And(Query.EQ("_id", atoken._id),
                            Query.EQ("steam.steamid", atoken.steamid)));
                    if (user != null)
                    {
                        if (user.vouch != null)
                        {
                            log.Debug("AUTHED [" + ctx.ConnectionId + "] => [" + user.steam.steamid + "]");

                            // Check for any existing client to use
                            BrowserClient exist = null;
                            if (!ClientsBySteamID.TryGetValue(user.steam.steamid, out exist))
                                exist = ClientsBySteamID[user.steam.steamid] = new BrowserClient(user);

                            // Register any groups here
                            var match = MatchesController.Games.FirstOrDefault(m=>m.Players.Any(x=>x.SID == user.steam.steamid));
                            if (match != null)
                            {
                                Hubs.Matches.HubContext.Groups.Add(ctx.ConnectionId, exist.Match.Id.ToString());
                                match.TransmitSnapshot();
                            }

                            var chal = ChallengeController.Challenges.Values.FirstOrDefault(m=>m.ChallengerSID == user.steam.steamid || m.ChallengedSID == user.steam.steamid);
                            if (chal != null)
                                Hubs.Matches.HubContext.Groups.Add(ctx.ConnectionId, chal.Id.ToString());
                            Hubs.Matches.HubContext.Clients.Client(ctx.ConnectionId).ChallengeSnapshot(chal);

                            var hubctx = Hubs.Chat.HubContext;
                            foreach (var chan in exist.Channels)
                            {
                                hubctx.Groups.Add(ctx.ConnectionId, chan.Id.ToString());
                                hubctx.Clients.Client(ctx.ConnectionId).ChannelUpdate(chan);
                                League leaguel;
                                if (LeagueDB.Leagues.TryGetValue(chan.Name, out leaguel) && leaguel.MotdMessages != null)
                                    foreach (var msg in leaguel.MotdMessages)
                                        ChatChannel.SystemMessage(leaguel.Name, "MOTD: " + msg, user.steam.steamid);
                            }

                            // Add our new connection ID 
                            Clients[ctx.ConnectionId] = exist;
                            return;
                        }
                        log.Warn("Unvouched user tried to connect.");
                    }
                    else
                    {
                        log.Warn("Authentication token valid but no user for " + jsonPayload);
                    }
                }
                catch (Exception ex)
                {
                    log.Warn("Issue authenticating decrypted token " + atoken._id, ex);
                }
            }
            catch (Exception ex)
            {
                log.Error("Unable to authenticate user", ex);
            }
        }

        /// <summary>
        ///     Called when the client disconnects.
        /// </summary>
        /// <param name="ctx"></param>
        public static void HandleDisconnected(HubCallerContext ctx)
        {
            log.Debug("DISCONNECTED [" + ctx.ConnectionId + "]");
            BrowserClient cli;
            Clients.TryRemove(ctx.ConnectionId, out cli);
        }

        /// <summary>
        ///     Called when a chat channel is added/removed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChatChannelOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (var chan in e.NewItems.OfType<ChatChannel>().ToArray())
                    Hubs.Chat.HubContext.Clients.Group(User.steam.steamid).ChannelUpdate(chan);
            if (e.OldItems != null)
                foreach (var chan in e.OldItems.OfType<ChatChannel>().ToArray())
                    Hubs.Chat.HubContext.Clients.Group(User.steam.steamid).ChannelRemove(chan.Id);
        }

        /// <summary>
        ///     Lookup the ChatMember.
        /// </summary>
        public void ReloadUser()
        {
            if (User == null) return;
            ChatMember oldMember = member;

            ChatMember memb = null;
            if (MemberDB.Members.TryGetValue(User.steam.steamid, out memb) && memb != null)
            {
                member = memb;
            }
            else
            {
                MemberDB.UpdateDB();
                if (!MemberDB.Members.TryGetValue(User.steam.steamid, out memb) || memb == null)
                {
                    log.Warn("Unable to find ChatMember for user " + User.profile.name + "!");
                }
            }
            if (memb != null && oldMember != memb)
            {
                if (oldMember != null) oldMember.PropertyChanged -= MemberPropertyChanged;
                memb.PropertyChanged += MemberPropertyChanged;
            }
            RecheckChats();
        }

        /// <summary>
        ///     Recheck chats
        /// </summary>
        private void RecheckChats()
        {
            if (member?.Leagues == null) return;
            var leagueChans = Channels.Where(m => m.ChannelType == ChannelType.League);
            foreach (var league in leagueChans.ToArray().Where(league => !member.Leagues.Contains(league.Name)))
            {
                lock (league.Members)
                    league.Members.Remove(User.steam.steamid);
                lock (Channels)
                    Channels.Remove(league);
            }
            foreach (var league in member.Leagues.Where(league => Channels.All(m => m.Name != league)))
            {
                lock (Channels)
                {
                    ChatChannel chan = ChatChannel.JoinOrCreate(league, member, ChannelType.League);
                    if (chan != null)
                        Channels.Add(chan);
                    League leaguel;
                    if (LeagueDB.Leagues.TryGetValue(league, out leaguel) && leaguel.MotdMessages != null)
                        foreach (var msg in leaguel.MotdMessages)
                            ChatChannel.SystemMessage(league, "MOTD: " + msg, User.steam.steamid);
                }
            }
        }

        /// <summary>
        ///     Watch changes on the member.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="propertyChangedEventArgs"></param>
        private void MemberPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            RecheckChats();
        }

        /// <summary>
        ///     Try to leave the current match.
        /// </summary>
        public void LeaveMatch()
        {
        }

        /// <summary>
        ///     Force set the user.
        /// </summary>
        /// <param name="user">user object</param>
        public void UpdateUser(User user)
        {
            _user = user;
        }

        /// <summary>
        ///     Deconstruct
        /// </summary>
        ~BrowserClient()
        {
            LeaveMatch();
            if (Challenge != null && !userped)
            {
                BrowserClient tcont;
                if (ClientsBySteamID.TryGetValue(Challenge.ChallengedSID, out tcont))
                    tcont.ChallengeTimer.Stop();
                Challenge.Discard();
            }
            Channels.CollectionChanged -= ChatChannelOnCollectionChanged;
            foreach (var channel in Channels.ToArray())
                channel.Members.Remove(User.steam.steamid);
            Channels.Clear();
            Channels = null;
            ChallengeTimer?.Stop();
            ChallengeTimer?.Close();
            BrowserClient tmpcli;
            if (User != null)
                ClientsBySteamID.TryRemove(User.steam.steamid, out tmpcli);
            if (member != null)
            {
                member.StateDesc = "Offline";
                member.State = UserState.OFFLINE;
            }
            member = null;
            _user = null;
        }
    }
}