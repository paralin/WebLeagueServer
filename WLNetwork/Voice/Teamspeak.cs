using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KellermanSoftware.CompareNetObjects;
using log4net;
using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;
using TentacleSoftware.TeamSpeakQuery;
using TentacleSoftware.TeamSpeakQuery.NotifyResult;
using TentacleSoftware.TeamSpeakQuery.ServerQueryResult;
using WLNetwork.Database;
using WLNetwork.Matches;
using WLNetwork.Model;
using XSockets.Core.XSocket.Helpers;
using Timer = System.Timers.Timer;

namespace WLNetwork.Voice
{
    /// <summary>
    /// A teamspeak controller instance.
    /// </summary>
    public class Teamspeak
    {
        private static Controllers.Matches Matches = new Controllers.Matches();
        private static Controllers.Chat Chats = new Controllers.Chat();

        private static readonly ILog log = LogManager.GetLogger
            (MethodBase.GetCurrentMethod().DeclaringType);
        public static Teamspeak Instance = null;

        public ConcurrentDictionary<string, ChannelInfoResult> Channels;

        ServerQueryClient client;

        private bool connected = false;

        public Dictionary<string, User> UserCache;
        private Dictionary<uint, string> ServerGroupCache;
        private WhoAmIResult me;

        /// <summary>
        /// Force users with this ID to this channel name
        /// </summary>
        public ConcurrentDictionary<string, string> ForceChannel = new ConcurrentDictionary<string, string>();

        public Teamspeak()
        {
            Instance = this;
            Channels = new ConcurrentDictionary<string, ChannelInfoResult>();
            UserCache = new Dictionary<string, User>();
            ServerGroupCache = new Dictionary<uint, string>();
            RegisterDefaultChannels();
        }

        private async Task Periodic()
        {
            try
            {
                MatchGame game = null;
                if (MatchGame.TSSetupQueue.TryDequeue(out game) && game != null) await game.CreateTeamspeakChannels();

                await SetupChannels();
                await CheckClients();
            }
            catch (Exception ex)
            {
                log.Warn("Error in periodic update.", ex);
            }
        }

        public async Task Startup()
        {
            await InitClient();
        }

        private bool closed = true;
        private int thid = 0;

