using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chloe
{
    public abstract partial class DbContext : IDbContext, IDisposable
    {
        private bool _nonParamSQL;

        public abstract IDbmaintain Dbmaintain();

        public bool NonParamSQL
        {
            get => _nonParamSQL;

            set
            {
                _nonParamSQL = value;
                this.DatabaseProvider.CreateDbExpressionTranslator().NonParamSQL = value;
            }
        }

        public abstract int UpdateRange<TEntity>(List<TEntity> entities, string table = null);
    }
}