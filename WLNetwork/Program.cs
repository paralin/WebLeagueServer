using System;
using System.Reflection;
using System.Threading;
using log4net;
using log4net.Config;
using WLNetwork.Bots;
using WLNetwork.Chat;
using WLNetwork.Matches;
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
            XmlConfigurator.Configure();
            ThreadPool.SetMaxThreads (1500, 1500);
#if !DEBUG
            AppDomain.CurrentDomain.UnhandledException +=
                (sender, eventArgs) => log.Fatal("UNHANDLED EXCEPTION", (Exception) eventArgs.ExceptionObject);
#endif

            log.Info("Web League master starting up!");

            //Init database
            log.Info("There are " + HeroCache.Heros.Values.Count + " heros in the system.");
            log.Info("There are " + MemberDB.Members.Count+" members in the system.");
            log.Info("There are " + BotDB.Bots.Count+" bots in the system.");

            Console.CancelKeyPress += delegate { shutdown = true; };
            using (var container = Composable.GetExport<IXSocketServerContainer>())
            {
                container.Start();
                new ChatChannel("main", ChannelType.Public, false, true);
                log.Debug("Server online and listening.");
                ThreadPool.QueueUserWorkItem(se => MatchGame.RecoverActiveMatches());
                while (!shutdown && !(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter))
                {
                    Thread.Sleep(500);
                }
            }
        }
    }
}