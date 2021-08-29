using Chloe.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChloeDemo
{
    /// <summary>
    /// 医保国籍表
    /// </summary>
    [Table("INSUR_COUNTRY_DICT", "YZN")]
    public partial class Insur_Country_Dict
    {
        /// <summary>
        /// 医保类别
        /// DataType=VARCHAR2
        /// 长度=20
        /// Nullable=0
        /// </summary>
        [Column(Size = 20)]
        public string Insur_Type { get; set; }

        /// <summary>
        /// 医保国籍编码
        /// DataType=VARCHAR2
        /// 长度=6
        /// Nullable=0
        /// </summary>
        [Column(Size = 6)]
        public string Country_Code { get; set; }

        /// <summary>
        /// 医保国籍名称
        /// DataType=VARCHAR2
        /// 长度=40
        /// Nullable=1
        /// </summary>
        [Column(Size = 40)]
        public string Country_Name { get; set; }

        [Column(Precision = 10, Scale = 2)]
        public decimal Price { get; set; } = 0;

        public int Age { get; set; }

        public DateTime? CreateTime { get; set; }
    }
}