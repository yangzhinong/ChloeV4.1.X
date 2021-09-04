namespace Chloe.Oracle.SysDbModel
{
    public class All_Tab_Cols
    {
        public string Owner { get; set; }
        public string Table_Name { get; set; }

        public string Column_Name { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public string Data_Type { get; set; }

        /// <summary>
        /// 长度
        /// </summary>

        public int Data_Length { get; set; }

        /// <summary>
        /// 精度
        /// </summary>
        public int Data_Precision { get; set; }

        /// <summary>
        /// 小数位
        /// </summary>
        public int Data_Scale { get; set; }
    }
}