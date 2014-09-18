using System;
using System.Linq.Expressions;
using WLCommon.Model;
using WLNetwork.Model;
using XSockets.Client40.Common.Interfaces;
using XSockets.Core.Common.Protocol;
using XSockets.Core.Common.Socket;

namespace WLNetwork.Utils
{
    public static class XTensions
    {
        public static User GetUser(this IXSocketController cont)
        {
            var accessor = GetFieldAccessor<IXSocketController, IConnectionContext>("ConnectionContext");
            var context = accessor(cont);
            if (!context.IsAuthenticated) return null;
            return ((UserIdentity)context.User.Identity).User;
        }

        public static Func<T, R> GetFieldAccessor<T, R>(string fieldName)
        {
            ParameterExpression param =
            Expression.Parameter(typeof(T), "arg");

            MemberExpression member =
            Expression.Field(param, fieldName);

            LambdaExpression lambda =
            Expression.Lambda(typeof(Func<T, R>), member, param);

            Func<T, R> compiled = (Func<T, R>)lambda.Compile();
            return compiled;
        }
    }
}
