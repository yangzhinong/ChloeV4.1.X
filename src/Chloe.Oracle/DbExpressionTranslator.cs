using Chloe.DbExpressions;
using Chloe.Infrastructure;
using System.Collections.Generic;

namespace Chloe.Oracle
{
    internal class DbExpressionTranslator : IDbExpressionTranslator
    {
        public static readonly DbExpressionTranslator Instance = new DbExpressionTranslator();
        public bool NonParamSQL { get; set; }

        public virtual string Translate(DbExpression expression, out List<DbParam> parameters)
        {
            SqlGenerator generator = this.CreateSqlGenerator();
            generator.NonParamSQL = NonParamSQL;
            expression = EvaluableDbExpressionTransformer.Transform(expression);
            expression.Accept(generator);

            parameters = generator.Parameters;
            string sql = generator.SqlBuilder.ToSql();

            return sql;
        }

        public virtual SqlGenerator CreateSqlGenerator()
        {
            return SqlGenerator.CreateInstance();
        }
    }

    internal class DbExpressionTranslator_ConvertToUppercase : DbExpressionTranslator
    {
        public static readonly new DbExpressionTranslator_ConvertToUppercase Instance = new DbExpressionTranslator_ConvertToUppercase();

        public override SqlGenerator CreateSqlGenerator()
        {
            return new SqlGenerator_ConvertToUppercase();
        }
    }
}