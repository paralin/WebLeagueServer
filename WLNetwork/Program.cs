using System;
using System.Reflection;
using System.Threading;
using log4net;
using log4net.Config;
using WLNetwork.Chat;
using WLNetwork.Matches;
using WLNetwork.Voice;
using XSockets.Core.Common.Socket;
using XSockets.Plugin.Framework;

namespace WLNetwork
{
    internal class Program
    {
        private static readonly ILog log = LogManager.GetLogger
            (MethodBase.GetCurrentMethod().DeclaringType);

        private static volatile bool shutdown;

        public static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            log.Info("Web League master starting up!");
            Console.CancelKeyPress += delegate { shutdown = true; };
            using (var container = Composable.GetExport<IXSocketServerContainer>())
            {
                container.Start();
                log.Debug("Server online and listening.");
                new ChatChannel("main", ChannelType.Public, false, true);
                MatchGame.RecoverActiveMatches();
                new Teamspeak().Startup();
                while (!shutdown && !(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter))
                {
                    Thread.Sleep(500);
                }
            }
        }
    }
}