using Chloe.DbExpressions;

namespace Chloe.SqlServer.MethodHandlers
{
    class AddSeconds_Handler : IMethodHandler
    {
        public bool CanProcess(DbMethodCallExpression exp)
        {
            if (exp.Method.DeclaringType != PublicConstants.TypeOfDateTime)
                return false;

            return true;
        }
        public void Process(DbMethodCallExpression exp, SqlGenerator generator)
        {
            SqlGenerator.DbFunction_DATEADD(generator, "SECOND", exp);
        }
    }
}
