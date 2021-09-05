using Chloe.DbExpressions;
using Chloe.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chloe
{
    internal static class Utils
    {
        private static readonly HashSet<Type> ToStringableNumericTypes;

        static Utils()
        {
            ToStringableNumericTypes = new HashSet<Type>();
            ToStringableNumericTypes.Add(typeof(byte));
            ToStringableNumericTypes.Add(typeof(sbyte));
            ToStringableNumericTypes.Add(typeof(short));
            ToStringableNumericTypes.Add(typeof(ushort));
            ToStringableNumericTypes.Add(typeof(int));
            ToStringableNumericTypes.Add(typeof(uint));
            ToStringableNumericTypes.Add(typeof(long));
            ToStringableNumericTypes.Add(typeof(ulong));
            ToStringableNumericTypes.Add(typeof(decimal));
            ToStringableNumericTypes.Add(typeof(float));
            ToStringableNumericTypes.Add(typeof(double));
            ToStringableNumericTypes.TrimExcess();
        }

        public static bool IsToStringableNumericType(Type type)
        {
            type = ReflectionExtension.GetUnderlyingType(type);
            return ToStringableNumericTypes.Contains(type);
        }

        public static bool IsTypeStr(Type type) => type == PublicConstants.TypeOfString;

        public static string GenerateUniqueColumnAlias(DbSqlQueryExpression sqlQuery, string defaultAlias = UtilConstants.DefaultColumnAlias)
        {
            string alias = defaultAlias;
            int i = 0;
            while (sqlQuery.ColumnSegments.Any(a => string.Equals(a.Alias, alias, StringComparison.OrdinalIgnoreCase)))
            {
                alias = defaultAlias + i.ToString();
                i++;
            }

            return alias;
        }

        public static DbJoinType AsDbJoinType(this JoinType joinType)
        {
            switch (joinType)
            {
                case JoinType.InnerJoin:
                    return DbJoinType.InnerJoin;

                case JoinType.LeftJoin:
                    return DbJoinType.LeftJoin;

                case JoinType.RightJoin:
                    return DbJoinType.RightJoin;

                case JoinType.FullJoin:
                    return DbJoinType.FullJoin;

                default:
                    throw new NotSupportedException();
            }
        }

        public static Type GetFuncDelegateType(params Type[] typeArguments)
        {
            int parameters = typeArguments.Length;
            Type funcType = null;
            switch (parameters)
            {
                case 2:
                    funcType = typeof(Func<,>);
                    break;

                case 3:
                    funcType = typeof(Func<,,>);
                    break;

                case 4:
                    funcType = typeof(Func<,,,>);
                    break;

                case 5:
                    funcType = typeof(Func<,,,,>);
                    break;

                case 6:
                    funcType = typeof(Func<,,,,,>);
                    break;

                default:
                    throw new NotSupportedException();
            }

            return funcType.MakeGenericType(typeArguments);
        }

        public static bool IsAutoIncrementType(Type t)
        {
            return t == typeof(Int16) || t == typeof(Int32) || t == typeof(Int64);
        }
    }
}