using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace SharpRNA
{
    /// <summary>
    /// State information passed to IL generators and IConverters
    /// </summary>
    public class ILState
    {
        /// <summary>
        /// Full RNA definition that we're generating code with
        /// </summary>
        public RNA RNA { get; set; }

        /// <summary>
        /// The C# struct that we're generating a conversion to
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// The DNA entity we're generating a conversion from
        /// </summary>
        public Entity Entity { get; set; }

        /// <summary>
        /// The current IL generator to add opcodes into
        /// </summary>
        public ILGenerator Generator { get; set; }

        /// <summary>
        /// The local variable instance of our output C# type to write into
        /// </summary>
        public LocalBuilder Local { get; set; }

        /// <summary>
        /// Blender DNA field we are currently writing conversion opcodes for
        /// </summary>
        public Entity Field { get; set; }

        /// <summary>
        /// The target field of <see cref="Local"/> that we're writing into
        /// </summary>
        public FieldInfo FieldInfo { get; set; }

        /// <summary>
        /// <see cref="DNAAttribute"/> attached to the target <see cref="FieldInfo"/>.
        /// </summary>
        public DNAAttribute DNAInfo { get; set; }
    }
}
