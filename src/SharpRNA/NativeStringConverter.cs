using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpRNA
{
    /// <summary>
    /// DNA converter from <c>char name[64];</c> to <see cref="string"/>.
    /// </summary>
    public class NativeStringConverter : IConverter
    {
        public bool CanConvert(Entity from, Type to)
        {
            return from.Type == EntityType.Array
                && from.CType == "char"
                && to.GetType() == typeof(string);
        }

        public void GenerateIL(GeneratorState state)
        {
            // char name[64]; -> string name;
            var il = state.Generator;
            var field = state.FieldInfo;

            throw new NotImplementedException();
        }
    }
}
