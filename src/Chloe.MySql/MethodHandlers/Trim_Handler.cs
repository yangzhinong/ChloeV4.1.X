using Chloe.DbExpressions;

namespace Chloe.MySql.MethodHandlers
{
    class Trim_Handler : IMethodHandler
    {
        public bool CanProcess(DbMethodCallExpression exp)
        {
            if (exp.Method != PublicConstants.MethodInfo_String_Trim)
                return false;

            return true;
        }
        public void Process(DbMethodCallExpression exp, SqlGenerator generator)
        {
            generator.SqlBuilder.Append("TRIM(");
            exp.Object.Accept(generator);
            generator.SqlBuilder.Append(")");
        }
    }
}
