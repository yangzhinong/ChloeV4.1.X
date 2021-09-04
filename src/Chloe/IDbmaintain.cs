namespace Chloe
{
    public interface IDbmaintain
    {
        void DropTable<TEntity>();

        void InitTable<TEntity>();

        /// <summary>
        /// 无表添加表，有表检查与模型的差异，添加表比模型少的列
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        void SafteInitTablbeAndColumns<TEntity>();
    }
}