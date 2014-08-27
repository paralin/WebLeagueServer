using System;
using System.Threading;
using XSockets.Core.Common.Socket;
using XSockets.Plugin.Framework;

namespace WLNetwork
{
	class Program 
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
			(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

	    private static volatile bool shutdown = false;
		public static void Main (string[] args)
		{
			log4net.Config.XmlConfigurator.Configure ();
			log.Info ("Web League master starting up!");
            Console.CancelKeyPress += delegate
            {
                shutdown = true;
            };
            using (var container = Composable.GetExport<IXSocketServerContainer>())
            {
                container.Start();
                log.Debug("Server online and listening.");
                while (!shutdown && !(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter))
                {
                    Thread.Sleep(500);
                }
            }
		}
	}
}
