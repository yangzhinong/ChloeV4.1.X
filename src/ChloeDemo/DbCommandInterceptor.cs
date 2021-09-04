using Chloe.Infrastructure.Interception;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChloeDemo
{
    /// <summary>
    /// sql 拦截器。可以输出 sql 语句极其相应的参数
    /// </summary>
    public class DbCommandInterceptor : IDbCommandInterceptor
    {
        /// <summary>
        /// oracle：修改参数绑定方式
        /// </summary>
        /// <param name="command"></param>
        private void BindByName(IDbCommand command)
        {
            if (command is OracleCommand)
            {
                (command as OracleCommand).BindByName = true;
            }
        }

        public void ReaderExecuting(IDbCommand command, DbCommandInterceptionContext<IDataReader> interceptionContext)
        {
            this.BindByName(command);

            //interceptionContext.DataBag.Add("startTime", DateTime.Now);

            DebugSQLInfo(AppendDbCommandInfo(command));
            //DebugSQLInfo(command.CommandText);
        }

        private void DebugSQLInfo(object info)
        {
            if (Debugger.IsAttached)
            {
                Debug.WriteLine(info);
            }
            else
            {
                if (File.Exists("debug.sql"))
                {
                    string strLogPath = "SQLLOG";
                    if (!Directory.Exists(strLogPath))
                        Directory.CreateDirectory(strLogPath);
                    string strLogFile = strLogPath + @"\SQL" + DateTime.Now.ToString("yyyyMMdd") + ".log";
                    Task.Factory.StartNew(() =>
                    {
                        //开始写入
                        File.AppendAllText(strLogFile, info.ToString());
                    });
                }
            }
        }

        public void ReaderExecuted(IDbCommand command, DbCommandInterceptionContext<IDataReader> interceptionContext)
        {
            //DateTime startTime = (DateTime)(interceptionContext.DataBag["startTime"]);
            //Console.WriteLine(DateTime.Now.Subtract(startTime).TotalMilliseconds);
            if (interceptionContext.Exception == null)
                DebugSQLInfo("结果行数:" + interceptionContext.Result.FieldCount);
        }

        public void NonQueryExecuting(IDbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            this.BindByName(command);

            DebugSQLInfo(AppendDbCommandInfo(command));
            //DebugSQLInfo(command.CommandText);
        }

        public void NonQueryExecuted(IDbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            if (interceptionContext.Exception == null)
                DebugSQLInfo("影响行数:" + interceptionContext.Result);
        }

        public void ScalarExecuting(IDbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            this.BindByName(command);

            //interceptionContext.DataBag.Add("startTime", DateTime.Now);
            DebugSQLInfo(AppendDbCommandInfo(command));
            //DebugSQLInfo(command.CommandText);
        }

        public void ScalarExecuted(IDbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            //DateTime startTime = (DateTime)(interceptionContext.DataBag["startTime"]);
            //Console.WriteLine(DateTime.Now.Subtract(startTime).TotalMilliseconds);
            if (interceptionContext.Exception == null)
                DebugSQLInfo("返回:" + interceptionContext.Result);
        }

        public static string AppendDbCommandInfo(IDbCommand command)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            foreach (IDbDataParameter param in command.Parameters)
            {
                if (param == null)
                    continue;
                sb.AppendLine();
                object value = null;
                if (param.Value == null || param.Value == DBNull.Value)
                {
                    value = "NULL";
                }
                else
                {
                    value = param.Value;

                    if (param.DbType == DbType.String || param.DbType == DbType.AnsiString || param.DbType == DbType.DateTime)
                        value = "'" + value + "'";
                }

                sb.AppendFormat("{3} {0} {1} = {2};", Enum.GetName(typeof(DbType), param.DbType), param.ParameterName, value, Enum.GetName(typeof(ParameterDirection), param.Direction));
            }

            sb.Append(command.CommandText);

            return sb.ToString();
        }
    }
}