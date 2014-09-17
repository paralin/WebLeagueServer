using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            log.Info("Web League master starting up!");
            Console.CancelKeyPress += delegate
            {
                shutdown = true;
            };
            using (var container = Composable.GetExport<IXSocketServerContainer>())
            {
                container.Start();
                log.Debug("Server online and listening.");
                new ChatChannel("main", ChannelType.Public, false, true);
                while (!shutdown && !(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter))
                {
                    Thread.Sleep(500);
                }
            }
        }
    }
}
