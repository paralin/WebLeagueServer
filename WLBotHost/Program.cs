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

            var client = new WLBotClient("ws://wln.paral.in:4502", Settings.Default["BotID"] as string,
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