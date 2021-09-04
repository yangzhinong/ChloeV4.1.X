using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChloeDemo
{
    public class DbColCommentAttribute : Attribute
    {
        public string Comment { get; }

        public DbColCommentAttribute(string comment)
        {
            Comment = comment;
        }
    }
}