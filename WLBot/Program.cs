using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WLBot.Client;
using WLBot.Properties;
using WLCommon.Matches;
using WLCommon.Model;
using XSockets.Core.Common.Socket;
using XSockets.Plugin.Framework;

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

            var testBot = new LobbyBot.LobbyBot(new MatchSetupDetails()
            {
                Bot = new Bot()
                {
                    Id = "test",
                    Invalid = false,
                    Password = "ciagnickeyen",
                    Username = "webleague2"
                }
            });
            testBot.Start();

            var client = new WLBotClient("ws://localhost:4502", Settings.Default["BotID"] as string, Settings.Default["BotSecret"] as string);
            client.Start();
            while (!shutdown && !(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter))
            {
                Thread.Sleep(500);
            }
            client.Stop();
        }
    }
}
