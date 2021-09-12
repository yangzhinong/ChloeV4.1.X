using Chloe.Core;
using Chloe.Data;
using Chloe.Infrastructure;
using Chloe.Mapper;
using Chloe.Mapper.Activators;
using Chloe.Query.Mapping;
using Chloe.Query.QueryState;
using Chloe.Query.Visitors;
using Chloe.Reflection;
using Chloe.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Chloe.Query.Internals
{
    internal class InternalQuery<T> : IEnumerable<T>, IEnumerable
    {
        private Query<T> _query;

        internal InternalQuery(Query<T> query)
        {
            this._query = query;
        }

        public DbCommandFactor GenerateCommandFactor()
        {
            IQueryState qs = QueryExpressionResolver.Resolve(this._query.QueryExpression, new ScopeParameterDictionary(), new StringSet());
            MappingData data = qs.GenerateMappingData();

            IObjectActivator objectActivator;
            if (this._query._trackEntity)
                objectActivator = data.ObjectActivatorCreator.CreateObjectActivator(this._query.DbContext);
            else
                objectActivator = data.ObjectActivatorCreator.CreateObjectActivator();

            IDbExpressionTranslator translator = this._query.DbContext.DatabaseProvider.CreateDbExpressionTranslator();
            List<DbParam> parameters;
            string cmdText = translator.Translate(data.SqlQuery, out parameters);

            DbCommandFactor commandFactor = new DbCommandFactor(objectActivator, cmdText, parameters.ToArray());
            return commandFactor;
        }

        public IEnumerator<T> GetEnumerator()
        {
            DbCommandFactor commandFactor = this.GenerateCommandFactor();
            var enumerator = QueryEnumeratorCreator.CreateEnumerator<T>(commandFactor, cmdFactor =>
            {
                IDataReader dataReader = this._query.DbContext.AdoSession.ExecuteReader(cmdFactor.CommandText, cmdFactor.Parameters, CommandType.Text);

                if (cmdFactor.ObjectActivator is RootEntityActivator)
                {
                    dataReader = new QueryDataReader(dataReader);
                }

                cmdFactor.ObjectActivator.Prepare(dataReader);

                return dataReader;
            });
            return enumerator;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public override string ToString()
        {
            DbCommandFactor commandFactor = this.GenerateCommandFactor();
            return AppendDbCommandInfo(commandFactor.CommandText, commandFactor.Parameters);
        }

        private static string AppendDbCommandInfo(string cmdText, DbParam[] parameters)
        {
            StringBuilder sb = new StringBuilder();
            if (parameters != null)
            {
                foreach (DbParam param in parameters)
                {
                    if (param == null)
                        continue;

                    string typeName = null;
                    object value = null;
                    Type parameterType;
                    if (param.Value == null || param.Value == DBNull.Value)
                    {
                        parameterType = param.Type;
                        value = "NULL";
                    }
                    else
                    {
                        value = param.Value;
                        parameterType = param.Value.GetType();

                        if (parameterType == typeof(string) || parameterType == typeof(DateTime))
                            value = "'" + value + "'";
                    }

                    if (parameterType != null)
                        typeName = GetTypeName(parameterType);

                    sb.AppendFormat("{0} {1} = {2};", typeName, param.Name, value);
                    sb.AppendLine();
                }
            }

            sb.AppendLine(cmdText);

            return sb.ToString();
        }

        private static string GetTypeName(Type type)
        {
            Type underlyingType;
            if (ReflectionExtension.IsNullable(type, out underlyingType))
            {
                return string.Format("Nullable<{0}>", GetTypeName(underlyingType));
            }

            return type.Name;
        }
    }
}