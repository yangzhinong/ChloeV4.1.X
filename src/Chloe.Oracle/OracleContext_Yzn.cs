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
    }
}