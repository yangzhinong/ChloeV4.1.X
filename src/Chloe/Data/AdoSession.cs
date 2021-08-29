using Chloe.Exceptions;
using Chloe.Infrastructure;
using Chloe.Infrastructure.Interception;
using System;
using System.Collections.Generic;
using System.Data;

namespace Chloe.Data
{
    internal abstract class AdoSession : IAdoSession
    {
        private bool _disposed = false;

        public AdoSession()
        {
        }

        public abstract IDbConnection DbConnection { get; }

        /// <summary>
        /// 如果未开启事务，则返回 null
        /// </summary>
        public virtual IDbTransaction DbTransaction { get; protected set; }

        public virtual bool IsInTransaction { get; protected set; } = false;

        /// <summary>
        /// 命令执行超时时间，单位 seconds
        /// </summary>
        public virtual int CommandTimeout { get; set; } = 30;

        public event AdoEventHandler<IDataReader> OnReaderExecuting;

        public event AdoEventHandler<IDataReader> OnReaderExecuted;

        public event AdoEventHandler<int> OnNonQueryExecuting;

        public event AdoEventHandler<int> OnNonQueryExecuted;

        public event AdoEventHandler<object> OnScalarExecuting;

        public event AdoEventHandler<object> OnScalarExecuted;

        public virtual void Activate()
        {
            this.CheckDisposed();

            if (this.DbConnection.State == ConnectionState.Broken)
            {
                this.DbConnection.Close();
            }

            if (this.DbConnection.State == ConnectionState.Closed)
            {
                this.DbConnection.Open();
            }
        }

        /// <summary>
        /// 表示一次查询完成。在事务中的话不关闭连接，交给 CommitTransaction() 或者 RollbackTransaction() 控制，否则调用 IDbConnection.Close() 关闭连接
        /// </summary>
        public virtual void Complete()
        {
            if (!this.IsInTransaction)
            {
                if (this.DbConnection.State == ConnectionState.Open)
                {
                    this.DbConnection.Close();
                }
            }
        }

        public virtual void BeginTransaction(IsolationLevel? il)
        {
            this.Activate();

            if (il == null)
                this.DbTransaction = this.DbConnection.BeginTransaction();
            else
                this.DbTransaction = this.DbConnection.BeginTransaction(il.Value);

            this.IsInTransaction = true;
        }

        public virtual void CommitTransaction()
        {
            if (!this.IsInTransaction)
            {
                throw new ChloeException("Current session does not open a transaction.");
            }
            this.DbTransaction.Commit();
            this.ReleaseTransaction();
            this.Complete();
        }

        public virtual void RollbackTransaction()
        {
            if (!this.IsInTransaction)
            {
                throw new ChloeException("Current session does not open a transaction.");
            }
            this.DbTransaction.Rollback();
            this.ReleaseTransaction();
            this.Complete();
        }

        public virtual IDataReader ExecuteReader(string cmdText, DbParam[] parameters, CommandType cmdType)
        {
            return this.ExecuteReader(cmdText, parameters, cmdType, CommandBehavior.Default);
        }

        public virtual IDataReader ExecuteReader(string cmdText, DbParam[] parameters, CommandType cmdType, CommandBehavior behavior)
        {
            this.CheckDisposed();

            List<OutputParameter> outputParameters;
            IDbCommand cmd = this.PrepareCommand(cmdText, parameters, cmdType, out outputParameters);

            DbCommandInterceptionContext<IDataReader> dbCommandInterceptionContext = new DbCommandInterceptionContext<IDataReader>();

            this.Activate();
            this.OnReaderExecuting(cmd, dbCommandInterceptionContext);

            IDataReader reader;
            try
            {
                reader = new InternalDataReader(this, cmd.ExecuteReader(behavior), cmd, outputParameters);
            }
            catch (Exception ex)
            {
                dbCommandInterceptionContext.Exception = ex;
                this.OnReaderExecuted(cmd, dbCommandInterceptionContext);

                throw WrapException(ex);
            }

            dbCommandInterceptionContext.Result = reader;
            this.OnReaderExecuted(cmd, dbCommandInterceptionContext);
            /*
             * ps: 可在拦截器里对 dbCommandInterceptionContext.Result 进行装饰，然后重新设置到 dbCommandInterceptionContext.Result
             */

            return dbCommandInterceptionContext.Result;
        }

