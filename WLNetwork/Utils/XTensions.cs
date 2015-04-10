using System;
using System.Linq.Expressions;
using WLNetwork.Model;
using XSockets.Core.Common.Protocol;
using XSockets.Core.Common.Socket;

namespace WLNetwork.Utils
{
    public static class XTensions
    {
        public static ulong ToSteamID64(this uint accountid)
        {
            return (76561197960265728L+(ulong)accountid);
        }

        public static uint ToAccountID(this ulong steamId)
        {
            return (uint)(steamId - 76561197960265728L);
        }

        public static User GetUser(this IXSocketController cont)
        {
            Func<IXSocketController, IConnectionContext> accessor =
                GetFieldAccessor<IXSocketController, IConnectionContext>("ConnectionContext");
            IConnectionContext context = accessor(cont);
            if (!context.IsAuthenticated) return null;
            return ((UserIdentity) context.User.Identity).User;
        }

        public static Func<T, R> GetFieldAccessor<T, R>(string fieldName)
        {
            ParameterExpression param =
                Expression.Parameter(typeof (T), "arg");

            MemberExpression member =
                Expression.Field(param, fieldName);

            LambdaExpression lambda =
                Expression.Lambda(typeof (Func<T, R>), member, param);

            var compiled = (Func<T, R>) lambda.Compile();
            return compiled;
        }
    }
}