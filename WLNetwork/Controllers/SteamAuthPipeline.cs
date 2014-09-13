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
            try
            {
                string token = protocol.ConnectionContext.QueryString["token"];
                if (token != null)
                {
                    //Decrypt the token
                    try
                    {
                        string jsonPayload = JWT.JsonWebToken.Decode(token, Settings.Default.AuthSecret);
                        var atoken = JObject.Parse(jsonPayload).ToObject<AuthToken>();
                        //find the user
                        try
                        {
                            var user =
                                Mongo.Users.FindOneAs<User>(Query.And(Query.EQ("_id", atoken._id),
                                    Query.EQ("steam.steamid", atoken.steamid)));
                            if (user != null)
                            {
                                log.Debug("AUTHED [" + protocol.ConnectionContext.PersistentId + "] => ["+user.steam.steamid+"]");
                                protocol.ConnectionContext.User = new GenericPrincipal(new UserIdentity(user),
                                    user.authItems);
                                protocol.ConnectionContext.IsAuthenticated = true;
                                return protocol.ConnectionContext.User;
                            }
                            else
                            {
                                log.Warn("Authentication token valid but no user for " + jsonPayload);
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Warn("Issue authenticating decrypted token " + atoken._id, ex);
                        }
                    }
                    catch (JWT.SignatureVerificationException)
                    {
                        log.Warn("Invalid token.");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Unable to authenticate user", ex);
            }
            protocol.ConnectionContext.User = new GenericPrincipal(new GenericIdentity("AUTHFAIL"), new string[0]);
            protocol.ConnectionContext.IsAuthenticated = false;
            return protocol.ConnectionContext.User;
        }
    }
}