        private async Task InitClient()
        {
            if (!Env.ENABLE_TEAMSPEAK)
            {
                log.Warn("Teamspeak integration disabled, not starting client.");
                return;
            }

            connected = false;
            string[] parts = Env.TEAMSPEAK_URL.Split(':');
            int port = 10011;
            if (parts.Length >= 2) port = int.Parse(parts[1]);
            
            log.Debug("Initializing teamspeak, connecting to "+Env.TEAMSPEAK_URL+"...");
            
            client = new ServerQueryClient(parts[0], port, TimeSpan.FromMilliseconds(50));
            client.ConnectionClosed += ConnectionClosed;
            client.NotifyTextMessage += NotifyTextMessage;

            ServerQueryBaseResult connectedr = client.Initialize().Result;

            if (!connectedr.Success)
            {
                log.Debug("Can't connect to teamspeak, trying again in 1 minute.");
                Shutdown();
                ThreadPool.QueueUserWorkItem(state =>
                {
                    Thread.Sleep(TimeSpan.FromMinutes(1));
                    Startup();
                });
                return;
            }

            parts = Env.TEAMSPEAK_AUTH.Split(':');
            if (parts.Length != 2)
            {
                log.Warn("Invalid teamspeak auth detected, "+Env.TEAMSPEAK_AUTH+", not starting ts.");
                return;
            }

            ServerQueryBaseResult login;
            try
            {
                login = client.Login(parts[0], parts[1]).Result;
            }
            catch (Exception ex)
            {
                log.Error("Error authenticating with teamspeak! Trying again in 1 minute...", ex);
                Shutdown();
                ThreadPool.QueueUserWorkItem(state =>
                {
                    Thread.Sleep(TimeSpan.FromMinutes(1));
                    Startup();
                });
                return;
            }
            if (login.Success)
            {
                log.Debug("Successfully authed to Teamspeak.");
            }
            else
            {
                log.Warn("Auth failed to teamspeak, "+login.ErrorId+" "+login.ErrorMessage);
                log.Warn("Not trying to connect to teamspeak again.");
                return;
            }

            ServerQueryBaseResult use = client.Use(UseServerBy.Port, Env.TEAMSPEAK_PORT).Result;

            if (use.Success)
            {
                log.Debug("Successfully bound to teamspeak server.");
            }
            else
            {
                log.Debug("Can't select teamspeak server, trying again in 1 minute.");
                Shutdown();
                ThreadPool.QueueUserWorkItem(state =>
                {
                    Thread.Sleep(TimeSpan.FromMinutes(1));
                    Startup();
                });
                return;
            }

            var res = client.SendCommandAsync("instanceedit serverinstance_serverquery_flood_commands=50").Result;
            if (!res.Success)
            {
                log.Warn("Unable to change query flood params, we could max out the server. "+res.ErrorMessage);
            }

            var sgres = client.SendCommandAsync("servergrouplist").Result;
            if (!res.Success)
            {
                log.Warn("Unable to fetch server group list.");
            }
            else
            {
                foreach (var g in sgres.Response.Split('|').Where(m=>m.Contains("type=1")))
                {
                    var namer = Regex.Match(g, "(name=)([^\\s]+)");
                    if (namer.Success)
                    {
                        var idr = Regex.Match(g, "(sgid=)(\\d+)");
                        if (idr.Success)
                        {
                            if (namer.Groups.Count < 3)
                            {
                                log.Warn("Invalid regex match for sg "+g+"!");
                                continue;
                            }
                            var groupn = namer.Groups[2].Value;
                            var idn = idr.Groups[2].Value;
                            ServerGroupCache[uint.Parse(idn)] = groupn.Unescape();
                            log.Debug("Server group "+groupn+"="+idn+".");
                        }
                    }
                }
            }

            client.KeepAlive(TimeSpan.FromSeconds(20));

            await client.ClientUpdate("FPL Server");
            me = await client.WhoAmI();

            foreach (var eve in new[] {Event.channel, Event.server, Event.textchannel, Event.textprivate, Event.textserver})
                await client.ServerNotifyRegister(eve, 0);

            log.Debug("Server ready, configuring...");
            connected = true;

            try
            {
                await SetupChannels();
            }
            finally
            {
                closed = false;
                thid++;
                ThreadPool.QueueUserWorkItem(PeriodicUpdate);
            }
        }

        private async void PeriodicUpdate(object state)
        {
            int ourid = thid;
            while (!closed && ourid == thid)
            {
                await Periodic();
                Thread.Sleep(1000);
            }
            log.Debug("Teamspeak thread "+ourid+" quit.");
        }

        private void Shutdown()
        {
            connected = false;
            client.Quit();
            client = null;
        }

        private void NotifyTextMessage(object sender, NotifyTextMessageResult notifyTextMessageResult)
        {
            if (int.Parse(notifyTextMessageResult.Invokerid) == me.ClientId || notifyTextMessageResult.Targetmode != "1") return;
            log.Debug("Message => "+notifyTextMessageResult.Invokerid+"="+notifyTextMessageResult.Invokername+": "+notifyTextMessageResult.Msg);
            var user = Mongo.Users.FindOneAs<User>(Query<User>.EQ(m => m.tsonetimeid, notifyTextMessageResult.Msg));
            if(user == null)
                client.SendTextMessage(TextMessageTargetMode.TextMessageTarget_CLIENT, int.Parse(notifyTextMessageResult.Invokerid), "Your token, "+notifyTextMessageResult.Msg+" is not recognized. Please try again.");
            else
            {
                client.SendTextMessage(TextMessageTargetMode.TextMessageTarget_CLIENT, int.Parse(notifyTextMessageResult.Invokerid), "Thank you, "+user.profile.name+", welcome to the server.");
                try
                {
                    Task.Run(async () =>
                    {
                        var usr = await client.ClientInfo(int.Parse(notifyTextMessageResult.Invokerid));
                        foreach (var e in UserCache.Keys.Where(m => UserCache[m] != null && UserCache[m].Id == user.Id).ToArray())
                            UserCache.Remove(e);
                        //if (user.tsuniqueids == null) FOR NOW JUST ALLOW ONE TS AUTH AT ONCE
                            user.tsuniqueids = new string[0];
                        user.tsuniqueids = new List<string>(user.tsuniqueids) { usr.ClientUniqueIdentifier }.ToArray();
                        user.tsonetimeid = null; //it will be regenned later
                        Mongo.Users.Update(Query<User>.EQ(m => m.Id, user.Id),
                            Update<User>.Set(m => m.tsuniqueids, user.tsuniqueids).Set(m=>m.tsonetimeid, null));
                        foreach (var u in Matches.Find(m => m.User != null && m.User.Id == user.Id)) u.ReloadUser();
                        foreach (var u in Chats.Find(m => m.User != null && m.User.Id == user.Id)) u.ReloadUser();
                        UserCache[usr.ClientUniqueIdentifier] = user;
                        await CheckClients();
                    });
                }
                catch (Exception ex)
                {
                    log.Error("Error when recognizing signed in user: ", ex);
                }
            }
        }

