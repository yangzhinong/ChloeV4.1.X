using Chloe;
using Chloe.Descriptors;
using Chloe.Infrastructure;
using Chloe.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChloeDemo
{
    public class OracleDbManagerTool
    {
        private IDbContext _db;

        public OracleDbManagerTool(IDbContext db)
        {
            _db = db;
        }

        public void InitTable<TEntity>()
        {
            Type entityType = typeof(TEntity);
            var sqlList = this.CreateTableScript(entityType);

            foreach (var sql in sqlList)
            {
                this._db.Session.ExecuteNonQuery(sql);
            }
        }

        public void DropTable<TEntity>()
        {
            Type entityType = typeof(TEntity);
            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(entityType);

            string tableName = typeDescriptor.Table.Name;
            string schcmeName = typeDescriptor.Table.Schema;

            if (_db.Query<SysDbModel.All_Tables>().Any(x => x.Owner == schcmeName && x.Table_Name == tableName))
            {
                _db.Session.ExecuteNonQuery($"drop table {QuoteSchemaAndName(schcmeName, tableName)}");
            }
        }

        private List<string> CreateTableScript(Type entityType)
        {
            List<string> sqlList = new List<string>();
            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(entityType);
            string tableName = typeDescriptor.Table.Name;
            string schcmeName = typeDescriptor.Table.Schema;
            bool tableExists = this._db.SqlQuery<int>($"select count(1) from user_tables where TABLE_NAME = '{tableName.ToUpper()}'").First() > 0;
            if (!tableExists)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"CREATE TABLE {this.QuoteSchemaAndName(schcmeName, tableName)}(");

                string c = "";
                foreach (var propertyDescriptor in typeDescriptor.PrimitivePropertyDescriptors)
                {
                    sb.AppendLine(c);
                    sb.Append($"  {this.BuildColumnPart(propertyDescriptor)}");
                    c = ",";
                }

                sb.AppendLine();
                sb.Append(")");

                sqlList.Add(sb.ToString());

                if (typeDescriptor.PrimaryKeys.Count > 0)
                {
                    string key = typeDescriptor.PrimaryKeys.First().Column.Name;
                    sqlList.Add($"ALTER TABLE {this.QuoteName(tableName)} ADD CHECK ({this.QuoteName(key)} IS NOT NULL)");

                    sqlList.Add($"ALTER TABLE {this.QuoteName(tableName)} ADD PRIMARY KEY ({this.QuoteName(key)})");
                }
            }

            if (typeDescriptor.AutoIncrement != null)
            {
                string seqName = $"{tableName.ToUpper()}_{typeDescriptor.AutoIncrement.Column.Name.ToUpper()}_SEQ".ToUpper();
                bool seqExists = this._db.SqlQuery<int>($"select count(*) from dba_sequences where SEQUENCE_NAME='{seqName}'").First() > 0;
                if (!seqExists)
                {
                    string seqScript = $"CREATE SEQUENCE {this.QuoteName(seqName)} INCREMENT BY 1 MINVALUE 1 MAXVALUE 9999999999999999999999999999 START WITH 1 CACHE 20";

                    sqlList.Add(seqScript);
                }

                string triggerName = $"{seqName.ToUpper()}_TRIGGER";
                string createTrigger = $@"create or replace trigger {triggerName} before insert on {tableName.ToUpper()} for each row
begin
select {seqName.ToUpper()}.nextval into :new.{typeDescriptor.AutoIncrement.Column.Name} from dual;
end;";

                sqlList.Add(createTrigger);
            }

            var seqProperties = typeDescriptor.PrimitivePropertyDescriptors.Where(a => a.HasSequence());
            foreach (var seqProperty in seqProperties)
            {
                if (seqProperty == typeDescriptor.AutoIncrement)
                {
                    continue;
                }

                string seqName = seqProperty.Definition.SequenceName;
                bool seqExists = this._db.SqlQuery<int>($"select count(*) from dba_sequences where SEQUENCE_NAME='{seqName}'").First() > 0;

                if (!seqExists)
                {
                    string seqScript = $"CREATE SEQUENCE {this.QuoteName(seqName)} INCREMENT BY 1 MINVALUE 1 MAXVALUE 9999999999999999999999999999 START WITH 1 CACHE 20";
                    sqlList.Add(seqScript);
                }
            }

            return sqlList;
        }

        private string QuoteName(string name)
        {
            return string.Concat("\"", name.ToUpper(), "\"");
        }

        private string QuoteSchemaAndName(string schema, string name)
        {
            if (string.IsNullOrWhiteSpace(schema))
            {
                return QuoteName(name);
            }
            return $"{QuoteName(schema)}.{QuoteName(name)}";
        }

        private string BuildColumnPart(PrimitivePropertyDescriptor propertyDescriptor)
        {
            var colTypeString = this.GetMappedDbTypeName(propertyDescriptor);
            string part = $"{this.QuoteName(propertyDescriptor.Column.Name)} {colTypeString}";

            if (!propertyDescriptor.IsNullable)
            {
                if (colTypeString.StartsWith("NUM") || colTypeString.StartsWith("BINARY_"))
                {
                    part += " DEFAULT 0 ";
                }
                part += " NOT NULL";
            }
            else
            {
                part += " NULL";
            }

            return part;
        }

        private string GetMappedDbTypeName(PrimitivePropertyDescriptor propertyDescriptor)
        {
            Type type = propertyDescriptor.PropertyType.GetUnderlyingType();
            if (type.IsEnum)
            {
                type = type.GetEnumUnderlyingType();
            }

            if (type == typeof(string))
            {
                int stringLength = propertyDescriptor.Column.Size ?? 2000;
                return $"VARCHAR2({stringLength} BYTE)";
            }

            if (type == typeof(int))
            {
                return "NUMBER(9,0)";
            }

            if (type == typeof(byte))
            {
                return "NUMBER(3,0)";
            }

            if (type == typeof(Int16))
            {
                return "NUMBER(4,0)";
            }

            if (type == typeof(long))
            {
                return "NUMBER(18,0)";
            }

            if (type == typeof(float))
            {
                return "BINARY_FLOAT";
            }

            if (type == typeof(double))
            {
                return "BINARY_DOUBLE";
            }

            if (type == typeof(decimal))
            {
                return "NUMBER";
            }

            if (type == typeof(bool))
            {
                return "NUMBER(9,0)";
            }

            if (type == typeof(DateTime))
            {
                return "DATE";
            }

            if (type == typeof(Guid))
            {
                return "BLOB";
            }

            throw new NotSupportedException(type.FullName);
        }
    }
}