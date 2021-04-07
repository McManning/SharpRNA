using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpRNA
{
    /// <summary>
    /// Representation of a pointer to a known type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class NativePointer<T> where T : struct
    {
        public IntPtr Ptr { get; private set; }

        public bool IsNull => Ptr == IntPtr.Zero;

        public T Value => rnaDelegate(Ptr, rna);

        /// <summary>
        /// RNA instance used for conversion
        /// </summary>
        private readonly RNA rna;

        /// <summary>
        /// Transcribe delegate from a DNA type to a C# representation.
        /// </summary>
        private readonly RNA<T>.Delegate rnaDelegate;

        public NativePointer(RNA rna, Entity entity, IntPtr ptr)
        {
            this.rna = rna;
            Ptr = ptr;
            rnaDelegate = RNA<T>.GetDelegate(rna, entity);
        }
    }

    /// <summary>
    /// DNA converter to support <see cref="NativePointer{T}{T}"/> fields.
    /// </summary>
    public class NativePointerConverter : IConverter
    {
        public bool CanConvert(Entity from, Type to)
        {
            return to.IsGenericType && to.GetGenericTypeDefinition() == typeof(NativePointer<>);
        }

        public void GenerateIL(ILState state)
        {
            if (state.Field.Type != EntityType.Pointer)
            {
                throw new Exception(
                    $"Cannot represent a [{state.Field.CType}] as NativePointer"
                );
            }

            // Mesh* mesh -> NativePointer<Mesh> mesh;
            var il = state.Generator;
            var field = state.FieldInfo;

            // Extract type N from `NativeArray<N>`
            var type = field.FieldType.GetGenericArguments()[0];
            var arrayEntity = state.RNA.FindEntityForType(type);
            if (arrayEntity == null)
            {
                throw new Exception(
                    $"Type {type} needs to be [DNA] for use with NativePointer"
                );
            }

            // local.x = NativePointerConversion.Create<N>(rna, ctype, ptr + offset);
            il.Emit(OpCodes.Ldloca_S, state.Local);

            // RNA responsible for conversion
            il.Emit(OpCodes.Ldarg_1);

            // The entity type to convert
            il.Emit(OpCodes.Ldstr, arrayEntity.CType);

            // Indirect pointer stored at offset to convert
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, state.Field.Offset);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ldind_I8);

            il.Emit(OpCodes.Call, typeof(NativePointerConverter).GetMethod("Create").MakeGenericMethod(type));

            il.Emit(OpCodes.Stfld, field);
        }

        public static NativePointer<N> Create<N>(RNA rna, string ctype, IntPtr ptr) where N : struct
        {
            Console.WriteLine($"Create NativePointer<{typeof(N)}>, ptr={ptr}, ctype={ctype}");

            var entity = rna.FindEntityForCType(ctype);
            return new NativePointer<N>(rna, entity, ptr);
        }
    }
}
