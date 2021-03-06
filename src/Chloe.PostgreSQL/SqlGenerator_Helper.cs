using Chloe.DbExpressions;
using Chloe.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chloe.PostgreSQL
{
    partial class SqlGenerator : DbExpressionVisitor<DbExpression>
    {
        static string GenParameterName(int ordinal)
        {
            if (ordinal < CacheParameterNames.Count)
            {
                return CacheParameterNames[ordinal];
            }

            return UtilConstants.ParameterNamePrefix + ordinal.ToString();
        }
        static string GenRowNumberName(List<DbColumnSegment> columns)
        {
            int ROW_NUMBER_INDEX = 1;
            string row_numberName = "ROW_NUMBER_0";
            while (columns.Any(a => string.Equals(a.Alias, row_numberName, StringComparison.OrdinalIgnoreCase)))
            {
                row_numberName = "ROW_NUMBER_" + ROW_NUMBER_INDEX.ToString();
                ROW_NUMBER_INDEX++;
            }

            return row_numberName;
        }
        public static void AmendDbInfo(DbExpression exp1, DbExpression exp2)
        {
            DbColumnAccessExpression datumPointExp = null;
            DbParameterExpression expToAmend = null;

            DbExpression e = Trim_Nullable_Value(exp1);
            if (e.NodeType == DbExpressionType.ColumnAccess && exp2.NodeType == DbExpressionType.Parameter)
            {
                datumPointExp = (DbColumnAccessExpression)e;
                expToAmend = (DbParameterExpression)exp2;
            }
            else if ((e = Trim_Nullable_Value(exp2)).NodeType == DbExpressionType.ColumnAccess && exp1.NodeType == DbExpressionType.Parameter)
            {
                datumPointExp = (DbColumnAccessExpression)e;
                expToAmend = (DbParameterExpression)exp1;
            }
            else
                return;

            if (datumPointExp.Column.DbType != null)
            {
                if (expToAmend.DbType == null)
                    expToAmend.DbType = datumPointExp.Column.DbType;
            }
        }
        public static void AmendDbInfo(DbColumn column, DbExpression exp)
        {
            if (column.DbType == null || exp.NodeType != DbExpressionType.Parameter)
                return;

            DbParameterExpression expToAmend = (DbParameterExpression)exp;

            if (expToAmend.DbType == null)
                expToAmend.DbType = column.DbType;
        }
        static DbExpression Trim_Nullable_Value(DbExpression exp)
        {
            DbMemberExpression memberExp = exp as DbMemberExpression;
            if (memberExp == null)
                return exp;

            if (memberExp.Member.Name == "Value" && ReflectionExtension.IsNullable(memberExp.Expression.Type))
                return memberExp.Expression;

            return exp;
        }

        public static DbCaseWhenExpression ConstructReturnCSharpBooleanCaseWhenExpression(DbExpression exp)
        {
            // case when 1>0 then 1 when not (1>0) then 0 else Null end
            DbCaseWhenExpression.WhenThenExpressionPair whenThenPair = new DbCaseWhenExpression.WhenThenExpressionPair(exp, DbConstantExpression.True);
            DbCaseWhenExpression.WhenThenExpressionPair whenThenPair1 = new DbCaseWhenExpression.WhenThenExpressionPair(DbExpression.Not(exp), DbConstantExpression.False);
            List<DbCaseWhenExpression.WhenThenExpressionPair> whenThenExps = new List<DbCaseWhenExpression.WhenThenExpressionPair>(2);
            whenThenExps.Add(whenThenPair);
            whenThenExps.Add(whenThenPair1);
            DbCaseWhenExpression caseWhenExpression = DbExpression.CaseWhen(whenThenExps, DbConstantExpression.Null, PublicConstants.TypeOfBoolean);

            return caseWhenExpression;
        }

        static Stack<DbExpression> GatherBinaryExpressionOperand(DbBinaryExpression exp)
        {
            DbExpressionType nodeType = exp.NodeType;

            Stack<DbExpression> items = new Stack<DbExpression>();
            items.Push(exp.Right);

            DbExpression left = exp.Left;
            while (left.NodeType == nodeType)
            {
                exp = (DbBinaryExpression)left;
                items.Push(exp.Right);
                left = exp.Left;
            }

            items.Push(left);
            return items;
        }

        static bool TryGetCastTargetDbTypeString(Type sourceType, Type targetType, out string dbTypeString, bool throwNotSupportedException = true)
        {
            dbTypeString = null;

            sourceType = ReflectionExtension.GetUnderlyingType(sourceType);
            targetType = ReflectionExtension.GetUnderlyingType(targetType);

            if (sourceType == targetType)
                return false;

            if (CastTypeMap.TryGetValue(targetType, out dbTypeString))
            {
                return true;
            }

            if (throwNotSupportedException)
                throw new NotSupportedException(AppendNotSupportedCastErrorMsg(sourceType, targetType));
            else
                return false;
        }
        static string AppendNotSupportedCastErrorMsg(Type sourceType, Type targetType)
        {
            return string.Format("Does not support the type '{0}' converted to type '{1}'.", sourceType.FullName, targetType.FullName);
        }

        public static void DbFunction_DATEADD(SqlGenerator generator, string interval, DbMethodCallExpression exp)
        {
            generator._sqlBuilder.Append("(");
            exp.Object.Accept(generator);
            generator._sqlBuilder.Append(" + ");
            generator._sqlBuilder.Append("make_interval");
            generator._sqlBuilder.Append("(");
            generator._sqlBuilder.Append(interval);
            generator._sqlBuilder.Append(":=");
            exp.Arguments[0].Accept(generator);
            generator._sqlBuilder.Append(")");
            generator._sqlBuilder.Append(")");
        }
        public static void DbFunction_DATEPART(SqlGenerator generator, string interval, DbExpression exp)
        {
            generator._sqlBuilder.Append("CAST(");
            generator._sqlBuilder.Append("DATE_PART(");
            generator._sqlBuilder.Append("'", interval, "'");
            generator._sqlBuilder.Append(",");
            exp.Accept(generator);
            generator._sqlBuilder.Append(")");
            generator._sqlBuilder.Append(" as integer)");
        }
        public static void DbFunction_DATEDIFF(SqlGenerator generator, string interval, DbExpression startDateTimeExp, DbExpression endDateTimeExp)
        {
            generator._sqlBuilder.Append("DATEDIFF(");
            generator._sqlBuilder.Append(interval);
            generator._sqlBuilder.Append(",");
            startDateTimeExp.Accept(generator);
            generator._sqlBuilder.Append(",");
            endDateTimeExp.Accept(generator);
            generator._sqlBuilder.Append(")");
        }

        #region AggregateFunction
        public static void Aggregate_Count(SqlGenerator generator)
        {
            generator._sqlBuilder.Append("COUNT(1)");
        }
        public static void Aggregate_LongCount(SqlGenerator generator)
        {
            generator._sqlBuilder.Append("COUNT(1)");
        }
        public static void Aggregate_Max(SqlGenerator generator, DbExpression exp, Type retType)
        {
            AppendAggregateFunction(generator, exp, retType, "MAX", false);
        }
        public static void Aggregate_Min(SqlGenerator generator, DbExpression exp, Type retType)
        {
            AppendAggregateFunction(generator, exp, retType, "MIN", false);
        }
        public static void Aggregate_Sum(SqlGenerator generator, DbExpression exp, Type retType)
        {
            if (retType.IsNullable())
            {
                AppendAggregateFunction(generator, exp, retType, "SUM", true);
            }
            else
            {
                generator._sqlBuilder.Append("COALESCE(");
                AppendAggregateFunction(generator, exp, retType, "SUM", false);
                generator._sqlBuilder.Append(",");
                generator._sqlBuilder.Append("0");
                generator._sqlBuilder.Append(")");
            }
        }
        public static void Aggregate_Average(SqlGenerator generator, DbExpression exp, Type retType)
        {
            AppendAggregateFunction(generator, exp, retType, "AVG", false);
        }

        static void AppendAggregateFunction(SqlGenerator generator, DbExpression exp, Type retType, string functionName, bool withCast)
        {
            string dbTypeString = null;
            if (withCast == true)
            {
                Type underlyingType = ReflectionExtension.GetUnderlyingType(retType);
                if (CastTypeMap.TryGetValue(underlyingType, out dbTypeString))
                {
                    generator._sqlBuilder.Append("CAST(");
                }
            }

            generator._sqlBuilder.Append(functionName, "(");
            exp.Accept(generator);
            generator._sqlBuilder.Append(")");

            if (dbTypeString != null)
            {
                generator._sqlBuilder.Append(" AS ", dbTypeString, ")");
            }
        }
        #endregion

    }
}
