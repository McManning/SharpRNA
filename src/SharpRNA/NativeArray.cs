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
        public Entity Entity { get; private set; }

        public IntPtr Ptr { get; private set; }

        public int Count { get; private set; }

        public int ElementSize { get; private set; }

        public bool IsNull => Ptr == IntPtr.Zero;

        /// <summary>
        /// Translation delegate from a DNA type to a C# representation.
        /// </summary>
        private readonly DNAToStructure<T>.Delegate converter;

        /// <summary>
        ///
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="entity"></param>
        /// <param name="count">
        ///     Number of <see cref="Entity"/> represented by this array.
        /// </param>
        public NativeArray(IntPtr ptr, Entity entity, int count)
        {
            Ptr = ptr;
            Entity = entity;
            Count = count;

            if (entity == null)
            {
                throw new ArgumentNullException($"Expected Entity");
            }

            converter = DNAToStructure<T>.GetConverter(entity);
            ElementSize = entity.Size;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="count">Number of <typeparamref name="T"/> represented by this array</param>
        public NativeArray(IntPtr ptr, int count)
        {
            Ptr = ptr;
            Count = count;

            // Use direct memcpy as the converter since there's no DNA conversion
            converter = DNAToStructure<T>.GetCopyConverter();
            ElementSize = Marshal.SizeOf<T>();
        }

        public T this[int index] {
            get {
                if (index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException($"Index outside [0, {Count})");
                }

                // We jump based on the underlying DNA type size - not T's size.
                // That way conversions are aligned to the underlying size.
                return converter(IntPtr.Add(Ptr, ElementSize * index));
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
            var entity = DNAVersion.FindEntityForType<I>();
            return new NativeArray<I>(Ptr, entity, count);
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

        public void GenerateIL(GeneratorState state)
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

        private static void EmitFixedArrayAsNativeArrayIL(GeneratorState state)
        {
            // char name[64] -> NativeArray<byte> name
            // float co[3] -> NativeArray<float> position

            var il = state.Generator;
            var field = state.FieldInfo;

            // Extract type N from `NativeArray<N>`
            var type = field.FieldType.GetGenericArguments()[0];
            var arrayEntity = DNAVersion.FindEntityForType(type);

            var constantArraySize = state.Field.Count;

            // local.x = Factory.CreateNativeArray<N>(ptr + offset, constantArraySize, entityId);
            il.Emit(OpCodes.Ldloca_S, state.Local);

            il.Emit(OpCodes.Ldarg_0); // Load source pointer onto stack
            il.Emit(OpCodes.Ldc_I4, state.Field.Offset); // Push read field offset onto stack
            il.Emit(OpCodes.Add); // Add offset to pointer

            il.Emit(OpCodes.Ldc_I4, constantArraySize);
            il.Emit(OpCodes.Ldc_I4, arrayEntity != null ? arrayEntity.ID : -1); // Push entity ID onto the stack

            // TODO: Direct constructor call? Would it be quicker? (probably)
            il.Emit(OpCodes.Call, typeof(NativeArrayConverter).GetMethod("Create").MakeGenericMethod(type));

            il.Emit(OpCodes.Stfld, field); // Store retval in local field
        }

        private static void EmitPointerAsNativeArrayIL(GeneratorState state)
        {
            // char* name -> NativeArray<byte> name
            // MVert* verts -> NativeArray<Vertex> vertices

            var il = state.Generator;
            var field = state.FieldInfo;

            // Extract type N from `NativeArray<N>`
            var type = field.FieldType.GetGenericArguments()[0];
            var arrayEntity = DNAVersion.FindEntityForType(type);

            if (state.DNA.SizeField.Length < 1)
            {
                throw new Exception(
                    $"A [DNA(SizeField)] on [{field.Name}] is required to use NativeArray when the underlying type is a pointer"
                );
            }

            var sizeField = state.Entity.Fields[state.DNA.SizeField];

            // Sanity check - ensure it's int-like.
            if (sizeField.CType != "int")
            {
                // TODO: Friendly errors
                throw new Exception("Expected int SizeField");
            }

            // TODO: Might have uints? If so, we need a different opcode to load and local type.

            // Add opcodes to extract the count from another field in the struct.

            // arraySizeLocal = *(int*)(ptr + sizeFieldOffset)
            var arraySizeLocal = il.DeclareLocal(typeof(int));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, sizeField.Offset);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ldind_I4);
            il.Emit(OpCodes.Stloc, arraySizeLocal);

            // local.x = Factory.CreateNativeArray<N>(ptr + offset, arraySizeLocal, entityId);
            il.Emit(OpCodes.Ldloca_S, state.Local);

            il.Emit(OpCodes.Ldarg_0); // Load source pointer onto stack
            il.Emit(OpCodes.Ldc_I4, state.Field.Offset); // Push read field offset onto stack
            il.Emit(OpCodes.Add); // Add offset to pointer
            il.Emit(OpCodes.Ldind_I8); // Replace value as a pointer to the start of the array and push back

            il.Emit(OpCodes.Ldloc, arraySizeLocal);
            il.Emit(OpCodes.Ldc_I4, arrayEntity != null ? arrayEntity.ID : -1); // Push entity ID onto the stack

            il.Emit(OpCodes.Call, typeof(NativeArrayConverter).GetMethod("Create").MakeGenericMethod(type));

            il.Emit(OpCodes.Stfld, field); // Store retval in local field
        }

        public static NativeArray<N> Create<N>(IntPtr ptr, int count, int entityId) where N : struct
        {
            Console.WriteLine($"Create NativeArray<{typeof(N)}>, ptr={ptr}, count={count}, entityId={entityId}");

            // If an ID is supplied, do a lookup and attach to the array.
            if (entityId >= 0)
            {
                var entity = DNAVersion.FindEntityByID(entityId);
                return new NativeArray<N>(ptr, entity, count);
            }

            return new NativeArray<N>(ptr, count);
        }
    }
}
