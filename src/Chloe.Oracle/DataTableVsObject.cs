using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Chloe.Oracle
{
    /// <summary>
    /// 数据表与实体列表互换工具（针对Oracle表大写进行了处理）
    /// </summary>
    public static class DataTableVsObject
    {
        public enum EnuFieldNameType
        {
            Normal,
            OracleUpperCase,
        }

        /// <summary>
        /// 把列表转成datatable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="toType"></param>
        /// <param name="fAcceptChanges"></param>
        /// <returns></returns>
        public static DataTable ToDataTable<T>(this IEnumerable<T> collection,
                                               EnuFieldNameType toType = EnuFieldNameType.OracleUpperCase,
                                               bool fAcceptChanges = true)
        {
            var props = typeof(T).GetProperties();
            var dt = new DataTable();
            if (toType == EnuFieldNameType.OracleUpperCase)
            {
                dt.Columns.AddRange(props.Select(p => new DataColumn(p.Name.ToUpper(), p.PropertyType)).ToArray());
            }
            else
            {
                dt.Columns.AddRange(props.Select(p => new DataColumn(p.Name, p.PropertyType)).ToArray());
            }

            if (collection.Count() > 0)
            {
                for (int i = 0; i < collection.Count(); i++)
                {
                    ArrayList tempList = new ArrayList();
                    foreach (PropertyInfo pi in props)
                    {
                        object obj = pi.GetValue(collection.ElementAt(i), null);
                        tempList.Add(obj);
                    }
                    object[] array = tempList.ToArray();
                    dt.LoadDataRow(array, fAcceptChanges);
                }
            }
            return dt;
        }

        /// <summary>
        /// 忽略字段大小写转换datatable到列表
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static List<T> ToList<T>(this DataTable dt)
        {
            var dataColumn = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            var properties = typeof(T).GetProperties();
            Dictionary<PropertyInfo, string> dicFind = new Dictionary<PropertyInfo, string>();
            const string NOFOUND = "-1";
            return dt.AsEnumerable().Select(row =>
            {
                var t = Activator.CreateInstance<T>();
                foreach (var p in properties)
                {
                    if (dicFind.ContainsKey(p))
                    {
                        if (dicFind[p] == NOFOUND) continue;
                        if (!p.CanWrite)
                            continue;
                        WatieObjectFromDataRow(row, dicFind, t, p);
                    }
                    else
                    {
                        var findCol = dataColumn.Where(x => string.Compare(x, p.Name, true) == 0).FirstOrDefault();
                        if (string.IsNullOrWhiteSpace(findCol))
                        {
                            dicFind.Add(p, NOFOUND);
                        }
                        else
                        {
                            if (!p.CanWrite)
                                continue;
                            WatieObjectFromDataRow(row, dicFind, t, p);
                        }
                    }
                }
                return t;
            }).ToList();
        }

        private static void WatieObjectFromDataRow<T>(DataRow row, Dictionary<PropertyInfo, string> dicFind, T t, PropertyInfo p)
        {
            object value = row[dicFind[p]];
            Type type = p.PropertyType;

            if (value != DBNull.Value)
            {
                p.SetValue(t, Convert.ChangeType(value, type), null);
            }
        }
    }
}