﻿using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Config;
using Nancy;
using Nancy.Hosting.Self;
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
            ThreadPool.SetMaxThreads (1000, 1000);
            XmlConfigurator.Configure();
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => log.Fatal("UNHANDLED EXCEPTION", (Exception) eventArgs.ExceptionObject);

            log.Info("Web League master starting up!");

            //Init database
            log.Info("There are " + HeroCache.Heros.Values.Count + " heros in the system.");
            Console.CancelKeyPress += delegate { shutdown = true; };


            var container = Composable.GetExport<IXSocketServerContainer>();
            container.Start();
            new ChatChannel("main", ChannelType.Public, false, true);
            log.Debug("Server online and listening.");

            ThreadPool.QueueUserWorkItem((async state =>
            {
                await new Teamspeak().Startup();
                ThreadPool.QueueUserWorkItem((async se => MatchGame.RecoverActiveMatches()));
            }));
            
            while (!shutdown && !(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter))
            {
                Thread.Sleep(500);
            }

            //container.Stop();
            container.Dispose();
        }
    }
}