using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chloe
{
    public partial interface IDbContext : IDisposable
    {
        IDbmaintain Dbmaintain();

        int UpdateRange<TEntity>(List<TEntity> entities, string table = null);

        bool NonParamSQL { get; set; }
    }
}