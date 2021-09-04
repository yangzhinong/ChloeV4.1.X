using Chloe.Annotations;
using Chloe.Descriptors;
using Chloe.Infrastructure;
using Chloe.Reflection;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using syscom = System.ComponentModel;

namespace Chloe.Oracle
{
    public class Dbmaintain
    {
        private IDbContext _db;

        public Dbmaintain(IDbContext db)
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

        /// <summary>
        /// 无表添加表，有表检查与模型的差异，添加表比模型少的列
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        public void SafteInitTablbeAndColumns<TEntity>()
        {
            Type entityType = typeof(TEntity);
            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(entityType);

            string tableName = typeDescriptor.Table.Name;
            string schcmeName = typeDescriptor.Table.Schema;

            if (_db.Query<SysDbModel.All_Tables>()
                   .Any(x => x.Owner == schcmeName && x.Table_Name == tableName))
            {
                var oldColumns = _db.Query<SysDbModel.All_Tab_Cols>()
                                    .Where(x => x.Owner == schcmeName &&
                                               x.Table_Name == tableName)
                                    .Select(x => x.Column_Name.ToUpper())
                                    .ToList();
                foreach (var propertyDescriptor in typeDescriptor.PrimitivePropertyDescriptors)
                {
                    var colName = propertyDescriptor.Column.Name.ToUpper();
                    if (!oldColumns.Contains(colName))
                    {
                        var colpart = BuildColumnPart(propertyDescriptor);
                        var schcmeTable = QuoteSchemaAndName(schcmeName, tableName);
                        var sql = $"ALTER TABLE {schcmeTable} ADD ({colpart})";
                        _db.Session.ExecuteNonQuery(sql);
                        var prop = entityType.GetProperty(propertyDescriptor.Column.Name);
                        var desc = GetPropertyComment(prop);
                        if (!string.IsNullOrWhiteSpace(desc))
                        {
                            sql = $"COMMENT ON COLUMN {schcmeTable}.{colName} IS '{SafeSqlString(desc)}'";
                            _db.Session.ExecuteNonQuery(sql);
                        }
                    }
                }
            }
            else
            {
                InitTable<TEntity>();
            }
        }

        private string GetPropertyComment(PropertyInfo prop)
        {
            if (prop == null) return "";
            if (Attribute.GetCustomAttribute(prop, typeof(CommentAttribute))
                                is CommentAttribute atrrComment)
            {
                return atrrComment.Comment;
            }
            else
            {
                if (Attribute.GetCustomAttribute(prop, typeof(DisplayAttribute))
                                is DisplayAttribute attrDisplay)
                {
                    if (string.IsNullOrWhiteSpace(attrDisplay.Description))
                    {
                        return attrDisplay.Description;
                    }
                    else
                    {
                        return attrDisplay.Name;
                    }
                }
                if (Attribute.GetCustomAttribute(prop, typeof(syscom.DisplayNameAttribute))
                                    is syscom.DisplayNameAttribute attrDisplayName)
                {
                    return attrDisplayName.DisplayName;
                }
            }
            return "";
        }

        private string SafeSqlString(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return s;
            }
            return s.Replace("'", "''");
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
                    var keys = typeDescriptor.PrimaryKeys.Select(x => x.Column.Name).ToList();
                    foreach (string key in keys)
                    {
                        sqlList.Add($"ALTER TABLE {this.QuoteName(tableName)} ADD CHECK ({this.QuoteName(key)} IS NOT NULL)");
                    }
                    var keyQuoteJion = string.Join(",", keys.Select(x => QuoteName(x)));
                    sqlList.Add($"ALTER TABLE {this.QuoteName(tableName)} ADD PRIMARY KEY ({keyQuoteJion})");
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
                if (propertyDescriptor.Column.Precision.HasValue)
                {
                    var precision = propertyDescriptor.Column.Precision.Value;
                    if (precision > 0)
                    {
                        var scale = propertyDescriptor.Column.Scale ?? 0;
                        return $"NUMBER({precision},{scale})";
                    }
                }
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