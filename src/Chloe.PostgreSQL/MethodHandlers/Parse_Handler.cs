using Chloe.DbExpressions;
using System;

namespace Chloe.PostgreSQL.MethodHandlers
{
    class Parse_Handler : IMethodHandler
    {
        public bool CanProcess(DbMethodCallExpression exp)
        {
            if (exp.Arguments.Count != 1)
                return false;

            DbExpression arg = exp.Arguments[0];
            if (arg.Type != PublicConstants.TypeOfString)
                return false;

            Type retType = exp.Method.ReturnType;
            if (exp.Method.DeclaringType != retType)
                return false;

            return true;
        }
        public void Process(DbMethodCallExpression exp, SqlGenerator generator)
        {
            DbExpression arg = exp.Arguments[0];
            DbExpression e = DbExpression.Convert(arg, exp.Method.ReturnType);
            e.Accept(generator);
        }
    }
}
