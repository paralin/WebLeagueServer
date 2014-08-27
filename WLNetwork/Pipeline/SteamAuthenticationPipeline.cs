using System;
using System.IdentityModel.Tokens;
using System.Security.Principal;
using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;
using Serilog;
using WLNetwork.Database;
using WLNetwork.Model;
using WLNetwork.Properties;
using XSockets.Core.Common.Protocol;
using XSockets.Core.Common.Socket;
using XSockets.Plugin.Framework.Attributes;

namespace WLNetwork.Pipeline
{
    [Export(typeof(IXSocketAuthenticationPipeline))]
    public class SteamAuthenticationPipeline : IXSocketAuthenticationPipeline
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public IPrincipal GetPrincipal(IXSocketProtocol protocol)
        {
            return protocol.ConnectionContext.User;
        }
    }
}
