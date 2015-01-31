using MongoDB.Driver.Linq;
using WLCommon.Model;
using XSockets.Core.Common.Socket.Attributes;
using XSockets.Core.XSocket;

namespace WLNetwork.Controllers
{
    public class WebLeagueController : XSocketController
    {
        public User User
        {
            get
            {
                if (!ConnectionContext.IsAuthenticated) return null;
                return ((UserIdentity)ConnectionContext.User.Identity).User;
            }
            protected set
            {
                if (!ConnectionContext.IsAuthenticated) return;
                ((UserIdentity)ConnectionContext.User.Identity).User = value;
            }
        }

        public override bool OnAuthorization(IAuthorizeAttribute authorizeAttribute)
        {
            if (User == null) return false;
            if (!string.IsNullOrWhiteSpace(authorizeAttribute.Roles))
            {
                string[] roles = authorizeAttribute.Roles.Split(',');
                return User.authItems.ContainsAll(roles);
            }
            return User.steam.steamid == authorizeAttribute.Users;
        }
    }
}
