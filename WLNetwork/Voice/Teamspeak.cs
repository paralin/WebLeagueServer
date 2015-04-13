using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KellermanSoftware.CompareNetObjects;
using log4net;
using TentacleSoftware.TeamSpeakQuery;
using TentacleSoftware.TeamSpeakQuery.NotifyResult;
using TentacleSoftware.TeamSpeakQuery.ServerQueryResult;
using WLNetwork.Utils;

namespace WLNetwork.Voice
{
    /// <summary>
    /// A teamspeak controller instance.
    /// </summary>
    public class Teamspeak
    {
        private static readonly ILog log = LogManager.GetLogger
            (MethodBase.GetCurrentMethod().DeclaringType);
        public static Teamspeak Instance = null;

        public ObservableDictionary<string, ChannelInfoResult> Channels;

        ServerQueryClient client;

        private bool connected = false;

        public Teamspeak()
        {
            Instance = this;
            Channels = new ObservableDictionary<string, ChannelInfoResult>();
            RegisterDefaultChannels();
        }

        public void Startup()
        {
            Task.Factory.StartNew(InitClient);
        }

        private void InitClient()
        {
            connected = false;
            string[] parts = Env.TEAMSPEAK_URL.Split(':');
            int port = 10011;
            if (parts.Length < 2) port = int.Parse(parts[1]);
            
            log.Debug("Initializing teamspeak, connecting to "+Env.TEAMSPEAK_URL+"...");
            
            client = new ServerQueryClient(parts[0], port, TimeSpan.FromMilliseconds(100));
            client.ConnectionClosed += ConnectionClosed;
            client.NotifyChannelDescriptionChanged += NotifyChannelDescriptionChanged;
            client.NotifyChannelEdited += NotifyChannelEdited;
            client.NotifyClientEnterView += NotifyClientEnterView;
            client.NotifyClientLeftView += NotifyClientLeftView;
            client.NotifyClientMoved += NotifyClientMoved;
            client.NotifyServerEdited += NotifyServerEdited;
            client.NotifyTextMessage += NotifyTextMessage;

            ServerQueryBaseResult connectedr = client.Initialize().Result;

            if (!connectedr.Success)
            {
                log.Debug("Can't connect to teamspeak, trying again in 1 minute.");
                Shutdown();
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(TimeSpan.FromMinutes(1));
                    Startup();
                });
                return;
            }

            parts = Env.TEAMSPEAK_AUTH.Split(':');
            ServerQueryBaseResult login = client.Login(parts[0], parts[1]).Result;
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
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(TimeSpan.FromMinutes(1));
                    Startup();
                });
                return;
            }

            client.KeepAlive(TimeSpan.FromMinutes(1));

            ServerQueryBaseResult notifyRegister = client.ServerNotifyRegister(Event.textchannel).Result;
            if (notifyRegister.Success)
            {
                log.Debug("Registered notify events.");
            }
            else
            {
                log.Warn("Unable to register notify events, some things might be broken.");
            }

            log.Debug("Server ready, configuring...");
            connected = true;
            SetupChannels();
        }

        private void Shutdown()
        {
            connected = false;
            client.Quit();
            client = null;
        }

        private void NotifyTextMessage(object sender, NotifyTextMessageResult notifyTextMessageResult)
        {
            log.Debug("Text message => "+notifyTextMessageResult.Invokername+": "+notifyTextMessageResult.Msg);
        }

        private void NotifyServerEdited(object sender, NotifyServerEditedResult notifyServerEditedResult)
        {
            
        }

        private void NotifyClientMoved(object sender, NotifyClientMovedResult notifyClientMovedResult)
        {
            
        }

        private void NotifyClientLeftView(object sender, NotifyClientLeftViewResult notifyClientLeftViewResult)
        {
            
        }

        private void NotifyClientEnterView(object sender, NotifyClientEnterViewResult notifyClientEnterViewResult)
        {
            
        }

        private void NotifyChannelEdited(object sender, NotifyChannelEditedResult notifyChannelEditedResult)
        {
            
        }

        private void NotifyChannelDescriptionChanged(object sender, NotifyChannelDescriptionChangedResult notifyChannelDescriptionChangedResult)
        {
            
        }

        private void ConnectionClosed(object sender, EventArgs eventArgs)
        {
            connected = false;
            log.Warn("Disconnected from teamspeak, trying to connect again...");
            Shutdown();
            Startup();
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
                            @t,
                            name =
                                string.Concat(
                                    @t.prop.Name.Select(
                                        (x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString()))
                                    .ToLower()
                        })
                .Select(@t => @t.name + "=" + @t.@t.val.ToString().Escape()).ToArray();
        }

        private async Task<TextResult> SendCommandAsync(string command)
        {
            log.Debug("Sending command "+command);
            return await client.SendCommandAsync(command);
        }

        public async void SetupChannels()
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
                    var lookupr = await client.ChannelInfo(exist.Cid);

                    // Compare the two
                    var comp = compare.Compare(lookupr, channel);
                    if (comp.Differences.Any(m => m.PropertyName != ".Cid" && (m.PropertyName != ".ChannelPassword" || ((string)m.Object1Value != "OtycbMEkSzZZvqMxSSZuWKscxUY="))))
                    {
                        string[] props = comp.Differences.Select(m => m.PropertyName.Substring(1)).ToArray();
                        var eres = await SendCommandAsync("channeledit cid="+exist.Cid+" " + string.Join(" ", ObjectToArgs(channel, props)));
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
                if (Channels.Keys.Contains(channel.ChannelName)) continue;
                var res = await SendCommandAsync("channeldelete force=1 cid=" + channel.Cid);
                if (res.Success)
                {
                    log.Debug("Deleted channel " + channel.ChannelName + ".");
                }
                else
                {
                    log.Debug("Unable to delete channel " + channel.ChannelName + "!");
                }
            }
        }
    }
}
