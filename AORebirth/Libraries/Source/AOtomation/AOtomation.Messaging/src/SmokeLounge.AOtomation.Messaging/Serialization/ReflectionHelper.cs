// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ReflectionHelper.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the ReflectionHelper type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Serialization
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    public static class ReflectionHelper
    {
        #region Public Methods and Operators

        public static MethodInfo GetMethodInfo<TSource, TSignature>(
            Expression<Func<TSource, TSignature>> lambdaExpression)
        {
            MethodInfo methodInfo;
            if (TryGetMethodInfo(lambdaExpression.Body, out methodInfo))
            {
                return methodInfo;
            }

            throw new InvalidOperationException();
        }

        public static PropertyInfo GetPropertyInfo<TSource>(Expression<Func<TSource, object>> propertyExpression)
        {
            var lambdaExpression = propertyExpression as LambdaExpression;
            if (lambdaExpression == null)
            {
                throw new InvalidOperationException();
            }

            MemberExpression memberExpression;
            var unaryExpression = lambdaExpression.Body as UnaryExpression;
            if (unaryExpression != null)
            {
                memberExpression = unaryExpression.Operand as MemberExpression;
            }
            else
            {
                memberExpression = lambdaExpression.Body as MemberExpression;
            }

            if (memberExpression == null)
            {
                throw new InvalidOperationException();
            }

            var propertyInfo = memberExpression.Member as PropertyInfo;
            if (propertyInfo == null)
            {
                throw new InvalidOperationException();
            }

            return propertyInfo;
        }

        public static bool IsStruct(Type type)
        {
            return type.IsValueType && type.IsPrimitive == false && type.IsEnum == false;
        }

        private static bool TryGetMethodInfo(Expression expression, out MethodInfo methodInfo)
        {
            methodInfo = null;

            var unaryExpression = expression as UnaryExpression;
            if (unaryExpression != null)
            {
                return TryGetMethodInfo(unaryExpression.Operand, out methodInfo);
            }

            var constantExpression = expression as ConstantExpression;
            if (constantExpression != null)
            {
                methodInfo = constantExpression.Value as MethodInfo;
                return methodInfo != null;
            }

            var methodCallExpression = expression as MethodCallExpression;
            if (methodCallExpression != null)
            {
                if (methodCallExpression.Object != null && methodCallExpression.Object.NodeType == ExpressionType.Parameter)
                {
                    methodInfo = methodCallExpression.Method;
                    return true;
                }

                if (methodCallExpression.Object != null && TryGetMethodInfo(methodCallExpression.Object, out methodInfo))
                {
                    return true;
                }

                foreach (var argument in methodCallExpression.Arguments)
                {
                    if (TryGetMethodInfo(argument, out methodInfo))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion
    }
}
