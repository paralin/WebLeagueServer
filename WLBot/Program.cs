using System;
using System.Threading;
using WLBot.Client;
using WLBot.Properties;

namespace WLBot
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static volatile bool shutdown = false;
        public static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            log.Info("Web League bot slave starting up!");
            Console.CancelKeyPress += delegate
            {
                shutdown = true;
            };

            var client = new WLBotClient("ws://wln.paral.in:4502", Settings.Default["BotID"] as string, Settings.Default["BotSecret"] as string);
            client.Start();
            while (!shutdown && !(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter))
            {
                Thread.Sleep(500);
            }
            client.Stop();
        }
    }
}
