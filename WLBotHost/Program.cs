using System;
using System.Reflection;
using System.Threading;
using log4net;
using log4net.Config;
using WLBotHost.Client;
using WLBotHost.Properties;

namespace WLBotHost
{
    internal class Program
    {
        private static readonly ILog log = LogManager.GetLogger
            (MethodBase.GetCurrentMethod().DeclaringType);

        private static volatile bool shutdown;

        public static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            log.Info("Web League bot slave starting up!");
            Console.CancelKeyPress += delegate { shutdown = true; };

            var MASTER_URL = Environment.GetEnvironmentVariable("MASTER_URL");
            if (MASTER_URL == null)
            {
                log.Fatal("Unable to set MASTER_URL");
                return;
            }

            var client = new WLBotClient(MASTER_URL, Settings.Default["BotID"] as string,
                Settings.Default["BotSecret"] as string);
            client.Start();
            while (!shutdown && !(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter))
            {
                Thread.Sleep(500);
            }
            client.Stop();
        }
    }
}