using Chloe.DbExpressions;
using Chloe.RDBMS;
using System.Linq;

namespace Chloe.Oracle.MethodHandlers
{
    class StartsWith_Handler : IMethodHandler
    {
        public bool CanProcess(DbMethodCallExpression exp)
        {
            if (exp.Method != PublicConstants.MethodInfo_String_StartsWith)
                return false;

            return true;
        }
        public void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            exp.Object.Accept(generator);
            generator.SqlBuilder.Append(" LIKE ");
            exp.Arguments.First().Accept(generator);
            generator.SqlBuilder.Append(" || '%'");
        }
    }
}
