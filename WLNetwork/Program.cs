using System;
using log4net;
using Castle.Windsor;

namespace WLNetwork
{
	class Program 
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
			(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		public static void Main (string[] args)
		{
			log4net.Config.XmlConfigurator.Configure ();
			log.Info ("Web League master starting up!");
			log.Debug ("Setting up Castle Windsor...");
			var container = new WindsorContainer ();
			//Install
			//Set up root component
			log.Info ("Disposing app...");
			container.Dispose ();
		}
	}
}
