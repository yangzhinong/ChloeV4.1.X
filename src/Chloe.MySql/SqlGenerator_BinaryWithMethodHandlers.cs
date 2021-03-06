using Chloe.DbExpressions;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Chloe.MySql
{
    partial class SqlGenerator : DbExpressionVisitor<DbExpression>
    {
        static Dictionary<MethodInfo, Action<DbBinaryExpression, SqlGenerator>> InitBinaryWithMethodHandlers()
        {
            var binaryWithMethodHandlers = new Dictionary<MethodInfo, Action<DbBinaryExpression, SqlGenerator>>();
            binaryWithMethodHandlers.Add(PublicConstants.MethodInfo_String_Concat_String_String, StringConcat);
            binaryWithMethodHandlers.Add(PublicConstants.MethodInfo_String_Concat_Object_Object, StringConcat);

            var ret = PublicHelper.Clone(binaryWithMethodHandlers);
            return ret;
        }

        static void StringConcat(DbBinaryExpression exp, SqlGenerator generator)
        {
            List<DbExpression> operands = new List<DbExpression>();
            operands.Add(exp.Right);

            DbExpression left = exp.Left;
            DbAddExpression e = null;
            while ((e = (left as DbAddExpression)) != null && (e.Method == PublicConstants.MethodInfo_String_Concat_String_String || e.Method == PublicConstants.MethodInfo_String_Concat_Object_Object))
            {
                operands.Add(e.Right);
                left = e.Left;
            }

            operands.Add(left);

            DbExpression whenExp = null;
            List<DbExpression> operandExps = new List<DbExpression>(operands.Count);
            for (int i = operands.Count - 1; i >= 0; i--)
            {
                DbExpression operand = operands[i];
                DbExpression opBody = operand;
                if (opBody.Type != PublicConstants.TypeOfString)
                {
                    // 需要 cast type
                    opBody = DbExpression.Convert(opBody, PublicConstants.TypeOfString);
                }

                DbExpression equalNullExp = DbExpression.Equal(opBody, PublicConstants.DbConstant_Null_String);

                if (whenExp == null)
                    whenExp = equalNullExp;
                else
                    whenExp = DbExpression.And(whenExp, equalNullExp);

                operandExps.Add(opBody);
            }

            generator._sqlBuilder.Append("CASE", " WHEN ");
            whenExp.Accept(generator);
            generator._sqlBuilder.Append(" THEN ");
            DbConstantExpression.Null.Accept(generator);
            generator._sqlBuilder.Append(" ELSE ");

            generator._sqlBuilder.Append("CONCAT(");

            for (int i = 0; i < operandExps.Count; i++)
            {
                if (i > 0)
                    generator._sqlBuilder.Append(",");

                generator._sqlBuilder.Append("IFNULL(");
                operandExps[i].Accept(generator);
                generator._sqlBuilder.Append(",");
                DbConstantExpression.StringEmpty.Accept(generator);
                generator._sqlBuilder.Append(")");
            }

            generator._sqlBuilder.Append(")");

            generator._sqlBuilder.Append(" END");
        }
    }
}
