using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chloe.Annotations
{
    public class IndexAttribute : Attribute
    {
        public const string Prefix = "Idx";
        public string[] ColNames { get; }

        /// <summary>
        /// 如果要指定索引名，请保证唯一性，不指定则会按Idx_TableName_ColName规则创建
        /// </summary>
        public string IdxName { get; }

        public IndexAttribute()
        {
        }

        public IndexAttribute(string indexName, params string[] colNames)
        {
            IdxName = indexName;
            ColNames = colNames;
        }
    }
}