using System;

namespace Chloe.DbExpressions
{
    [System.Diagnostics.DebuggerDisplay("Name = {Name} isAlias={IsAlias}")]
    public class DbTable
    {
        private string _name;
        private string _schema;
        private bool _isAlias;

        public DbTable(string name, bool isAlias = false)
            : this(name, null, isAlias)
        {
        }

        public DbTable(string name, string schema, bool isAlias = false)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Table name could not be null or empty.");
            }

            this._name = name;
            this._schema = schema;
            this._isAlias = isAlias;
        }

        public string Name { get { return this._name; } }
        public string Schema { get { return this._schema; } }

        public bool IsAlias { get => _isAlias; }
    }
}