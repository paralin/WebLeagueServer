using System;
using System.Security.Principal;
using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;
using WLCommon.Model;
using WLNetwork.Database;
using WLNetwork.Matches;
using WLNetwork.Model;
using WLNetwork.Properties;
using XSockets.Core.Common.Protocol;
using XSockets.Core.Common.Socket;
using XSockets.Core.XSocket.Helpers;
using XSockets.Plugin.Framework.Attributes;

namespace WLNetwork.Controllers
{
    [Export(typeof(IXSocketAuthenticationPipeline))]
    public class AuthPipeline : IXSocketAuthenticationPipeline
    {
        private static readonly Controllers.Matches Matches = new Controllers.Matches();

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
                string bid = protocol.ConnectionContext.QueryString["bid"];
                string btoken = protocol.ConnectionContext.QueryString["btoken"];
                string bdata = protocol.ConnectionContext.QueryString["bdata"];
                if (btoken != null && bdata != null && bid != null)
                {
                    var bot = Mongo.BotHosts.FindOneAs<BotHost>(Query.EQ("_id", bid));
                    if (bot == null)
                    {
                        log.Warn("BotHost has ID " + bid + " but can't be found in the database.");
                    }
                    else
                    {
                        var data = JWT.JsonWebToken.Decode(btoken, bot.Secret, true);
                        var atoken = JObject.Parse(data).ToObject<BotToken>();
                        if (atoken != null && atoken.Id == bid && atoken.SecretData == bdata)
                        {
                            log.Debug("BOT AUTHED ["+bid+"]");
                            protocol.ConnectionContext.User = new GenericPrincipal(new GenericIdentity("BOT ["+bid+"]"), new[]{"dotaBot"});
                            protocol.ConnectionContext.IsAuthenticated = true;
                            return protocol.ConnectionContext.User;
                        }
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
