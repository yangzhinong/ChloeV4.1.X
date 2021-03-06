using Chloe.Extension;
using System.Data;

namespace Chloe
{
    public static class DbSessionExtension
    {
        /// <summary>
        /// dbSession.ExecuteDataTable("select Age from Users where Id=@Id", new { Id = 1 })
        /// </summary>
        /// <param name="dbSession"></param>
        /// <param name="cmdText"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public static DataTable ExecuteDataTable(this IDbSession dbSession, string cmdText, object parameter)
        {
            return ExecuteDataTable(dbSession, cmdText, Utils.BuildParams(dbSession.DbContext, parameter));
        }
        public static DataTable ExecuteDataTable(this IDbSession dbSession, string cmdText, params DbParam[] parameters)
        {
            using (var reader = dbSession.ExecuteReader(cmdText, parameters))
            {
                DataTable dt = DbHelper.FillDataTable(reader);
                return dt;
            }
        }

        /// <summary>
        /// dbSession.ExecuteDataTable("select Age from Users where Id=@Id", CommandType.Text, new { Id = 1 })
        /// </summary>
        /// <param name="dbSession"></param>
        /// <param name="cmdText"></param>
        /// <param name="cmdType"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public static DataTable ExecuteDataTable(this IDbSession dbSession, string cmdText, CommandType cmdType, object parameter)
        {
            return ExecuteDataTable(dbSession, cmdText, cmdType, Utils.BuildParams(dbSession.DbContext, parameter));
        }
        public static DataTable ExecuteDataTable(this IDbSession dbSession, string cmdText, CommandType cmdType, params DbParam[] parameters)
        {
            using (var reader = dbSession.ExecuteReader(cmdText, cmdType, parameters))
            {
                DataTable dt = DbHelper.FillDataTable(reader);
                return dt;
            }
        }

        /// <summary>
        /// dbSession.ExecuteDataSet("select Age from Users where Id=@Id", new { Id = 1 })
        /// </summary>
        /// <param name="dbSession"></param>
        /// <param name="cmdText"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public static DataSet ExecuteDataSet(this IDbSession dbSession, string cmdText, object parameter)
        {
            return ExecuteDataSet(dbSession, cmdText, Utils.BuildParams(dbSession.DbContext, parameter));
        }
        public static DataSet ExecuteDataSet(this IDbSession dbSession, string cmdText, params DbParam[] parameters)
        {
            using (var reader = dbSession.ExecuteReader(cmdText, parameters))
            {
                DataSet ds = DbHelper.FillDataSet(reader);
                return ds;
            }
        }

        /// <summary>
        /// dbSession.ExecuteDataSet("select Age from Users where Id=@Id", CommandType.Text, new { Id = 1 })
        /// </summary>
        /// <param name="dbSession"></param>
        /// <param name="cmdText"></param>
        /// <param name="cmdType"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public static DataSet ExecuteDataSet(this IDbSession dbSession, string cmdText, CommandType cmdType, object parameter)
        {
            return ExecuteDataSet(dbSession, cmdText, cmdType, Utils.BuildParams(dbSession.DbContext, parameter));
        }
        public static DataSet ExecuteDataSet(this IDbSession dbSession, string cmdText, CommandType cmdType, params DbParam[] parameters)
        {
            using (var reader = dbSession.ExecuteReader(cmdText, cmdType, parameters))
            {
                DataSet ds = DbHelper.FillDataSet(reader);
                return ds;
            }
        }
    }
}
