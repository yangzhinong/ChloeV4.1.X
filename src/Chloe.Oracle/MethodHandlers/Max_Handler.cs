using Chloe.DbExpressions;
using Chloe.RDBMS;
using System.Linq;

namespace Chloe.Oracle.MethodHandlers
{
    class Max_Handler : IMethodHandler
    {
        public bool CanProcess(DbMethodCallExpression exp)
        {
            if (exp.Method.DeclaringType != PublicConstants.TypeOfSql)
                return false;

            return true;
        }
        public void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            SqlGenerator.Aggregate_Max(generator, exp.Arguments.First(), exp.Method.ReturnType);
        }
    }
}
