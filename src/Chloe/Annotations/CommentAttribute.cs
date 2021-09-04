using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chloe.Annotations
{
    public class CommentAttribute : Attribute
    {
        public string Comment { get; }

        public CommentAttribute(string comment)
        {
            Comment = comment;
        }
    }
}