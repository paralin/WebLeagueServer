using System;
using System.Security.Principal;
using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;
using WLNetwork.Database;
using WLNetwork.Model;
using WLNetwork.Properties;
using XSockets.Core.Common.Protocol;
using XSockets.Core.Common.Socket;
using XSockets.Plugin.Framework.Attributes;

namespace WLNetwork.Controllers
{
    [Export(typeof(IXSocketAuthenticationPipeline))]
    public class SteamAuthPipeline : IXSocketAuthenticationPipeline
    {
        private static readonly log4net.ILog log =
           log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public IPrincipal GetPrincipal(IXSocketProtocol protocol)
        {
            protocol.ConnectionContext.User = new GenericPrincipal(new GenericIdentity("ANONYMOUS"), new string[0]);
            protocol.ConnectionContext.IsAuthenticated = false;
            return protocol.ConnectionContext.User;
        }
    }
}