        public int ExecuteNonQuery(string cmdText, DbParam[] parameters, CommandType cmdType)
        {
            this.CheckDisposed();

            IDbCommand cmd = null;
            try
            {
                List<OutputParameter> outputParameters;
                cmd = this.PrepareCommand(cmdText, parameters, cmdType, out outputParameters);

                DbCommandInterceptionContext<int> dbCommandInterceptionContext = new DbCommandInterceptionContext<int>();

                this.Activate();
                this.OnNonQueryExecuting(cmd, dbCommandInterceptionContext);

                int rowsAffected;
                try
                {
                    rowsAffected = cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    dbCommandInterceptionContext.Exception = ex;
                    this.OnNonQueryExecuted(cmd, dbCommandInterceptionContext);

                    throw WrapException(ex);
                }

                dbCommandInterceptionContext.Result = rowsAffected;
                this.OnNonQueryExecuted(cmd, dbCommandInterceptionContext);
                OutputParameter.CallMapValue(outputParameters);

                return dbCommandInterceptionContext.Result;
            }
            finally
            {
                this.Complete();
                if (cmd != null)
                    cmd.Dispose();
            }
        }

        public object ExecuteScalar(string cmdText, DbParam[] parameters, CommandType cmdType)
        {
            this.CheckDisposed();

            IDbCommand cmd = null;
            try
            {
                List<OutputParameter> outputParameters;
                cmd = this.PrepareCommand(cmdText, parameters, cmdType, out outputParameters);

                DbCommandInterceptionContext<object> dbCommandInterceptionContext = new DbCommandInterceptionContext<object>();

                this.Activate();
                this.OnScalarExecuting(cmd, dbCommandInterceptionContext);

                object ret;
                try
                {
                    ret = cmd.ExecuteScalar();
                }
                catch (Exception ex)
                {
                    dbCommandInterceptionContext.Exception = ex;
                    this.OnScalarExecuted(cmd, dbCommandInterceptionContext);

                    throw WrapException(ex);
                }

                dbCommandInterceptionContext.Result = ret;
                this.OnScalarExecuted(cmd, dbCommandInterceptionContext);
                OutputParameter.CallMapValue(outputParameters);

                return dbCommandInterceptionContext.Result;
            }
            finally
            {
                this.Complete();
                if (cmd != null)
                    cmd.Dispose();
            }
        }

        public void Dispose()
        {
            if (this._disposed)
                return;

            this.Dispose(true);
            this._disposed = true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.DbTransaction != null)
            {
                if (this.IsInTransaction)
                {
                    try
                    {
                        this.DbTransaction.Rollback();
                    }
                    catch
                    {
                    }
                }

                this.ReleaseTransaction();
            }

            if (this.DbConnection != null)
            {
                this.DbConnection.Dispose();
            }
        }

        protected virtual IDbCommand PrepareCommand(string cmdText, DbParam[] parameters, CommandType cmdType, out List<OutputParameter> outputParameters)
        {
            outputParameters = null;

            IDbCommand cmd = this.DbConnection.CreateCommand();

            cmd.CommandText = cmdText;
            cmd.CommandType = cmdType;
            cmd.CommandTimeout = this.CommandTimeout;
            if (this.IsInTransaction)
                cmd.Transaction = this.DbTransaction;

            if (parameters != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    DbParam param = parameters[i];
                    if (param == null)
                        continue;

                    if (param.ExplicitParameter != null)/* 如果存在创建好了的 IDbDataParameter，则直接用它。同时也忽视了 DbParam 的其他属性 */
                    {
                        cmd.Parameters.Add(param.ExplicitParameter);
                        continue;
                    }

                    Type parameterType;
                    if (param.Value == null || param.Value == DBNull.Value)
                    {
                        parameterType = param.Type ?? typeof(object);
                    }
                    else
                    {
                        parameterType = param.Value.GetType();
                        if (parameterType.IsEnum)
                        {
                            parameterType = Enum.GetUnderlyingType(parameterType);
                        }
                    }

                    IDbDataParameter parameter = cmd.CreateParameter();
                    Infrastructure.MappingType mappingType;
                    IDbParameterAssembler dbParameterAssembler = MappingTypeSystem.IsMappingType(parameterType, out mappingType) ? mappingType.DbParameterAssembler : DbParameterAssembler.Default;
                    dbParameterAssembler.SetupParameter(parameter, param);

                    cmd.Parameters.Add(parameter);

                    OutputParameter outputParameter = null;
                    if (param.Direction == ParamDirection.Output || param.Direction == ParamDirection.InputOutput)
                    {
                        outputParameter = new OutputParameter(param, parameter);
                        if (outputParameters == null)
                            outputParameters = new List<OutputParameter>();
                        outputParameters.Add(outputParameter);
                    }
                }
            }

            return cmd;
        }

        private void ReleaseTransaction()
        {
            this.DbTransaction.Dispose();
            this.DbTransaction = null;
            this.IsInTransaction = false;
        }

        private void CheckDisposed()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }

        private static ChloeException WrapException(Exception ex)
        {
            return new ChloeException($"An exception occurred while executing DbCommand. For details please see the inner exception. {ex.Message}", ex);
        }
    }
}