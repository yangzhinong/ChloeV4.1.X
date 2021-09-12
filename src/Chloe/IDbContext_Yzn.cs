using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Chloe
{
    public partial interface IDbContext : IDisposable
    {
        IDbmaintain Dbmaintain();

        /// <summary>
        /// 批量更新（只要表实体定义正确，程序将自动处理行版本）
        /// Set子句中 行版本=行版本+1，不包括主键，忽略，序列
        /// Where子句 会是主键+行版本？
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entities"></param>

        /// <returns>列表值，必须含关键字列，建议包括行版本列</returns>
        int UpdateRange<TEntity>(List<TEntity> entities);

        /// <summary>
        /// 批量更新（只要表实体定义正确，程序将自动处理行版本）
        /// Set子句中 会中TUpdate类属性中取， 行版本=行版本+1，不包括主键，忽略，序列
        /// Where子句 会是主键+行版本？ (故建议entitie中包括行版本列）
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TUpdate"></typeparam>
        /// <param name="entities">列表值，建议包括行版本列</param>
        /// <param name="typeHelper">表达式Heper 方便编译器推出TEntity类， 这样TUpdate可以是匿名类型 (Yzn.MutiKeyTest x) => true</param>
        /// <returns></returns>
        int UpdateRange<TEntity, TUpdate>(List<TUpdate> entities, Expression<Func<TEntity, bool>> typeHelper, bool checkWhere = true);

        /// <summary>
        /// 批量更新方法的单值更新 （只要表实体定义正确，程序将自动处理行版本）
        /// Set子句中 会中TUpdate类属性中取， 行版本=行版本+1，不包括主键，忽略，序列
        /// Where子句 会是主键+行版本? (故建议entitie中包括行版本列）
        /// 表达式Heper 方便编译器推出TEntity类，除此之外无用可以乱写
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TUpdate"></typeparam>
        /// <param name="entitie">值，建议包括行版本列</param>
        /// <param name="typeHelper">表达式Heper 方便编译器推出TEntity类，这样TUpdate可以是匿名类型 (Yzn.MutiKeyTest x) => true</param>
        /// <returns></returns>
        int UpdateOneUseRangeMethod<TEntity, TUpdate>(TUpdate entitie, Expression<Func<TEntity, bool>> typeHelper, bool checkWhere = true);

        /// <summary>
        /// 批量更新方法的单值更新 （只要表实体定义正确，程序将自动处理行版本）
        /// Set子句中 会中TUpdate类属性中取， 行版本=行版本+1，不包括主键，忽略，序列
        /// Where子句 会是主键+行版本
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TUpdate"></typeparam>
        /// <param name="entitie">值，建议包括行版本列</param>
        /// <param name="checkWhere">测检where字是否为空，空报异常</param>
        /// <returns></returns>
        int UpdateOneUseRangeMethod<TEntity, TUpdate>(TUpdate entitie, bool checkWhere = true);

        bool NonParamSQL { get; set; }

        /// <summary>
        /// 生成这种insert into a (f1,f2,f2) select c1,c2,c3 from b where b.score>70
        /// </summary>
        /// <typeparam name="TInsert"></typeparam>
        /// <param name="insertCols"></param>
        /// <param name="select"></param>
        /// <returns></returns>
        int InsertFrom<TInsert, TCols, TSelect>(Expression<Func<TInsert, TCols>> insertCols, IQuery<TSelect> select);
    }
}