using Chloe.Core.Visitors;
using Chloe.DbExpressions;
using Chloe.Descriptors;
using Chloe.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Chloe.Oracle
{
    public partial class OracleContext : DbContext
    {
        /// <summary>
        /// 批量更新（只要表实体定义正确，程序将自动处理行版本）
        /// Set子句中 会中TUpdate类属性中取， 行版本=行版本+1，不包括主键，忽略，序列
        /// Where子句 会是主键+行版本？ (故建议entitie中包括行版本列）
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TUpdate"></typeparam>
        /// <param name="entities">列表值，建议包括行版本列</param>
        /// <param name="typeHelper">表达式Heper 方便编译器推出TEntity类， 这样TUpdate可以是匿名类型 (Yzn.MutiKeyTest x) => true</param>
        /// <param name="checkWhere">检查更新条件是否空（空的话是更新全表，有可能有危险）</param>
        /// <returns></returns>
        public override int UpdateRange<TEntity, TUpdate>(List<TUpdate> entities, Expression<Func<TEntity, bool>> typeHelper, bool checkWhere = true)
        {
            PublicHelper.CheckNull(entities);

            if (entities.Count == 0)
                return 0;
            var propertyNames = typeof(TUpdate).GetProperties().Select(x => x.Name).ToList();

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));
            List<PrimitivePropertyDescriptor> setPropertyDescriptors = typeDescriptor.PrimitivePropertyDescriptors
                                              .Where(a => a.IsRowVersion ||
                                                    (a.CannotUpdate() &&
                                                        propertyNames.Contains(a.Property.Name)
                                                     ))
                                              .ToList();

            Dictionary<PrimitivePropertyDescriptor, PropertyInfo> dic = new Dictionary<PrimitivePropertyDescriptor, PropertyInfo>();
            {
                var props = typeof(TUpdate).GetProperties();
                foreach (var set in setPropertyDescriptors)
                {
                    var prop = props.FirstOrDefault(x => x.Name == set.Property.Name);
                    if (prop != null)
                    {
                        dic.Add(set, prop);
                    }
                }
            }

            if (checkWhere && !typeDescriptor.PrimitivePropertyDescriptors
                        .Where(a => a.IsPrimaryKey)
                        .All(x => propertyNames.Contains(x.Property.Name)))
            {
                throw new Exception("更新的数据中必须包含完整的主键信息!");
            }
            int maxParameters = 1000;
            int batchSize = 50; /* 每批实体大小，此值通过测试得出相对插入速度比较快的一个值 */

            var whereProperties = typeDescriptor.PrimitivePropertyDescriptors.Where(p => p.IsPrimaryKey || p.IsRowVersion).ToList();

            var rowVersionProp = whereProperties.FirstOrDefault(x => x.IsRowVersion);
            if (rowVersionProp != null)
            {
                if (!propertyNames.Contains(rowVersionProp.Property.Name))
                {
                    whereProperties.Remove(rowVersionProp);
                }
            }
            if (checkWhere && whereProperties.Count == 0)
            {
                throw new Exception("Where子句为空，将会更新全表!");
            }

            int maxDbParamsCount = maxParameters - setPropertyDescriptors.Count; /* 控制一个 sql 的参数个数 */

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, null);
            string sqlTemplate = this.AppendUpdateRangeSqlTemplate(dbTable, setPropertyDescriptors, whereProperties);

            Action updateAction = () =>
            {
                int batchCount = 0;
                List<DbParam> dbParams = new List<DbParam>();
                StringBuilder sqlBuilder = new StringBuilder();

                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];

                    if (batchCount > 0)
                        sqlBuilder.Append(";" + Environment.NewLine);

                    var str = sqlTemplate;
                    //一个实体对象构造SQL
                    for (int j = 0; j < setPropertyDescriptors.Count; j++)
                    {
                        var setPropertyDescriptor = setPropertyDescriptors[j];

                        if (setPropertyDescriptor.IsRowVersion)
                        {
                            var colname = Utils.QuoteName(setPropertyDescriptor.Column.Name, ConvertToUppercase);
                            str = str.Replace("{" + j + "}", $"= {colname} +1");
                            continue;
                        }
                        object val = dic[setPropertyDescriptor].GetValue(entity, null);
                        if (val == null)
                        {
                            str = str.Replace("{" + j + "}", "= NULL");
                            continue;
                        }

                        Type valType = val.GetType();
                        if (valType.IsEnum)
                        {
                            val = Convert.ChangeType(val, Enum.GetUnderlyingType(valType));
                            valType = val.GetType();
                        }

                        if (Utils.IsToStringableNumericType(valType))
                        {
                            str = str.Replace("{" + j + "}", " = " + val.ToString());
                            continue;
                        }

                        if (val is bool)
                        {
                            if ((bool)val == true)
                                str = str.Replace("{" + j + "}", "= 1");
                            else
                                str = str.Replace("{" + j + "}", "= 0");
                            continue;
                        }

                        string paramName = UtilConstants.ParameterNamePrefix + dbParams.Count.ToString();
                        DbParam dbParam = new DbParam(paramName, val) { DbType = setPropertyDescriptor.Column.DbType };
                        dbParams.Add(dbParam);
                        str = str.Replace("{" + j + "}", "= " + paramName);
                    }

                    //主键
                    for (var k = 0; k < whereProperties.Count; k++)
                    {
                        try
                        {
                            var primaryMappingProperty = whereProperties[k];
                            object val = dic[primaryMappingProperty].GetValue(entity, null);
                            string pkName = UtilConstants.ParameterNamePrefix + primaryMappingProperty.Column.Name + i;
                            DbParam pkParam = new DbParam(pkName, val) { DbType = primaryMappingProperty.Column.DbType };
                            dbParams.Add(pkParam);
                            str = str.Replace("{PK" + k + "}", pkName);
                        }
                        catch { }
                    }

                    sqlBuilder.Append(str);
                    batchCount++;

                    if ((batchCount >= 20 && dbParams.Count >= 120/*参数个数太多也会影响速度*/) || dbParams.Count >= maxDbParamsCount || batchCount >= batchSize || (i + 1) == entities.Count)
                    {
                        //sqlBuilder.Insert(0, sqlTemplate);

                        if (batchCount > 1)
                        {
                            sqlBuilder.Insert(0, "begin ");
                            sqlBuilder.Append("; end;");
                        }
                        string sql = sqlBuilder.ToString();
                        this.Session.ExecuteNonQuery(sql, dbParams.ToArray());

                        sqlBuilder.Clear();
                        dbParams.Clear();
                        batchCount = 0;
                    }
                }
            };

            Action fAction = () =>
            {
                updateAction();
            };

            if (this.Session.IsInTransaction)
            {
                fAction();
            }
            else
            {
                /* 因为分批插入，所以需要开启事务保证数据一致性 */
                this.Session.BeginTransaction();
                try
                {
                    fAction();
                    this.Session.CommitTransaction();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                    if (this.Session.IsInTransaction)
                        this.Session.RollbackTransaction();
                    throw;
                }
            }
            return entities.Count;
        }

        private string AppendUpdateRangeSqlTemplate(DbTable table,
                List<PrimitivePropertyDescriptor> setProperties,
                List<PrimitivePropertyDescriptor> wherePropertyDescriptors)
        {
            var sqlBuilder = new StringBuilder();

            sqlBuilder.Append("UPDATE ");
            sqlBuilder.Append(this.AppendTableName(table));
            sqlBuilder.Append(" SET ");
            for (int i = 0; i < setProperties.Count; i++)
            {
                var mappingPropertyDescriptor = setProperties[i];
                if (i > 0) sqlBuilder.Append(", ");

                sqlBuilder.Append(Utils.QuoteName(mappingPropertyDescriptor.Column.Name, ConvertToUppercase));
                sqlBuilder.Append(" {" + i + "} ");
            }
            if (wherePropertyDescriptors.Count > 0)
            {
                sqlBuilder.Append(" WHERE ");
            }
            for (var i = 0; i < wherePropertyDescriptors.Count; i++)
            {
                var primaryKeyDescriptor = wherePropertyDescriptors[i];
                if (i > 0) sqlBuilder.Append(" AND ");
                sqlBuilder.Append(Utils.QuoteName(primaryKeyDescriptor.Column.Name, ConvertToUppercase));
                sqlBuilder.Append(" = {PK" + i + "} ");
            }
            string sqlTemplate = sqlBuilder.ToString();
            return sqlTemplate;
        }

        /// <summary>
        /// insert into a (f1,f2,f2) select c1,c2,c3 from b where b.score>70 <br/>
        /// <br/>
        /// db.InsertFrom((Yzn.MutiKeyTest x) =&gt; new { x.Pat, x.Visit, x.Desc },<br/>
        ///    db.Query&lt;Yzn.MutiKeyTest&gt;() <br/>
        ///                   .Where(x = &gt; x.CreateTime &gt; dNow) <br/>
        ///                   .Select(x = &gt; new { x.Pat, x.Visit, x.Desc }));<br/>
        /// </summary>
        /// <typeparam name="TInsert">插入的表</typeparam>
        /// <param name="insertCols">列表达式</param>
        /// <param name="select"></param>
        /// <returns></returns>
        public override int InsertFrom<TInsert, TCols, TSelect>(Expression<Func<TInsert, TCols>> insertCols, IQuery<TSelect> select)
        {
            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TInsert));
            List<string> cols = typeof(TCols).GetProperties().Select(x => x.Name).ToList(); ;
            var colPropertyDescriptors = typeDescriptor.PrimitivePropertyDescriptors.Where(x => cols.Contains(x.Property.Name));
            DbTable dbTableInsert = PublicHelper.CreateDbTable(typeDescriptor, null);

            StringBuilder sqlBuilder = new StringBuilder("insert into ");
            sqlBuilder.Append(this.AppendTableName(dbTableInsert));
            sqlBuilder.Append(" ( ");
            bool isFirst = true;
            foreach (var propertyDescriptor in colPropertyDescriptors)
            {
                if (!isFirst)
                {
                    sqlBuilder.Append(", ");
                }
                else { isFirst = false; }
                var colname = Utils.QuoteName(propertyDescriptor.Column.Name, ConvertToUppercase);
                sqlBuilder.Append(colname);
            }
            sqlBuilder.Append(" ) ");
            var selectCmd = select.GetDbCommandFactor();
            sqlBuilder.Append(selectCmd.CommandText);
            var sql = sqlBuilder.ToString();
            var i = Session.ExecuteNonQuery(sql, selectCmd.Parameters);
            return i;
        }
    }
}