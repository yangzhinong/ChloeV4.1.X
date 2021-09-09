using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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

        public virtual int UpdateRange<TEntity>(List<TEntity> entities)
        {
            return UpdateRange(entities, (TEntity b) => true);
        }

        public abstract int UpdateRange<TEntity, TUpdate>(List<TUpdate> entities, Expression<Func<TEntity, bool>> typeHelper, bool checkWhere = true);

        public virtual int UpdateOneUseRangeMethod<TEntity, TUpdate>(TUpdate entitie, bool checkWhere = true)
        {
            List<TUpdate> updates = new List<TUpdate> { entitie };
            return UpdateRange(updates, (TEntity t) => true, checkWhere);
        }

        public virtual int UpdateOneUseRangeMethod<TEntity, TUpdate>(TUpdate entitie, Expression<Func<TEntity, bool>> typeHelper, bool checkWhere = true)
        {
            List<TUpdate> updates = new List<TUpdate> { entitie };
            return UpdateRange(updates, typeHelper, checkWhere);
        }
    }
}