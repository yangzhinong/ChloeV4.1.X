using Chloe.DbExpressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Chloe.RDBMS
{
    public class SqlGeneratorBase : DbExpressionVisitor<DbExpression>
    {
        private ISqlBuilder _sqlBuilder = new SqlBuilder();
        public bool NonParamSQL { get; set; }

        protected SqlGeneratorBase()
        {
        }

        public ISqlBuilder SqlBuilder { get { return this._sqlBuilder; } }
    }
}