        private void ConnectionClosed(object sender, EventArgs eventArgs)
        {
            connected = false;
            log.Warn("Disconnected from teamspeak, trying to connect again...");
            Task.Run(() =>
            {
                Shutdown();
                Startup();
            });
        }

        private void RegisterDefaultChannels()
        {
            Channels["Lobby"] = new ChannelInfoResult()
            {
                 ChannelCodecQuality = "10",
                 ChannelName = "Lobby",
                 ChannelFlagDefault = "1",
                 ChannelFlagPermanent = "1",
                 ChannelDescription = "General chat."
            };
            Channels["AFK"] = new ChannelInfoResult()
            {
                ChannelCodecQuality = "1",
                ChannelName = "AFK",
                ChannelDescription = "Channel for afk players.",
                ChannelFlagPermanent = "1",
                ChannelForcedSilence = "1",
                ChannelNeededTalkPower = "99999"
            };
            Channels["Unknown"] = new ChannelInfoResult()
            {
                ChannelCodecQuality = "1",
                ChannelName = "Unknown",
                ChannelDescription = "Channel for clients that have not verified their identity yet.",
                ChannelFlagPermanent = "1",
                ChannelForcedSilence = "1",
                ChannelNeededTalkPower = "99999",
                ChannelPassword = "dontjoinmanuallyscrub"
            };
            Channels["[spacer0]"] = new ChannelInfoResult()
            {
                ChannelName = "[spacer0]",
                ChannelFlagPermanent = "1",
                //ChannelNeededTalkPower = "9999",
                ChannelMaxclients = "0",
                ChannelFlagMaxclientsUnlimited = "0"
            };
        }

