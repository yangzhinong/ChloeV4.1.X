using Chloe.Core;
using Chloe.Core.Visitors;
using Chloe.DbExpressions;
using Chloe.Descriptors;
using Chloe.Exceptions;
using Chloe.Infrastructure;
using Chloe.RDBMS;
using Chloe.Utility;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Chloe.Oracle
{
    public partial class OracleContext : DbContext
    {
        private DatabaseProvider _databaseProvider;

        public OracleContext(IDbConnectionFactory dbConnectionFactory)
        {
            PublicHelper.CheckNull(dbConnectionFactory);
            this._databaseProvider = new DatabaseProvider(dbConnectionFactory, this);
        }

        public OracleContext(Func<IDbConnection> dbConnectionFactory) : this(new DbConnectionFactory(dbConnectionFactory))
        {
        }

        /// <summary>
        /// 设置方法解析器。
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="handler"></param>
        public static void SetMethodHandler(string methodName, IMethodHandler handler)
        {
            PublicHelper.CheckNull(methodName, nameof(methodName));
            PublicHelper.CheckNull(handler, nameof(handler));
            lock (SqlGenerator.MethodHandlers)
            {
                SqlGenerator.MethodHandlers[methodName] = handler;
            }
        }

        /// <summary>
        /// 是否将 sql 中的表名/字段名转成大写。默认为 true。
        /// </summary>
        public bool ConvertToUppercase { get; set; } = true;

        public override IDatabaseProvider DatabaseProvider
        {
            get { return this._databaseProvider; }
        }

        public override TEntity Insert<TEntity>(TEntity entity, string table)
        {
            PublicHelper.CheckNull(entity);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);

            List<PrimitivePropertyDescriptor> outputColumns = new List<PrimitivePropertyDescriptor>();
            Dictionary<PrimitivePropertyDescriptor, DbExpression> insertColumns = new Dictionary<PrimitivePropertyDescriptor, DbExpression>();
            foreach (PrimitivePropertyDescriptor propertyDescriptor in typeDescriptor.PrimitivePropertyDescriptors)
            {
                if (propertyDescriptor.IsAutoIncrement)
                {
                    outputColumns.Add(propertyDescriptor);
                    continue;
                }

                if (propertyDescriptor.HasSequence())
                {
                    DbMethodCallExpression getNextValueForSequenceExp = PublicHelper.MakeNextValueForSequenceDbExpression(propertyDescriptor, dbTable.Schema);
                    insertColumns.Add(propertyDescriptor, getNextValueForSequenceExp);
                    outputColumns.Add(propertyDescriptor);
                    continue;
                }

                object val = propertyDescriptor.GetValue(entity);

                PublicHelper.NotNullCheck(propertyDescriptor, val);

                DbExpression valExp = DbExpression.Parameter(val, propertyDescriptor.PropertyType, propertyDescriptor.Column.DbType);
                insertColumns.Add(propertyDescriptor, valExp);
            }

            DbInsertExpression e = new DbInsertExpression(dbTable);

            foreach (var kv in insertColumns)
            {
                e.InsertColumns.Add(kv.Key.Column, kv.Value);
            }

            e.Returns.AddRange(outputColumns.Select(a => a.Column));

            List<DbParam> parameters;

            var rowsAffected = this.ExecuteNonQuery(e, out parameters);
            if (rowsAffected < 1) throw new ChloeException($"未正确把数据插入数到表: {dbTable.Name}, 返回0条!");

            List<DbParam> outputParams = parameters.Where(a => a.Direction == ParamDirection.Output).ToList();

            for (int i = 0; i < outputColumns.Count; i++)
            {
                PrimitivePropertyDescriptor propertyDescriptor = outputColumns[i];
                string putputColumnName = Utils.GenOutputColumnParameterName(propertyDescriptor.Column.Name);
                DbParam outputParam = outputParams.Where(a => a.Name == putputColumnName).First();
                var outputValue = PublicHelper.ConvertObjectType(outputParam.Value, propertyDescriptor.PropertyType);
                outputColumns[i].SetValue(entity, outputValue);
            }

            return entity;
        }

        public override object Insert<TEntity>(Expression<Func<TEntity>> content, string table)
        {
            PublicHelper.CheckNull(content);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            //if (typeDescriptor.PrimaryKeys.Count > 1)
            //{
            //    /* 对于多主键的实体，暂时不支持调用这个方法进行插入 */
            //    throw new NotSupportedException(string.Format("Can not call this method because entity '{0}' has multiple keys.", typeDescriptor.Definition.Type.FullName));
            //}

            var keyPropertyDescriptors = typeDescriptor.PrimaryKeys;

            Dictionary<MemberInfo, Expression> insertColumns = InitMemberExtractor.Extract(content);

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);

            DefaultExpressionParser expressionParser = typeDescriptor.GetExpressionParser(dbTable);
            DbInsertExpression insertExp = new DbInsertExpression(dbTable);
            var keysDic = new Dictionary<PrimitivePropertyDescriptor, object>();

            foreach (var kv in insertColumns)
            {
                MemberInfo key = kv.Key;
                PrimitivePropertyDescriptor propertyDescriptor = typeDescriptor.GetPrimitivePropertyDescriptor(key);

                if (propertyDescriptor.IsAutoIncrement)
                    throw new ChloeException(string.Format("Could not insert value into the auto increment column '{0}'.", propertyDescriptor.Column.Name));

                if (propertyDescriptor.HasSequence())
                    throw new ChloeException(string.Format("Can not insert value into the column '{0}', because it's mapping member has define a sequence.", propertyDescriptor.Column.Name));

                if (propertyDescriptor.IsPrimaryKey)
                {
                    object val = ExpressionEvaluator.Evaluate(kv.Value);
                    if (val == null)
                        throw new ChloeException(string.Format("The primary key '{0}' could not be null.", propertyDescriptor.Property.Name));
                    else
                    {
                        keysDic.Add(propertyDescriptor, val);
                        insertExp.InsertColumns.Add(propertyDescriptor.Column, DbExpression.Parameter(val, propertyDescriptor.PropertyType, propertyDescriptor.Column.DbType));
                        continue;
                    }
                }

                insertExp.InsertColumns.Add(propertyDescriptor.Column, expressionParser.Parse(kv.Value));
            }

            //只增加主键是自增列或序列的返回
            foreach (PrimitivePropertyDescriptor propertyDescriptor in typeDescriptor.PrimitivePropertyDescriptors)
            {
                if (propertyDescriptor.IsAutoIncrement && propertyDescriptor.IsPrimaryKey)
                {
                    insertExp.Returns.Add(propertyDescriptor.Column);
                    continue;
                }

                if (propertyDescriptor.HasSequence())
                {
                    DbMethodCallExpression getNextValueForSequenceExp = PublicHelper.MakeNextValueForSequenceDbExpression(propertyDescriptor, dbTable.Schema);
                    insertExp.InsertColumns.Add(propertyDescriptor.Column, getNextValueForSequenceExp);

                    if (propertyDescriptor.IsPrimaryKey)
                    {
                        insertExp.Returns.Add(propertyDescriptor.Column);
                    }

                    continue;
                }
            }

            var keyValProperyNames = keysDic.Keys.Select(x => x.Property.Name).ToList();
            foreach (var keyPropertyDescriptor in keyPropertyDescriptors)
            {
                //主键为空并且主键又不是自增列
                if (!keyValProperyNames.Exists(x => keyPropertyDescriptor.Property.Name == x))
                {
                    if (!keyPropertyDescriptor.IsAutoIncrement && !keyPropertyDescriptor.HasSequence())
                    {
                        throw new ChloeException(string.Format("The primary key '{0}' could not be null.", keyPropertyDescriptor.Property.Name));
                    }
                }
            }

            List<DbParam> parameters;
            var rowsAffected = this.ExecuteNonQuery(insertExp, out parameters);
            if (rowsAffected < 1) throw new ChloeException($"未正确把数据插入数到表: {dbTable.Name}, 返回0条!");
            List<object> retList = new List<object>();
            if (keyPropertyDescriptors.Count > 0)
            {
                foreach (var keyPropertyDescriptor in keyPropertyDescriptors)
                {
                    if (keyPropertyDescriptor != null && (keyPropertyDescriptor.IsAutoIncrement || keyPropertyDescriptor.HasSequence()))
                    {
                        string outputColumnName = Utils.GenOutputColumnParameterName(keyPropertyDescriptor.Column.Name);
                        DbParam outputParam = parameters.Where(a => a.Direction == ParamDirection.Output && a.Name == outputColumnName).First();
                        retList.Add(PublicHelper.ConvertObjectType(outputParam.Value, keyPropertyDescriptor.PropertyType));
                    }
                }
            }
            if (retList.Count > 0)
            {
                return retList[0];
            }
            return null;
        }

        public override void InsertRange<TEntity>(List<TEntity> entities, string table)
        {
            /*
             * 将 entities 分批插入数据库
             * 每批生成 insert into TableName(...) select ... from dual union all select ... from dual...
             * 对于 oracle，貌似速度提升不了...- -
             * #期待各码友的优化建议#
             */

            PublicHelper.CheckNull(entities);
            if (entities.Count == 0)
                return;

            int maxParameters = 1000;
            int batchSize = 40; /* 每批实体大小，此值通过测试得出相对插入速度比较快的一个值 */

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            List<PrimitivePropertyDescriptor> mappingPropertyDescriptors = typeDescriptor.PrimitivePropertyDescriptors.Where(a => a.IsAutoIncrement == false).ToList();
            int maxDbParamsCount = maxParameters - mappingPropertyDescriptors.Count; /* 控制一个 sql 的参数个数 */

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            string sqlTemplate = AppendInsertRangeSqlTemplate(dbTable, mappingPropertyDescriptors);

            Action insertAction = () =>
            {
                int batchCount = 0;
                List<DbParam> dbParams = new List<DbParam>();
                StringBuilder sqlBuilder = new StringBuilder();
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];

                    if (batchCount > 0)
                        sqlBuilder.Append(" UNION ALL ");

                    sqlBuilder.Append("SELECT ");
                    for (int j = 0; j < mappingPropertyDescriptors.Count; j++)
                    {
                        if (j > 0)
                            sqlBuilder.Append(",");

                        PrimitivePropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[j];

                        object val = mappingPropertyDescriptor.GetValue(entity);

                        PublicHelper.NotNullCheck(mappingPropertyDescriptor, val);

                        if (val == null)
                        {
                            sqlBuilder.Append("NULL");
                            sqlBuilder.Append(" C").Append(j.ToString());
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
                            sqlBuilder.Append(val.ToString());
                        }
                        else if (val is bool)
                        {
                            if ((bool)val == true)
                                sqlBuilder.AppendFormat("1");
                            else
                                sqlBuilder.AppendFormat("0");
                        }
                        else
                        {
                            string paramName = UtilConstants.ParameterNamePrefix + dbParams.Count.ToString();
                            DbParam dbParam = new DbParam(paramName, val) { DbType = mappingPropertyDescriptor.Column.DbType };
                            dbParams.Add(dbParam);
                            sqlBuilder.Append(paramName);
                        }

                        sqlBuilder.Append(" C").Append(j.ToString());
                    }

                    sqlBuilder.Append(" FROM DUAL");

                    batchCount++;

                    if ((batchCount >= 20 && dbParams.Count >= 400/*参数个数太多也会影响速度*/) || dbParams.Count >= maxDbParamsCount || batchCount >= batchSize || (i + 1) == entities.Count)
                    {
                        sqlBuilder.Insert(0, sqlTemplate);
                        sqlBuilder.Append(") T");

                        string sql = sqlBuilder.ToString();
                        this.Session.ExecuteNonQuery(sql, dbParams.ToArray());

                        sqlBuilder.Clear();
                        dbParams.Clear();
                        batchCount = 0;
                    }
                }
            };

            Action fAction = insertAction;

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
                catch
                {
                    if (this.Session.IsInTransaction)
                        this.Session.RollbackTransaction();
                    throw;
                }
            }
        }

        public override int Update<TEntity>(TEntity entity, string table)
        {
            PublicHelper.CheckNull(entity);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));
            PublicHelper.EnsureHasPrimaryKey(typeDescriptor);

            PairList<PrimitivePropertyDescriptor, object> keyValues = new PairList<PrimitivePropertyDescriptor, object>(typeDescriptor.PrimaryKeys.Count);

            IEntityState entityState = this.TryGetTrackedEntityState(entity);
            Dictionary<PrimitivePropertyDescriptor, DbExpression> updateColumns = new Dictionary<PrimitivePropertyDescriptor, DbExpression>();
            foreach (PrimitivePropertyDescriptor propertyDescriptor in typeDescriptor.PrimitivePropertyDescriptors)
            {
                if (propertyDescriptor.IsPrimaryKey)
                {
                    var keyValue = propertyDescriptor.GetValue(entity);
                    PrimaryKeyHelper.KeyValueNotNull(propertyDescriptor, keyValue);
                    keyValues.Add(propertyDescriptor, keyValue);
                    continue;
                }

                if (propertyDescriptor.CannotUpdate())
                    continue;

                object val = propertyDescriptor.GetValue(entity);
                PublicHelper.NotNullCheck(propertyDescriptor, val);

                if (entityState != null && !entityState.HasChanged(propertyDescriptor, val))
                    continue;

                DbExpression valExp = DbExpression.Parameter(val, propertyDescriptor.PropertyType, propertyDescriptor.Column.DbType);
                updateColumns.Add(propertyDescriptor, valExp);
            }

            object rowVersionNewValue = null;
            if (typeDescriptor.HasRowVersion())
            {
                var rowVersionDescriptor = typeDescriptor.RowVersion;
                var rowVersionOldValue = rowVersionDescriptor.GetValue(entity);
                rowVersionNewValue = PublicHelper.IncreaseRowVersionNumber(rowVersionOldValue);
                updateColumns.Add(rowVersionDescriptor, DbExpression.Parameter(rowVersionNewValue, rowVersionDescriptor.PropertyType, rowVersionDescriptor.Column.DbType));
                keyValues.Add(rowVersionDescriptor, rowVersionOldValue);
            }

            if (updateColumns.Count == 0)
                return 0;

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            DbExpression conditionExp = PublicHelper.MakeCondition(keyValues, dbTable, this);
            DbUpdateExpression e = new DbUpdateExpression(dbTable, conditionExp);

            foreach (var item in updateColumns)
            {
                e.UpdateColumns.Add(item.Key.Column, item.Value);
            }

            int rowsAffected = this.ExecuteNonQuery(e);

            if (typeDescriptor.HasRowVersion())
            {
                PublicHelper.CauseErrorIfOptimisticUpdateFailed(rowsAffected);
                typeDescriptor.RowVersion.SetValue(entity, rowVersionNewValue);
            }

            if (entityState != null)
                entityState.Refresh();

            return rowsAffected;
        }

        public override int Delete<TEntity>(TEntity entity, string table)
        {
            PublicHelper.CheckNull(entity);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));
            PublicHelper.EnsureHasPrimaryKey(typeDescriptor);

            PairList<PrimitivePropertyDescriptor, object> keyValues = new PairList<PrimitivePropertyDescriptor, object>(typeDescriptor.PrimaryKeys.Count);

            foreach (PrimitivePropertyDescriptor keyPropertyDescriptor in typeDescriptor.PrimaryKeys)
            {
                object keyValue = keyPropertyDescriptor.GetValue(entity);
                PrimaryKeyHelper.KeyValueNotNull(keyPropertyDescriptor, keyValue);
                keyValues.Add(keyPropertyDescriptor, keyValue);
            }

            if (typeDescriptor.HasRowVersion())
            {
                var rowVersionValue = typeDescriptor.RowVersion.GetValue(entity);
                keyValues.Add(typeDescriptor.RowVersion, rowVersionValue);
            }

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            DbExpression conditionExp = PublicHelper.MakeCondition(keyValues, dbTable, this);
            DbDeleteExpression e = new DbDeleteExpression(dbTable, conditionExp);

            int rowsAffected = this.ExecuteNonQuery(e);

            if (typeDescriptor.HasRowVersion())
            {
                PublicHelper.CauseErrorIfOptimisticUpdateFailed(rowsAffected);
            }

            return rowsAffected;
        }

        private string AppendInsertRangeSqlTemplate(DbTable table, List<PrimitivePropertyDescriptor> mappingPropertyDescriptors)
        {
            StringBuilder sqlBuilder = new StringBuilder();

            sqlBuilder.Append("INSERT INTO ");
            sqlBuilder.Append(this.AppendTableName(table));
            sqlBuilder.Append("(");

            for (int i = 0; i < mappingPropertyDescriptors.Count; i++)
            {
                PrimitivePropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[i];
                if (i > 0)
                    sqlBuilder.Append(",");
                sqlBuilder.Append(this.QuoteName(mappingPropertyDescriptor.Column.Name));
            }

            sqlBuilder.Append(")");

            sqlBuilder.Append(" SELECT ");
            for (int i = 0; i < mappingPropertyDescriptors.Count; i++)
            {
                PrimitivePropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[i];
                if (i > 0)
                    sqlBuilder.Append(",");

                if (mappingPropertyDescriptor.HasSequence())
                    sqlBuilder.AppendFormat("{0}.{1}", this.QuoteName(mappingPropertyDescriptor.Definition.SequenceName), this.QuoteName("NEXTVAL"));
                else
                {
                    sqlBuilder.Append("C").Append(i.ToString());
                }
            }
            sqlBuilder.Append(" FROM (");

            string sqlTemplate = sqlBuilder.ToString();
            return sqlTemplate;
        }

        private string AppendTableName(DbTable table)
        {
            if (string.IsNullOrEmpty(table.Schema))
                return this.QuoteName(table.Name);

            return string.Format("{0}.{1}", this.QuoteName(table.Schema), this.QuoteName(table.Name));
        }

        private string QuoteName(string name)
        {
            if (this.ConvertToUppercase)
                return string.Concat("\"", name.ToUpper(), "\"");

            return string.Concat("\"", name, "\"");
        }

        public override IDbmaintain Dbmaintain()
        {
            return new Dbmaintain(this);
        }
    }
}