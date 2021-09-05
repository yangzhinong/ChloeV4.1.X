using System.Collections.Generic;

namespace Chloe.DbExpressions
{
    public class DbUpdateExpression : DbExpression
    {
        private DbTable _table;
        private DbExpression _condition;

        public DbUpdateExpression(DbTable table)
            : this(table, null)
        {
        }

        /// <summary>
        /// 生成更新表表达式
        /// </summary>
        /// <param name="table">表</param>
        /// <param name="condition">Where条件</param>
        public DbUpdateExpression(DbTable table, DbExpression condition)
            : base(DbExpressionType.Update, PublicConstants.TypeOfVoid)
        {
            PublicHelper.CheckNull(table);

            this._table = table;
            this._condition = condition;
        }

        public DbTable Table { get { return this._table; } }
        public Dictionary<DbColumn, DbExpression> UpdateColumns { get; private set; } = new Dictionary<DbColumn, DbExpression>();
        public List<DbColumn> Returns { get; private set; } = new List<DbColumn>();
        public DbExpression Condition { get { return this._condition; } }

        public override T Accept<T>(DbExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}