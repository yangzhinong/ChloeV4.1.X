using Chloe.DbExpressions;
using Chloe.RDBMS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chloe.Oracle.MethodHandlers
{
    internal class NextRowVersion_Handler : IMethodHandler
    {
        public bool CanProcess(DbMethodCallExpression exp)
        {
            if (exp.Method.DeclaringType != PublicConstants.TypeOfSql)
                return false;

            return true;
        }

        public void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            exp.Arguments[0].Accept(generator);
            generator.SqlBuilder.Append("+1");
        }
    }
}