using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRNA
{
    /// <summary>
    /// DNA binding for a C# struct or field
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Struct | AttributeTargets.Field,
        AllowMultiple = false
    )]
    public class DNAAttribute : Attribute
    {
        public string Name { get; set; }

        public string SizeField { get; set; }

        public int SizeConst { get; set; }

        public DNAAttribute(string name)
        {
            Name = name;
        }
    }
}
