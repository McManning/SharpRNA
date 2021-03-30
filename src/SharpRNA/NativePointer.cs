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
        public Entity Entity { get; private set; }

        public IntPtr Ptr { get; private set; }

        public bool IsNull => Ptr == IntPtr.Zero;

        public T Value => converter(Ptr);

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
        public NativePointer(IntPtr ptr, Entity entity)
        {
            Ptr = ptr;
            Entity = entity;

            if (entity == null)
            {
                throw new ArgumentNullException($"Expected Entity");
            }

            converter = DNAToStructure<T>.GetConverter(entity);
        }
    }

    /// <summary>
    /// DNA converter to support <see cref="NativePointer{T}{T}"/> fields.
    /// </summary>
    public class NativePointerConverter : IConverter
    {
        //public NativePointerConverter(DNA dna)
        //{
            // can use the DNA instance in Create<> to pass down a factory.
            // which is why I did Entity.Convert<type> instead of the alternative.
            // Because I need that DNA info baked into the entity...
        //}

        public bool CanConvert(Entity from, Type to)
        {
            return to.IsGenericType && to.GetGenericTypeDefinition() == typeof(NativePointer<>);
        }

        public void GenerateIL(GeneratorState state)
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
            var arrayEntity = DNAVersion.FindEntityForType(type);
            if (arrayEntity == null)
            {
                throw new Exception(
                    $"Type {type} needs to be [DNA] for use with NativePointer"
                );
            }

            // local.x = NativePointerConversion.Create<N>(ptr + offset, entityId);
            il.Emit(OpCodes.Ldloca_S, state.Local);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, state.Field.Offset);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ldind_I8);

            il.Emit(OpCodes.Ldc_I4, arrayEntity.ID);

            il.Emit(OpCodes.Call, typeof(NativePointerConverter).GetMethod("Create").MakeGenericMethod(type));

            il.Emit(OpCodes.Stfld, field);
        }

        public static NativePointer<N> Create<N>(IntPtr ptr, int entityId) where N : struct
        {
            Console.WriteLine($"Create NativePointer<{typeof(N)}>, ptr={ptr}, entityId={entityId}");

            var entity = DNAVersion.FindEntityByID(entityId);
            return new NativePointer<N>(ptr, entity);
        }
    }
}
