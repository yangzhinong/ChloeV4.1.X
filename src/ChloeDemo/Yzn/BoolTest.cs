using Chloe;
using Chloe.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChloeDemo.Yzn
{
    [Table("UNITTEST_BOOLTEST", "YZN")]
    public class BoolTest
    {
        public bool? IsEnable { get; set; }
        public DateTime CreateTime { get; set; }

        /*
          CREATE TABLE Yzn.UNITTEST_BOOLTEST (
             "ISENABLE" NUMBER(1,0) ,
             "CREATETIME" TIMESTAMP(4)
             )
         */

        public static void Run(IDbContext db)
        {
            //var o = new OracleDbManagerTool(db);
            //o.DropTable<BoolTest>();
            //o.InitTable<BoolTest>();

            var sql = db.Query<BoolTest>().ToString();
            db.Insert(new BoolTest());
            db.Insert(new BoolTest() { CreateTime = DateTime.Now });
        }
    }
}