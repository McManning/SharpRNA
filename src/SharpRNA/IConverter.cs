using System;
using System.Reflection;

namespace SharpRNA
{
    /// <summary>
    /// Class that converts from a DNA entity to a C# type
    /// </summary>
    public interface IConverter
    {
        /// <summary>
        /// Can this converter convert the given DNA entity to the given type.
        /// </summary>
        public bool CanConvert(Entity from, Type to);

        /// <summary>
        /// Generate the IL required to perform the conversion.
        /// State will contain everything needed to build the IL
        /// (local variable information, FieldInfo, DNA attribute values, etc)
        /// </summary>
        public void GenerateIL(ILState state);
    }
}
