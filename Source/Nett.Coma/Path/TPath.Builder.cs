﻿using System;
using System.Collections;
using System.Linq.Expressions;

namespace Nett.Coma.Path
{
    internal sealed partial class TPath
    {
        internal static class Builder
        {
            public static TPath Build(LambdaExpression expression)
            {
                return Build(expression.Body, TomlConfig.DefaultInstance);
            }

            private static TPath Build(Expression expression, TomlConfig config)
            {
                switch (expression)
                {
                    case MemberExpression me:
                        var path = Build(me.Expression, config);
                        return path / GetSegmentFromMemberExpression(me, config);
                    case BinaryExpression be:
                        path = Build(be.Left, config);
                        var seg = GetSegmentFromConstantExpression((ConstantExpression)be.Right);
                        return path / seg;
                    case ParameterExpression pe:
                        return new TPath();
                    default:
                        throw new InvalidOperationException($"TPath cannot be created as expression '{expression}' cannot be handled.");
                }
            }

            private static ITPathSegment GetSegmentFromConstantExpression(ConstantExpression ce)
            {
                return new IndexSegment((int)ce.Value);
            }

            private static ITPathSegment GetSegmentFromMemberExpression(MemberExpression expr, TomlConfig config)
            {
                var targetType = GetTargetType(expr.Type, config);
                return GetSegmentFromTargetType(targetType, expr.Member.Name);
            }

            private static TomlObjectType GetTargetType(Type memberType, TomlConfig config)
            {
                TomlObjectType GetTargetTypeFromArrayType()
                {
                    var et = memberType.GetElementType();
                    if (et != null)
                    {
                        return GetTargetTypeFromElementType(et);
                    }

                    return TomlObjectType.Array;
                }

                TomlObjectType GetTargetTypeFromEnumerableType()
                {
                    if (memberType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(memberType))
                    {
                        var ga = memberType.GetGenericArguments();
                        if (ga.Length == 1)
                        {
                            return GetTargetTypeFromElementType(ga[0]);
                        }
                    }

                    return TomlObjectType.Array;
                }

                TomlObjectType GetTargetTypeFromElementType(Type eleType)
                {
                    var tt = GetTargetType(eleType, config);
                    return tt == TomlObjectType.Table
                        ? TomlObjectType.ArrayOfTables
                        : TomlObjectType.Array;
                }

                var converter = config.TryGetToTomlConverter(memberType);
                if (converter != null)
                {
                    return converter.TomlTargetType.Value;
                }

                if (memberType.IsArray)
                {
                    return GetTargetTypeFromArrayType();
                }
                else if (typeof(IEnumerable).IsAssignableFrom(memberType))
                {
                    return GetTargetTypeFromEnumerableType();
                }

                return TomlObjectType.Table;
            }

            private static ITPathSegment GetSegmentFromTargetType(TomlObjectType targetType, string key)
            {
                switch (targetType)
                {
                    case TomlObjectType.Table: return new TableSegment(key);
                    case TomlObjectType.ArrayOfTables: return new TableArraySegment(key);
                    case TomlObjectType.Array: return new ArraySegment(key);
                    default: return new ValueSegment(targetType, key);
                }
            }
        }
    }
}
