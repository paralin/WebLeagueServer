using System;
using System.Reflection;
using System.Threading;
using log4net;
using log4net.Config;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Hosting;
using Owin;
using WLNetwork.Bots;
using WLNetwork.Chat;
using WLNetwork.Database;
using WLNetwork.Leagues;
using WLNetwork.Matches;

namespace WLNetwork
{
    public class Program
    {
        private static readonly ILog log = LogManager.GetLogger
            (MethodBase.GetCurrentMethod().DeclaringType);

        private static volatile bool shutdown;

        public static void Main()
        {
            XmlConfigurator.Configure();
            //ThreadPool.SetMaxThreads (1500, 1500);
#if !DEBUG
            AppDomain.CurrentDomain.UnhandledException +=
                (sender, eventArgs) => log.Fatal("UNHANDLED EXCEPTION", (Exception) eventArgs.ExceptionObject);
#endif

            log.Info("Web League master starting up!");

            //Init database
            log.Info("There are " + HeroCache.Heros.Values.Count + " heros in the system.");
            log.Info("There are " + MemberDB.Members.Count + " members in the system.");
            log.Info("There are " + BotDB.Bots.Count + " bots in the system.");
            log.Info("There are " + LeagueDB.Leagues.Count + " leagues in the system.");

            Console.CancelKeyPress += delegate { shutdown = true; };

#if DEBUG
            using (WebApp.Start("http://localhost:7028"))
            //using (WebApp.Start("http://*:7028"))
            //using (WebApp.Start("http://192.168.0.105:7028"))
#else
            using (WebApp.Start("http://*:4502"))
#endif
            {
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

    class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseCors(CorsOptions.AllowAll);
            app.MapSignalR();
        }
    }
}