        private string[] ObjectToArgs(object obj, string[] props=null)
        {
            var chain = obj.GetType()
                .GetProperties()
                .Where(prop => prop.Name != "Success");
            if(props != null) chain = chain.Where(m=>props.Contains(m.Name));
            return chain
                .Select(prop => new {prop, val = prop.GetValue(obj)})
                .Where(@t => @t.val != null && (!(@t.val is int) || (int) @t.val != 0))
                .Select(
                    @t =>
                        new
                        {
                            @t, name = string.Concat( @t.prop.Name.Select( (x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower().Replace("pid", "cpid")
                        })
                .Select(@t => @t.name + "=" + @t.@t.val.ToString().Escape()).ToArray();
        }

        private async Task<TextResult> SendCommandAsync(string command)
        {
            log.Debug("Sending command "+command);
            return await client.SendCommandAsync(command);
        }

        private Dictionary<string, ClientInfoResult> ClientCache = new Dictionary<string, ClientInfoResult>(); 
        public async Task CheckClients()
        {
            if (!connected) return;

            ClientListResult clientsr = null;
            try
            {
                clientsr = await client.ClientList();
            }
            catch (Exception ex)
            {
                log.Error("Unable to get clients list", ex);
                return;
            }
            if (!clientsr.Success)
            {
                log.Warn("Unable to list clients, " + clientsr.ErrorMessage + "!");
                return;
            }

            foreach (var clii in clientsr.Values)
            {
                if (clii.ClientType != "0") continue;

                ClientInfoResult cli = null;
                if (!ClientCache.TryGetValue(clii.ClientUniqueIdentifier, out cli))
                {
                    var val = await client.ClientInfo(clii.ClientId);
                    cli = val;
                    //ClientCache[clii.ClientUniqueIdentifier] = val;
                    cli.ClientId = clii.ClientId;
                }

                User user = null;
                if (cli.ClientUniqueIdentifier != null)
                {
                    if (!UserCache.TryGetValue(cli.ClientUniqueIdentifier, out user) || user == null)
                        user =
                            Mongo.Users.FindOneAs<User>(Query<User>.EQ(m => m.tsuniqueids, cli.ClientUniqueIdentifier));

                    UserCache[cli.ClientUniqueIdentifier] = user;
                }

                var targetGroups = new List<uint>();
                if(user == null) targetGroups.Add(ServerGroupCache.First(m=>m.Value == "Guest").Key);
                else if (user.authItems.Contains("admin")) targetGroups.Add(ServerGroupCache.First(m => m.Value == "Server Admin").Key);
                else if (user.vouch != null) targetGroups.Add(ServerGroupCache.First(m => m.Value == "Normal").Key);
                else targetGroups.Add(ServerGroupCache.First(m => m.Value == "Guest").Key);

                var ids = cli.ClientServerGroups.Split(',').Where(m => m.Length > 0).Select(uint.Parse).ToList();
                foreach (var id in ids.Where(m => !targetGroups.Contains(m)))
                {
                    log.Debug("Removing server group "+id+" from client "+cli.ClientNickname);
                    await SendCommandAsync("servergroupdelclient sgid=" + id + " cldbid=" + cli.ClientDatabaseId);
                }
                foreach (var id in targetGroups.Where(m => !ids.Contains(m)))
                {
                    log.Debug("Adding server group "+ServerGroupCache[id]+" to client "+cli.ClientNickname);
                    await SendCommandAsync("servergroupaddclient sgid=" + id + " cldbid=" + cli.ClientDatabaseId);
                }

                var uchan = Channels["Unknown"];
                if (user == null)
                {
                    if (uchan.Cid != cli.ChannelId && uchan.Cid != 0)
                    {
                        log.Debug("Moving client " + cli.ClientNickname + " to unknown channel, id: " + uchan.Cid +
                                  " from " + cli.ChannelId + ".");
                        var res = await SendCommandAsync("clientmove cid=" + uchan.Cid + " clid=" + cli.ClientId);
                        if (!res.Success)
                        {
                            log.Warn("Unable to move client, " + res.ErrorMessage);
                        }
                        else
                        {
                            cli.ChannelId = uchan.Cid;
                            await
                                client.SendTextMessage(TextMessageTargetMode.TextMessageTarget_CLIENT,
                                    cli.ClientId,
                                    "Welcome to the FPL teamspeak. Please paste your client token here. If you don't know what it is, click your name at the top right of the site and select Teamspeak Info.");

                        }
                    }
                    else if (!checkedunknown)
                    {
                        await client.SendTextMessage(TextMessageTargetMode.TextMessageTarget_CLIENT,
                            cli.ClientId,
                            "Welcome to the FPL teamspeak. Please paste your client token here. If you don't know what it is, click your name at the top right of the site and select Teamspeak Info.");
                    }
                    continue;
                }
                else
                {
                    string fc;
                    if (ForceChannel.TryGetValue(user.steam.steamid, out fc) && fc != null)
                    {
                        ChannelInfoResult chan = null;
                        if (Channels.TryGetValue(fc, out chan) && chan == null)
                        {
                            if (chan.Cid != 0 && cli.ChannelId != chan.Cid)
                            {
                                log.Debug("Moving client "+cli.ClientNickname+" into forced channel "+chan.ChannelName+".");
                                await SendCommandAsync("clientmove cid=" + chan.Cid + " clid=" + cli.ClientId);
                            }
                        }
                    }
                    else if (uchan.Cid != 0 && cli.ChannelId == uchan.Cid)
                    {
                        log.Debug("Moving client "+cli.ClientNickname+" out of the unknown channel.");
                        await SendCommandAsync("clientmove cid=" + Channels["Lobby"].Cid + " clid=" + cli.ClientId);
                    }
                }
            }
            checkedunknown = true;
        }

        private bool checkedunknown = false;
        private ConcurrentDictionary<string, ChannelInfoResult> ChannelCache = new ConcurrentDictionary<string, ChannelInfoResult>(); 
        public async Task SetupChannels()
        {
            if (!connected) return;

            var channelsr = await client.ChannelList();
            if (!channelsr.Success)
            {
                log.Warn("Unable to list channels, "+channelsr.ErrorMessage+"!");
                return;
            }

            var channels = channelsr.Values;
            var compare = new CompareLogic();

            // Create new channels
            foreach (var channel in Channels.Values)
            {
                var exist = channels.FirstOrDefault(m => m.ChannelName == channel.ChannelName);
                if (exist != null)
                {
                    channel.Cid = exist.Cid;
                    ChannelInfoResult lookupr = null;
                    if (!ChannelCache.TryGetValue(channel.ChannelName, out lookupr) || lookupr == null)
                    {
                        lookupr = await client.ChannelInfo(exist.Cid);
                        ChannelCache[channel.ChannelName] = lookupr;
                    }

                    // Compare the two
                    var comp = compare.Compare(lookupr, channel);
                    if (channel.ChannelFlagPassword == null) channel.ChannelFlagPassword = "0";
                    if (comp.Differences.Any(m => m.PropertyName != ".Cid" && m.PropertyName != ".ChannelPassword"))
                    {
                        string[] props = comp.Differences.Select(m => m.PropertyName.Substring(1)).ToArray();
                        var eres = await SendCommandAsync("channeledit cid="+exist.Cid+" " + string.Join(" ", ObjectToArgs(channel, props)));
                        ChannelInfoResult bogus;
                        ChannelCache.TryRemove(channel.ChannelName, out bogus);
                        if (eres.Success)
                        {
                            log.Debug("Edited channel " + channel.ChannelName + ", updated "+string.Join(", ", props)+".");
                        }
                        else
                        {
                            log.Warn("Unable to edit channel " + channel.ChannelName + ", " + eres.ErrorMessage + "...");
                        }
                    }
                    continue;
                }

                var res = await SendCommandAsync("channelcreate "+string.Join(" ", ObjectToArgs(channel)));
                if (res.Success)
                {
                    log.Debug("Created channel "+channel.ChannelName+".");
                    channelsr = await client.ChannelList();
                    if (!channelsr.Success)
                    {
                        log.Warn("Unable to list channels, " + channelsr.ErrorMessage + "!");
                        return;
                    }

                    channels = channelsr.Values;
                    channel.Cid = channels.First(m => m.ChannelName == channel.ChannelName).Cid;
                }
                else
                {
                    log.Warn("Unable to create channel "+channel.ChannelName+", "+res.ErrorMessage+"...");
                    ChannelInfoResult bogus;
                    if (res.ErrorMessage.Contains("invalid channelID"))
                        Channels.TryRemove(channel.ChannelName, out bogus);
                }
            }

            channelsr = await client.ChannelList();
            if (!channelsr.Success)
            {
                log.Warn("Unable to list channels, " + channelsr.ErrorMessage + "!");
                return;
            }

            channels = channelsr.Values;

            foreach (var channel in channels)
            {
                if (Channels.Keys.Contains(channel.ChannelName) || (channel.ChannelFlagPermanent == "0" && channel.Pid == 0)) continue;
                var res = await SendCommandAsync("channeldelete force=1 cid=" + channel.Cid);
                ChannelInfoResult bogus;
                ChannelCache.TryRemove(channel.ChannelName, out bogus);
                if (res.Success)
                {
                    log.Debug("Deleted channel " + channel.ChannelName + ".");
                }
                else
                {
                    if (res.ErrorMessage.Contains("invalid channel")) continue;
                    log.Warn("Unable to delete channel " + channel.ChannelName + "! -> "+res.ErrorMessage);
                }
            }
        }
    }
}
