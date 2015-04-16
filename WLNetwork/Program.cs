using System;
using System.Diagnostics.Eventing.Reader;
using System.Net.Mime;
using System.Reflection;
using System.Threading;
using log4net;
using log4net.Config;
using WLNetwork.Chat;
using WLNetwork.Matches;
using WLNetwork.Voice;
using XSockets.Core.Common.Socket;
using XSockets.Plugin.Framework;
using WLNetwork.Database;

namespace WLNetwork
{
    internal class Program
    {
        private static readonly ILog log = LogManager.GetLogger
            (MethodBase.GetCurrentMethod().DeclaringType);

        private static volatile bool shutdown;

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException +=
                (sender, eventArgs) => log.Fatal("UNHANDLED EXCEPTION", (Exception) eventArgs.ExceptionObject);
            XmlConfigurator.Configure();
            log.Info("Web League master starting up!");
			//Init database
			log.Info("There are "+HeroCache.Heros.Values.Count+" heros in the system.");
            Console.CancelKeyPress += delegate { shutdown = true; };
            using (var container = Composable.GetExport<IXSocketServerContainer>())
            {
                container.Start();
                log.Debug("Server online and listening.");
                new ChatChannel("main", ChannelType.Public, false, true);
                new Teamspeak().Startup();
                MatchGame.RecoverActiveMatches();
                while (!shutdown && !(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter))
                {
                    Thread.Sleep(500);
                }
            }
        }
    }
}