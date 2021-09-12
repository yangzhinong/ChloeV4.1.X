using Chloe.Core;
using Chloe.Core.Visitors;
using Chloe.Data;
using Chloe.DbExpressions;
using Chloe.Descriptors;
using Chloe.Exceptions;
using Chloe.Infrastructure;
using Chloe.InternalExtensions;
using Chloe.Query;
using Chloe.Query.Internals;
using Chloe.Reflection;
using Chloe.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Chloe
{
    public abstract partial class DbContext : IDbContext, IDisposable
    {
        private bool _disposed = false;
        private InnerAdoSession _adoSession;
        private DbSession _session;

        private Dictionary<Type, TrackEntityCollection> _trackingEntityContainer;

        private Dictionary<Type, TrackEntityCollection> TrackingEntityContainer
        {
            get
            {
                if (this._trackingEntityContainer == null)
                {
                    this._trackingEntityContainer = new Dictionary<Type, TrackEntityCollection>();
                }

                return this._trackingEntityContainer;
            }
        }

        internal Dictionary<Type, List<LambdaExpression>> QueryFilters { get; set; } = new Dictionary<Type, List<LambdaExpression>>();

        internal InnerAdoSession AdoSession
        {
            get
            {
                this.CheckDisposed();
                if (this._adoSession == null)
                    this._adoSession = new InnerAdoSession(this.DatabaseProvider.CreateConnection());
                return this._adoSession;
            }
        }

        public abstract IDatabaseProvider DatabaseProvider { get; }

        protected DbContext()
        {
            this._session = new DbSession(this);
        }

        public IDbSession Session { get { return this._session; } }

        public void HasQueryFilter<TEntity>(Expression<Func<TEntity, bool>> filter)
        {
            PublicHelper.CheckNull(filter, nameof(filter));

            Type entityType = typeof(TEntity);
            List<LambdaExpression> filters;
            if (!this.QueryFilters.TryGetValue(entityType, out filters))
            {
                filters = new List<LambdaExpression>(1);
                this.QueryFilters.Add(entityType, filters);
            }

            filters.Add(filter);
        }

        public virtual IQuery<TEntity> Query<TEntity>()
        {
            return this.Query<TEntity>(null);
        }

        public virtual IQuery<TEntity> Query<TEntity>(string table)
        {
            return this.Query<TEntity>(table, LockType.Unspecified);
        }

        public virtual IQuery<TEntity> Query<TEntity>(LockType @lock)
        {
            return this.Query<TEntity>(null, @lock);
        }

        public virtual IQuery<TEntity> Query<TEntity>(string table, LockType @lock)
        {
            return new Query<TEntity>(this, table, @lock);
        }

        public virtual TEntity QueryByKey<TEntity>(object key)
        {
            return this.QueryByKey<TEntity>(key, false);
        }

        public virtual TEntity QueryByKey<TEntity>(object key, bool tracking)
        {
            return this.QueryByKey<TEntity>(key, null, tracking);
        }

        public virtual TEntity QueryByKey<TEntity>(object key, string table)
        {
            return this.QueryByKey<TEntity>(key, table, false);
        }

        public virtual TEntity QueryByKey<TEntity>(object key, string table, bool tracking)
        {
            return this.QueryByKey<TEntity>(key, table, LockType.Unspecified, tracking);
        }

        public virtual TEntity QueryByKey<TEntity>(object key, string table, LockType @lock, bool tracking)
        {
            Expression<Func<TEntity, bool>> condition = PrimaryKeyHelper.BuildCondition<TEntity>(key);
            var q = this.Query<TEntity>(table, @lock).Where(condition);

            if (tracking)
                q = q.AsTracking();

            return q.FirstOrDefault();
        }

        public virtual IJoinQuery<T1, T2> JoinQuery<T1, T2>(Expression<Func<T1, T2, object[]>> joinInfo)
        {
            KeyValuePairList<JoinType, Expression> joinInfos = ResolveJoinInfo(joinInfo);
            var ret = this.Query<T1>()
                .Join<T2>(joinInfos[0].Key, (Expression<Func<T1, T2, bool>>)joinInfos[0].Value);

            return ret;
        }

        public virtual IJoinQuery<T1, T2, T3> JoinQuery<T1, T2, T3>(Expression<Func<T1, T2, T3, object[]>> joinInfo)
        {
            KeyValuePairList<JoinType, Expression> joinInfos = ResolveJoinInfo(joinInfo);
            var ret = this.Query<T1>()
                .Join<T2>(joinInfos[0].Key, (Expression<Func<T1, T2, bool>>)joinInfos[0].Value)
                .Join<T3>(joinInfos[1].Key, (Expression<Func<T1, T2, T3, bool>>)joinInfos[1].Value);

            return ret;
        }

        public virtual IJoinQuery<T1, T2, T3, T4> JoinQuery<T1, T2, T3, T4>(Expression<Func<T1, T2, T3, T4, object[]>> joinInfo)
        {
            KeyValuePairList<JoinType, Expression> joinInfos = ResolveJoinInfo(joinInfo);
            var ret = this.Query<T1>()
                .Join<T2>(joinInfos[0].Key, (Expression<Func<T1, T2, bool>>)joinInfos[0].Value)
                .Join<T3>(joinInfos[1].Key, (Expression<Func<T1, T2, T3, bool>>)joinInfos[1].Value)
                .Join<T4>(joinInfos[2].Key, (Expression<Func<T1, T2, T3, T4, bool>>)joinInfos[2].Value);

            return ret;
        }

        public virtual IJoinQuery<T1, T2, T3, T4, T5> JoinQuery<T1, T2, T3, T4, T5>(Expression<Func<T1, T2, T3, T4, T5, object[]>> joinInfo)
        {
            KeyValuePairList<JoinType, Expression> joinInfos = ResolveJoinInfo(joinInfo);
            var ret = this.Query<T1>()
                .Join<T2>(joinInfos[0].Key, (Expression<Func<T1, T2, bool>>)joinInfos[0].Value)
                .Join<T3>(joinInfos[1].Key, (Expression<Func<T1, T2, T3, bool>>)joinInfos[1].Value)
                .Join<T4>(joinInfos[2].Key, (Expression<Func<T1, T2, T3, T4, bool>>)joinInfos[2].Value)
                .Join<T5>(joinInfos[3].Key, (Expression<Func<T1, T2, T3, T4, T5, bool>>)joinInfos[3].Value);

            return ret;
        }

        public virtual IEnumerable<T> SqlQuery<T>(string sql, params DbParam[] parameters)
        {
            return this.SqlQuery<T>(sql, CommandType.Text, parameters);
        }

        public virtual IEnumerable<T> SqlQuery<T>(string sql, CommandType cmdType, params DbParam[] parameters)
        {
            PublicHelper.CheckNull(sql, "sql");
            return new InternalSqlQuery<T>(this, sql, cmdType, parameters);
        }

        public IEnumerable<T> SqlQuery<T>(string sql, object parameter)
        {
            /*
             * Usage:
             * dbContext.SqlQuery<User>("select * from Users where Id=@Id", new { Id = 1 }).ToList();
             */

            return this.SqlQuery<T>(sql, PublicHelper.BuildParams(this, parameter));
        }

        public IEnumerable<T> SqlQuery<T>(string sql, CommandType cmdType, object parameter)
        {
            /*
             * Usage:
             * dbContext.SqlQuery<User>("select * from Users where Id=@Id", CommandType.Text, new { Id = 1 }).ToList();
             */

            return this.SqlQuery<T>(sql, cmdType, PublicHelper.BuildParams(this, parameter));
        }

        public TEntity Save<TEntity>(TEntity entity)
        {
            if (this.Session.IsInTransaction)
            {
                this.Save(entity, null);
            }
            else
            {
                this.UseTransaction(() =>
                {
                    this.Save(entity, null);
                });
            }

            return entity;
        }

        private TEntity Save<TEntity>(TEntity entity, TypeDescriptor declaringTypeDescriptor)
        {
            this.Insert(entity);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            for (int i = 0; i < typeDescriptor.ComplexPropertyDescriptors.Count; i++)
            {
                //entity.TOther
                ComplexPropertyDescriptor navPropertyDescriptor = typeDescriptor.ComplexPropertyDescriptors[i];

                if (declaringTypeDescriptor != null && navPropertyDescriptor.PropertyType == declaringTypeDescriptor.Definition.Type)
                {
                    continue;
                }

                this.SaveOneToOne(navPropertyDescriptor, entity, typeDescriptor);
            }

            for (int i = 0; i < typeDescriptor.CollectionPropertyDescriptors.Count; i++)
            {
                //entity.List
                CollectionPropertyDescriptor collectionPropertyDescriptor = typeDescriptor.CollectionPropertyDescriptors[i];
                this.SaveCollection(collectionPropertyDescriptor, entity, typeDescriptor);
            }

            return entity;
        }

        private void SaveOneToOne(ComplexPropertyDescriptor navPropertyDescriptor, object owner, TypeDescriptor ownerTypeDescriptor)
        {
            /*
             * 1:1
             * T    <1:1>    TOther
             * T.TOther <--> TOther.T
             * T.Id <--> TOther.Id
             */

            //owner is T
            //navPropertyDescriptor is T.TOther
            //TypeDescriptor of T.TOther
            TypeDescriptor navTypeDescriptor = EntityTypeContainer.GetDescriptor(navPropertyDescriptor.PropertyType);
            //TOther.T
            ComplexPropertyDescriptor TOtherDotT = navTypeDescriptor.ComplexPropertyDescriptors.Where(a => a.PropertyType == ownerTypeDescriptor.Definition.Type).FirstOrDefault();

            bool isOneToOne = TOtherDotT != null;
            if (!isOneToOne)
                return;

            //instance of T.TOther
            object navValue = navPropertyDescriptor.GetValue(owner);
            if (navValue == null)
                return;

            //T.Id
            PrimitivePropertyDescriptor foreignKeyProperty = navPropertyDescriptor.ForeignKeyProperty;
            if (foreignKeyProperty.IsAutoIncrement || foreignKeyProperty.HasSequence())
            {
                //value of T.Id
                object foreignKeyValue = foreignKeyProperty.GetValue(owner);

                //T.TOther.Id = T.Id
                TOtherDotT.ForeignKeyProperty.SetValue(navValue, foreignKeyValue);
            }

            MethodInfo method = GetSaveMethod(navPropertyDescriptor.PropertyType);
            //DbContext.Save(navValue, ownerTypeDescriptor);
            method.Invoke(this, navValue, ownerTypeDescriptor);
        }

        private void SaveCollection(CollectionPropertyDescriptor collectionPropertyDescriptor, object owner, TypeDescriptor ownerTypeDescriptor)
        {
            PrimitivePropertyDescriptor ownerKeyPropertyDescriptor = ownerTypeDescriptor.PrimaryKeys.FirstOrDefault();
            if (ownerKeyPropertyDescriptor == null)
                return;

            //T.Elements
            IList elementList = collectionPropertyDescriptor.GetValue(owner) as IList;
            if (elementList == null || elementList.Count == 0)
                return;

            TypeDescriptor elementTypeDescriptor = EntityTypeContainer.GetDescriptor(collectionPropertyDescriptor.ElementType);
            //Element.T
            ComplexPropertyDescriptor elementDotT = elementTypeDescriptor.ComplexPropertyDescriptors.Where(a => a.PropertyType == ownerTypeDescriptor.Definition.Type).FirstOrDefault();

            object ownerKeyValue = ownerKeyPropertyDescriptor.GetValue(owner);
            MethodInfo method = GetSaveMethod(collectionPropertyDescriptor.ElementType);
            for (int i = 0; i < elementList.Count; i++)
            {
                object element = elementList[i];
                if (element == null)
                    continue;

                //element.ForeignKey = T.Id
                elementDotT.ForeignKeyProperty.SetValue(element, ownerKeyValue);
                //DbContext.Save(element, ownerTypeDescriptor);
                method.Invoke(this, element, ownerTypeDescriptor);
            }
        }

        public virtual TEntity Insert<TEntity>(TEntity entity)
        {
            return this.Insert(entity, null);
        }

        public virtual TEntity Insert<TEntity>(TEntity entity, string table)
        {
            PublicHelper.CheckNull(entity);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            Dictionary<PrimitivePropertyDescriptor, object> keyValueMap = PrimaryKeyHelper.CreateKeyValueMap(typeDescriptor);

            Dictionary<PrimitivePropertyDescriptor, DbExpression> insertColumns = new Dictionary<PrimitivePropertyDescriptor, DbExpression>();
            foreach (PrimitivePropertyDescriptor propertyDescriptor in typeDescriptor.PrimitivePropertyDescriptors)
            {
                if (propertyDescriptor.IsAutoIncrement)
                    continue;

                object val = propertyDescriptor.GetValue(entity);

                if (propertyDescriptor.IsPrimaryKey)
                {
                    keyValueMap[propertyDescriptor] = val;
                }

                PublicHelper.NotNullCheck(propertyDescriptor, val);

                DbParameterExpression valExp = DbExpression.Parameter(val, propertyDescriptor.PropertyType, propertyDescriptor.Column.DbType);
                insertColumns.Add(propertyDescriptor, valExp);
            }

            PrimitivePropertyDescriptor nullValueKey = keyValueMap.Where(a => a.Value == null && !a.Key.IsAutoIncrement).Select(a => a.Key).FirstOrDefault();
            if (nullValueKey != null)
            {
                /* 主键为空并且主键又不是自增列 */
                throw new ChloeException(string.Format("The primary key '{0}' could not be null.", nullValueKey.Property.Name));
            }

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            DbInsertExpression e = new DbInsertExpression(dbTable);

            foreach (var kv in insertColumns)
            {
                e.InsertColumns.Add(kv.Key.Column, kv.Value);
            }

            PrimitivePropertyDescriptor autoIncrementPropertyDescriptor = typeDescriptor.AutoIncrement;
            if (autoIncrementPropertyDescriptor == null)
            {
                this.ExecuteNonQuery(e);
                return entity;
            }

            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            List<DbParam> parameters;
            string sql = translator.Translate(e, out parameters);

            sql = string.Concat(sql, ";", this.GetSelectLastInsertIdClause());

            //SELECT @@IDENTITY 返回的是 decimal 类型
            object retIdentity = this.Session.ExecuteScalar(sql, parameters.ToArray());

            if (retIdentity == null || retIdentity == DBNull.Value)
            {
                throw new ChloeException("Unable to get the identity value.");
            }

            retIdentity = PublicHelper.ConvertObjectType(retIdentity, autoIncrementPropertyDescriptor.PropertyType);
            autoIncrementPropertyDescriptor.SetValue(entity, retIdentity);
            return entity;
        }

        public virtual object Insert<TEntity>(Expression<Func<TEntity>> content)
        {
            return this.Insert(content, null);
        }

        public virtual object Insert<TEntity>(Expression<Func<TEntity>> content, string table)
        {
            PublicHelper.CheckNull(content);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            if (typeDescriptor.PrimaryKeys.Count > 1)
            {
                /* 对于多主键的实体，暂时不支持调用这个方法进行插入 */
                throw new NotSupportedException(string.Format("Can not call this method because entity '{0}' has multiple keys.", typeDescriptor.Definition.Type.FullName));
            }

            PrimitivePropertyDescriptor keyPropertyDescriptor = typeDescriptor.PrimaryKeys.FirstOrDefault();

            Dictionary<MemberInfo, Expression> insertColumns = InitMemberExtractor.Extract(content);

            DbTable explicitDbTable = null;
            if (table != null)
                explicitDbTable = new DbTable(table, typeDescriptor.Table.Schema);
            DefaultExpressionParser expressionParser = typeDescriptor.GetExpressionParser(explicitDbTable);
            DbInsertExpression e = new DbInsertExpression(explicitDbTable ?? typeDescriptor.Table);

            object keyVal = null;

            foreach (var kv in insertColumns)
            {
                MemberInfo key = kv.Key;
                PrimitivePropertyDescriptor propertyDescriptor = typeDescriptor.GetPrimitivePropertyDescriptor(key);

                if (propertyDescriptor.IsAutoIncrement)
                    throw new ChloeException(string.Format("Could not insert value into the identity column '{0}'.", propertyDescriptor.Column.Name));

                if (propertyDescriptor.IsPrimaryKey)
                {
                    object val = ExpressionEvaluator.Evaluate(kv.Value);
                    if (val == null)
                        throw new ChloeException(string.Format("The primary key '{0}' could not be null.", propertyDescriptor.Property.Name));
                    else
                    {
                        keyVal = val;
                        e.InsertColumns.Add(propertyDescriptor.Column, DbExpression.Parameter(keyVal, propertyDescriptor.PropertyType, propertyDescriptor.Column.DbType));
                        continue;
                    }
                }

                e.InsertColumns.Add(propertyDescriptor.Column, expressionParser.Parse(kv.Value));
            }

            if (keyPropertyDescriptor != null)
            {
                //主键为空并且主键又不是自增列
                if (keyVal == null && !keyPropertyDescriptor.IsAutoIncrement)
                {
                    throw new ChloeException(string.Format("The primary key '{0}' could not be null.", keyPropertyDescriptor.Property.Name));
                }
            }

            if (keyPropertyDescriptor == null || !keyPropertyDescriptor.IsAutoIncrement)
            {
                this.ExecuteNonQuery(e);
                return keyVal; /* It will return null if an entity does not define primary key. */
            }

            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            List<DbParam> parameters;
            string sql = translator.Translate(e, out parameters);
            sql = string.Concat(sql, ";", this.GetSelectLastInsertIdClause());

            //SELECT @@IDENTITY 返回的是 decimal 类型
            object retIdentity = this.Session.ExecuteScalar(sql, parameters.ToArray());

            if (retIdentity == null || retIdentity == DBNull.Value)
            {
                throw new ChloeException("Unable to get the identity value.");
            }

            retIdentity = PublicHelper.ConvertObjectType(retIdentity, typeDescriptor.AutoIncrement.PropertyType);
            return retIdentity;
        }

        public virtual void InsertRange<TEntity>(List<TEntity> entities)
        {
            this.InsertRange(entities, null);
        }

        public virtual void InsertRange<TEntity>(List<TEntity> entities, string table)
        {
            throw new NotImplementedException();
        }

        public virtual int Update<TEntity>(TEntity entity)
        {
            return this.Update(entity, null);
        }

        public abstract int Update<TEntity>(TEntity entity, string table);

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="condition"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public virtual int Update<TEntity>(Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content)
        {
            return this.Update(condition, content, null);
        }

        /// <summary>
        /// 指定条件更新（除了自动增长和序列列不会更新外其余都要更新）不会跟踪值变化， 不会处理行版本
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="condition"></param>
        /// <param name="content"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public virtual int Update<TEntity>(Expression<Func<TEntity, bool>> condition,
                                           Expression<Func<TEntity, TEntity>> content,
                                           string table)
        {
            PublicHelper.CheckNull(condition);
            PublicHelper.CheckNull(content);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            Dictionary<MemberInfo, Expression> updateColumns = InitMemberExtractor.Extract(content);

            DbTable explicitDbTable = null;
            if (table != null)
                explicitDbTable = new DbTable(table, typeDescriptor.Table.Schema);
            DefaultExpressionParser expressionParser = typeDescriptor.GetExpressionParser(explicitDbTable);

            DbExpression conditionExp = expressionParser.ParseFilterPredicate(condition);
            if (updateColumns.Any(x => typeDescriptor.GetPrimitivePropertyDescriptor(x.Key).IsRowVersion))
            {
                var kvRowVersion = updateColumns.Where(x => typeDescriptor.GetPrimitivePropertyDescriptor(x.Key).IsRowVersion).First();
                var rowVersionDescriptor = typeDescriptor.RowVersion;
                var delfunc = content.Compile();

                var obj = delfunc(default(TEntity));
                var rowVersionOldValue = typeof(TEntity).GetProperty(rowVersionDescriptor.Property.Name).GetValue(obj, null); 

                DbExpression right = DbExpression.Constant(rowVersionOldValue, rowVersionDescriptor.PropertyType);
                DbExpression left = new DbColumnAccessExpression(explicitDbTable, rowVersionDescriptor.Column);
                DbExpression equalExp = new DbEqualExpression(left, right);
                conditionExp = conditionExp.And(equalExp);
            }

            DbUpdateExpression e = new DbUpdateExpression(explicitDbTable ?? typeDescriptor.Table, conditionExp);

            foreach (var kv in updateColumns)
            {
                MemberInfo key = kv.Key;
                PrimitivePropertyDescriptor propertyDescriptor = typeDescriptor.GetPrimitivePropertyDescriptor(key);

                //if (propertyDescriptor.IsPrimaryKey)
                //{
                //    continue;
                //}
                if (propertyDescriptor.IsAutoIncrement || propertyDescriptor.HasSequence())
                {
                    continue;
                }
                if (propertyDescriptor.IsRowVersion)
                {
                    continue;
                }
                e.UpdateColumns.Add(propertyDescriptor.Column, expressionParser.Parse(kv.Value));
            }

            if (typeDescriptor.HasRowVersion())
            {
                var rowVersionDescriptor = typeDescriptor.RowVersion;
                e.UpdateColumns.Add(rowVersionDescriptor.Column,
                            new DbAddExpression(rowVersionDescriptor.PropertyType,
                                new DbColumnAccessExpression(explicitDbTable, rowVersionDescriptor.Column),
                                new DbConstantExpression(1, rowVersionDescriptor.PropertyType), null));
            }

            if (e.UpdateColumns.Count == 0)
                return 0;

            return this.ExecuteNonQuery(e);
        }

        public virtual int Delete<TEntity>(TEntity entity)
        {
            return this.Delete(entity, null);
        }

        public abstract int Delete<TEntity>(TEntity entity, string table);

        public virtual int Delete<TEntity>(Expression<Func<TEntity, bool>> condition)
        {
            return this.Delete(condition, null);
        }

        public virtual int Delete<TEntity>(Expression<Func<TEntity, bool>> condition, string table)
        {
            PublicHelper.CheckNull(condition);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            DbTable explicitDbTable = null;
            if (table != null)
                explicitDbTable = new DbTable(table, typeDescriptor.Table.Schema);
            DefaultExpressionParser expressionParser = typeDescriptor.GetExpressionParser(explicitDbTable);
            DbExpression conditionExp = expressionParser.ParseFilterPredicate(condition);

            DbDeleteExpression e = new DbDeleteExpression(explicitDbTable ?? typeDescriptor.Table, conditionExp);

            return this.ExecuteNonQuery(e);
        }

        public virtual int DeleteByKey<TEntity>(object key)
        {
            return this.DeleteByKey<TEntity>(key, null);
        }

        public virtual int DeleteByKey<TEntity>(object key, string table)
        {
            Expression<Func<TEntity, bool>> condition = PrimaryKeyHelper.BuildCondition<TEntity>(key);
            return this.Delete<TEntity>(condition, table);
        }

        public ITransientTransaction BeginTransaction()
        {
            /*
             * using(ITransientTransaction tran = dbContext.BeginTransaction())
             * {
             *      dbContext.Insert()...
             *      dbContext.Update()...
             *      dbContext.Delete()...
             *      tran.Commit();
             * }
             */
            return new TransientTransaction(this);
        }

        public ITransientTransaction BeginTransaction(IsolationLevel il)
        {
            return new TransientTransaction(this, il);
        }

        public void UseTransaction(Action action)
        {
            /*
             * dbContext.UseTransaction(() =>
             * {
             *     dbContext.Insert()...
             *     dbContext.Update()...
             *     dbContext.Delete()...
             * });
             */

            PublicHelper.CheckNull(action);
            using (ITransientTransaction tran = this.BeginTransaction())
            {
                action();
                tran.Commit();
            }
        }

        public void UseTransaction(Action action, IsolationLevel il)
        {
            PublicHelper.CheckNull(action);
            using (ITransientTransaction tran = this.BeginTransaction(il))
            {
                action();
                tran.Commit();
            }
        }

        public virtual void TrackEntity(object entity)
        {
            PublicHelper.CheckNull(entity);
            Type entityType = entity.GetType();

            if (ReflectionExtension.IsAnonymousType(entityType))
                return;

            Dictionary<Type, TrackEntityCollection> entityContainer = this.TrackingEntityContainer;

            TrackEntityCollection collection;
            if (!entityContainer.TryGetValue(entityType, out collection))
            {
                TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(entityType);

                if (!typeDescriptor.HasPrimaryKey())
                    return;

                collection = new TrackEntityCollection(typeDescriptor);
                entityContainer.Add(entityType, collection);
            }

            collection.TryAddEntity(entity);
        }

        protected virtual string GetSelectLastInsertIdClause()
        {
            return "SELECT @@IDENTITY";
        }

        protected virtual IEntityState TryGetTrackedEntityState(object entity)
        {
            PublicHelper.CheckNull(entity);
            Type entityType = entity.GetType();
            Dictionary<Type, TrackEntityCollection> entityContainer = this._trackingEntityContainer;

            if (entityContainer == null)
                return null;

            TrackEntityCollection collection;
            if (!entityContainer.TryGetValue(entityType, out collection))
            {
                return null;
            }

            IEntityState ret = collection.TryGetEntityState(entity);
            return ret;
        }

        protected int ExecuteNonQuery(DbExpression e)
        {
            List<DbParam> parameters;
            return this.ExecuteNonQuery(e, out parameters);
        }

        protected int ExecuteNonQuery(DbExpression e, out List<DbParam> parameters)
        {
            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            string cmdText = translator.Translate(e, out parameters);

            int rowsAffected = this.Session.ExecuteNonQuery(cmdText, parameters.ToArray());
            return rowsAffected;
        }

        protected IDataReader ExecuteReader(DbExpression e)
        {
            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            List<DbParam> parameters;
            string cmdText = translator.Translate(e, out parameters);
            IDataReader dataReader = this.Session.ExecuteReader(cmdText, parameters.ToArray());
            return dataReader;
        }

        public void Dispose()
        {
            if (this._disposed)
                return;

            if (this._adoSession != null)
                this._adoSession.Dispose();
            this.Dispose(true);
            this._disposed = true;
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        private void CheckDisposed()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }

        private class TrackEntityCollection
        {
            public TrackEntityCollection(TypeDescriptor typeDescriptor)
            {
                this.TypeDescriptor = typeDescriptor;
                this.Entities = new Dictionary<object, IEntityState>(1);
            }

            public TypeDescriptor TypeDescriptor { get; private set; }
            public Dictionary<object, IEntityState> Entities { get; private set; }

            public bool TryAddEntity(object entity)
            {
                if (this.Entities.ContainsKey(entity))
                {
                    return false;
                }

                IEntityState entityState = new EntityState(this.TypeDescriptor, entity);
                this.Entities.Add(entity, entityState);

                return true;
            }

            public IEntityState TryGetEntityState(object entity)
            {
                IEntityState ret;
                if (!this.Entities.TryGetValue(entity, out ret))
                    ret = null;

                return ret;
            }
        }
    }
}