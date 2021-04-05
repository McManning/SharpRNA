using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpRNA
{
    public class NativeArray<T> where T : struct
    {
        public IntPtr Ptr { get; private set; }

        /// <summary>
        /// Number of elements in the array.
        ///
        /// <para>
        ///     This value <b>may be zero</b> if this array was converted
        ///     from a native pointer without a reference size field.
        ///     To access data within this array, call <see cref="Reinterpret{I}(int)"/>
        ///     with a new count.
        /// </para>
        /// </summary>
        public int Count { get; private set; }

        public int ElementSize { get; private set; }

        public bool IsNull => Ptr == IntPtr.Zero;

        /// <summary>
        /// RNA instance used for conversion
        /// </summary>
        private readonly RNA rna;

        /// <summary>
        /// Transcribe delegate from a DNA type to a C# representation.
        /// </summary>
        private readonly RNA<T>.Delegate rnaDelegate;

        public NativeArray(RNA rna, Entity entity, IntPtr ptr, int count)
        {
            this.rna = rna;
            Ptr = ptr;
            Count = count;

            if (entity != null)
            {
                rnaDelegate = RNA<T>.GetDelegate(rna, entity);
                ElementSize = entity.Size;
            }
            else
            {
                rnaDelegate = RNA<T>.GetCopyDelegate();
                ElementSize = Marshal.SizeOf<T>();
            }
        }

        public T this[int index] {
            get {
                if (index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException($"Index outside [0, {Count})");
                }

                // We jump based on the underlying DNA type size - not T's size.
                // That way conversions are aligned to the underlying size.
                return rnaDelegate(IntPtr.Add(Ptr, ElementSize * index), rna);
            }
        }

        public bool Equals(NativeArray<T> other)
        {
            // memcmp ElementSize * Count
            throw new NotImplementedException();
        }

        /// <summary>
        /// Reinterpret this array as a different data type.
        ///
        /// <para>
        ///     If <typeparamref name="I"/> is [DNA] - a converter will be used when
        ///     accessing entries. Otherwise - this will assume that the array was intended
        ///     to be packed as SizeOf(<typeparamref name="I"/>) * <paramref name="count"/>.
        /// </para>
        /// </summary>
        /// <typeparam name="I"></typeparam>
        /// <param name="count"></param>
        /// <returns></returns>
        public NativeArray<I> Reinterpret<I>(int count) where I : struct
        {
            var entity = rna.FindEntityForType(typeof(I));
            return new NativeArray<I>(rna, entity, Ptr, count);
        }
    }

    /// <summary>
    /// DNA converter to support <see cref="NativeArray{T}"/> fields.
    /// </summary>
    public class NativeArrayConverter : IConverter
    {
        public bool CanConvert(Entity from, Type to)
        {
            return to.IsGenericType && to.GetGenericTypeDefinition() == typeof(NativeArray<>);
        }

        public void GenerateIL(ILState state)
        {
            if (state.Field.Type == EntityType.Array)
            {
                EmitFixedArrayAsNativeArrayIL(state);
            }
            else if (state.Field.Type == EntityType.Pointer)
            {
                EmitPointerAsNativeArrayIL(state);
            }
            else
            {
                throw new Exception(
                    $"Cannot represent a primitive/struct [{state.Field.CType}] as NativeArray"
                );
            }
        }

        private static void EmitFixedArrayAsNativeArrayIL(ILState state)
        {
            // char name[64] -> NativeArray<byte> name
            // float co[3] -> NativeArray<float> position

            var il = state.Generator;
            var field = state.FieldInfo;

            // Extract type N from `NativeArray<N>`
            var type = field.FieldType.GetGenericArguments()[0];
            var arrayEntity = state.RNA.FindEntityForType(type);

            var constantArraySize = state.Field.Count;

            // local.x = Factory.CreateNativeArray<N>(rna, ctype, count, ptr + offset);
            il.Emit(OpCodes.Ldloca_S, state.Local);

            // RNA responsible for conversion
            il.Emit(OpCodes.Ldarg_1);

            // The entity type to convert
            il.Emit(OpCodes.Ldstr, arrayEntity?.CType ?? "");

            // Number of elements in the array
            il.Emit(OpCodes.Ldc_I4, constantArraySize);

            // Pointer stored at offset to convert
            il.Emit(OpCodes.Ldarg_0); // Load source pointer onto stack
            il.Emit(OpCodes.Ldc_I4, state.Field.Offset); // Push read field offset onto stack
            il.Emit(OpCodes.Add); // Add offset to pointer

            il.Emit(OpCodes.Call, typeof(NativeArrayConverter).GetMethod("Create").MakeGenericMethod(type));

            il.Emit(OpCodes.Stfld, field); // Store retval in local field
        }

        private static void EmitPointerAsNativeArrayIL(ILState state)
        {
            // char* name -> NativeArray<byte> name
            // MVert* verts -> NativeArray<Vertex> vertices

            var il = state.Generator;
            var field = state.FieldInfo;

            // Extract type N from `NativeArray<N>`
            var type = field.FieldType.GetGenericArguments()[0];
            var arrayEntity = state.RNA.FindEntityForType(type);

            var arraySizeLocal = il.DeclareLocal(typeof(int));

            if (!string.IsNullOrEmpty(state.DNAInfo.SizeField))
            {
                var sizeField = state.Entity.Fields[state.DNAInfo.SizeField];
                // Sanity check - ensure it's int-like.
                if (sizeField.CType != "int")
                {
                    // TODO: Friendly errors
                    // TODO: Might have uints? If so, we need a different opcode to load and local type.
                    throw new Exception("Expected int SizeField");
                }

                // Add opcodes to extract the element count from another field in the struct.

                // arraySizeLocal = *(int*)(ptr + sizeFieldOffset)
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, sizeField.Offset);
                il.Emit(OpCodes.Add);
                il.Emit(OpCodes.Ldind_I4);
                il.Emit(OpCodes.Stloc, arraySizeLocal);
            }

            // local.x = Factory.CreateNativeArray<N>(rna, ctype, count, ptr + offset);
            il.Emit(OpCodes.Ldloca_S, state.Local);

            // RNA responsible for conversion
            il.Emit(OpCodes.Ldarg_1);

            // The entity type to convert
            il.Emit(OpCodes.Ldstr, arrayEntity?.CType ?? "");

            // Number of elements in the array
            il.Emit(OpCodes.Ldloc, arraySizeLocal);

            // Pointer stored at offset to convert
            il.Emit(OpCodes.Ldarg_0); // Load source pointer onto stack
            il.Emit(OpCodes.Ldc_I4, state.Field.Offset); // Push read field offset onto stack
            il.Emit(OpCodes.Add); // Add offset to pointer
            il.Emit(OpCodes.Ldind_I8); // Replace value as a pointer to the start of the array and push back

            il.Emit(OpCodes.Call, typeof(NativeArrayConverter).GetMethod("Create").MakeGenericMethod(type));

            il.Emit(OpCodes.Stfld, field); // Store retval in local field
        }

        public static NativeArray<N> Create<N>(RNA rna, string ctype, int count, IntPtr ptr) where N : struct
        {
            var entity = rna.FindEntityForCType(ctype);
            return new NativeArray<N>(rna, entity, ptr, count);
        }
    }
}
