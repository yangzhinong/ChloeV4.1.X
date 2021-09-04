using Chloe.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChloeDemo.Yzn
{
    [Table("MUTIKEYTEST", "YZN")]
    public class MutiKeyTest
    {
        [Column(IsPrimaryKey = true, Size = 20)]
        [NonAutoIncrement]
        public string Pat { get; set; }

        [Column(IsPrimaryKey = true)]
        [NonAutoIncrement]
        public int Visit { get; set; }

        public int Val { get; set; }

        [Column(Precision = 10, Scale = 2)]
        [DbColComment("添加小数列")]
        public decimal AddDecCol { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        [Column(Size = 40, DbType = System.Data.DbType.AnsiString)]
        public string Desc { get; set; }
    }
}