using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;
using WLNetwork.Database;
using WLNetwork.Model;
using WLNetwork.Properties;
using XSockets.Core.Common.Socket.Attributes;
using XSockets.Core.XSocket;
using XSockets.Core.XSocket.Helpers;

namespace WLNetwork.Controllers
{
    public class Auth : XSocketController
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Auth()
        {
            this.OnOpen += (sender, args) => log.Debug("CONNECTED [" + this.ConnectionContext.PersistentId + "]");
        }

        public User User
        {
            get
            {
                if (!this.ConnectionContext.IsAuthenticated) return null;
                return ((UserIdentity)this.ConnectionContext.User.Identity).User;
            }
        }

        [AllowAnonymous]
        public bool AuthWithToken(AuthTokenString tok)
        {
            string token = tok.token;
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
                        var user = Mongo.Users.FindOneAs<User>(Query.And(Query.EQ("_id", atoken._id), Query.EQ("steam.steamid", atoken.steamid)));
                        if (user != null)
                        {
                            log.Debug("User successfully authed, " + user.steam.steamid);
                            ConnectionContext.User = new GenericPrincipal(new UserIdentity(user),
                                user.authItems);
                            ConnectionContext.IsAuthenticated = true;
                            return true;
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
            return false;
        }
    }
}
