using Chloe.Infrastructure;
using Chloe.Infrastructure.Interception;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace ChloeDemo
{
    public class Program
    {
        /* documentation：http://www.52chloe.com/Wiki/Document */

        public static void Main(string[] args)
        {
            /* 添加拦截器，输出 sql 语句极其相应的参数 */

            ////ConfigureMappingType();

            /////* fluent mapping */
            //DbConfiguration.UseTypeBuilders(typeof(UserMap));
            //DbConfiguration.UseTypeBuilders(typeof(CityMap));
            //DbConfiguration.UseTypeBuilders(typeof(ProvinceMap));

            OracleDemo.Run();
        }

        /// <summary>
        /// 配置映射类型。
        /// </summary>
        private static void ConfigureMappingType()
        {
            //MappingTypeBuilder stringTypeBuilder = DbConfiguration.ConfigureMappingType<string>();
            //stringTypeBuilder.HasDbParameterAssembler<String_MappingType>();
        }
    }
}