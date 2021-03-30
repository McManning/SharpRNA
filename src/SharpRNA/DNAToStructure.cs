using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace SharpRNA
{
    public class GeneratorState
    {
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
        public DNAAttribute DNA { get; set; }
    }

    public static class DNAToStructure<T>
    {
        public delegate T Delegate(IntPtr ptr);

        private static Delegate copyConverter;

        private static readonly Dictionary<Entity, Delegate> cache
            = new Dictionary<Entity, Delegate>();

        #region Public API

        public static T Convert(Entity entity, IntPtr ptr)
        {
            var converter = GetConverter(entity);
            return converter(ptr);
        }

        public static Delegate GetConverter(Entity entity)
        {
            if (cache.TryGetValue(entity, out Delegate converter))
            {
                return converter;
            }

            // Create IL for Entity -> T, store in cache and return
            converter = CreateConverter(entity);
            cache[entity] = converter;
            return converter;
        }

        public static Delegate GetCopyConverter()
        {
            if (copyConverter == null)
            {
                copyConverter = CreateCopyConverter();
            }

            return copyConverter;
        }

        #endregion

        #region IL Generator

        /// <summary>
        /// Create a delegate instance that performs an as-is memcpy
        /// instead of trying to convert from a DNA type.
        /// </summary>
        /// <returns></returns>
        static Delegate CreateCopyConverter()
        {
            var type = typeof(T);

            var method = new DynamicMethod(
                "DNAToStructureCopy<" + type.FullName + ">",
                type,
                new Type[] { typeof(IntPtr) },
                typeof(Delegate).Module
            );

            var il = method.GetILGenerator();
            var localStruct = il.DeclareLocal(typeof(T));

            // Note this will fail for unmarshallable types
            // e.g. the struct contains a NativeArray within it.
            var size = Marshal.SizeOf<T>();

            // Destination, source, then size on the stack
            il.Emit(OpCodes.Ldloca_S, localStruct);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, size);

            il.Emit(OpCodes.Cpblk);

            il.Emit(OpCodes.Ldloc, localStruct);
            il.Emit(OpCodes.Ret);

            return (Delegate)method.CreateDelegate(typeof(Delegate));
        }

        static Delegate CreateConverter(Entity entity)
        {
            var type = typeof(T);

            var method = new DynamicMethod(
                "DNAToStructureDyn<" + type.FullName + ">",
                type,
                new Type[] { typeof(IntPtr) },
                typeof(Entity).Module
            );

            var il = method.GetILGenerator();
            var localStruct = il.DeclareLocal(type);

            var state = new GeneratorState
            {
                Type = type,
                Entity = entity,
                Generator = il,
                Local = localStruct,
            };

            // Generate IL for all fields that have a [DNA] attribute
            // that map that field to one or more Blender DNA fields.
            foreach (var field in type.GetFields())
            {
                var dna = field.GetCustomAttribute<DNAAttribute>();
                if (dna != null)
                {
                    state.Field = entity.Fields[dna.Name];
                    state.DNA = dna;
                    state.FieldInfo = field;
                    EmitCopyFieldIL(state);
                }
            }

            // Push the local struct copy onto the stack and return
            il.Emit(OpCodes.Ldloc, localStruct);
            il.Emit(OpCodes.Ret);

            return (Delegate)method.CreateDelegate(typeof(Delegate));
        }

        private static void EmitCopyFieldIL(GeneratorState state)
        {
            var field = state.FieldInfo;
            var type = field.FieldType;

            if (type.IsPrimitive)
            {
                EmitCopyPrimitiveFieldIL(state);
            }

            // Custom converters - eventually this'll be a list of
            // registered converters to iterate through and check against.
            // (not in this class though - in a non-generic IL generator class)
            var nativeArrayConverter = new NativeArrayConverter();
            if (nativeArrayConverter.CanConvert(state.Entity, type))
            {
                nativeArrayConverter.GenerateIL(state);
                return;
            }

            var nativeListConverter = new NativeListConverter();
            if (nativeListConverter.CanConvert(state.Entity, type))
            {
                nativeListConverter.GenerateIL(state);
                return;
            }

            if (type.IsValueType)
            {
                // If the nested struct has a [DNA] attribute we
                // need to call a custom IL executor on that as well.
                var fieldTypeDNA = type.GetCustomAttribute<DNAAttribute>();
                if (fieldTypeDNA != null)
                {
                    // [DNA] on the referenced struct would probably emit this BEFORE
                    // running any custom converters for type checking.
                    EmitCopyDNAStructIL(state, field);
                }
                else // Regular struct - use a basic memcpy and hope it aligns.
                {
                    EmitCopyStructIL(state);
                }
            }
            else
            {
                throw new Exception(
                    $"DNA attribute on unsupported field [{field.Name}] with type [{field.FieldType}]"
                );
            }
        }

        private static void EmitCopyPrimitiveFieldIL(GeneratorState state)
        {
            // Mapping of C# primitive types to load indirect opcodes.
            var ldind = new Dictionary<Type, OpCode>()
            {
                { typeof(bool),     OpCodes.Ldind_I1 },
                { typeof(sbyte),    OpCodes.Ldind_I1 },
                { typeof(byte),     OpCodes.Ldind_U1 },
                { typeof(char),     OpCodes.Ldind_U2 },
                { typeof(short),    OpCodes.Ldind_I2 },
                { typeof(ushort),   OpCodes.Ldind_U2 },
                { typeof(int),      OpCodes.Ldind_I4 },
                { typeof(uint),     OpCodes.Ldind_U4 },
                { typeof(long),     OpCodes.Ldind_I8 },
                { typeof(ulong),    OpCodes.Ldind_I8 },
                { typeof(double),   OpCodes.Ldind_R8 },
                { typeof(float),    OpCodes.Ldind_R4 },
                { typeof(IntPtr),   OpCodes.Ldind_I8 },
                { typeof(UIntPtr),  OpCodes.Ldind_I8 },
            };

            var il = state.Generator;
            var field = state.FieldInfo;

            // TODO: CType missing (because it's at the root of version lookup)
            Console.WriteLine($"Emit {state.Entity.CType}.{state.DNA.Name}@{state.Field.Offset} -> {field.Name} [{ldind[field.FieldType]}]:");

            // local.x = *(float*)(ptr + offset);
            il.Emit(OpCodes.Ldloca_S, state.Local); // Load address of local variable
            il.Emit(OpCodes.Ldarg_0); // Load pointer onto stack
            il.Emit(OpCodes.Ldc_I4, state.Field.Offset); // Push read field offset onto stack
            il.Emit(OpCodes.Add); // Add offset to pointer

            il.EmitWriteLine($"Copy {state.Entity.CType}.{state.DNA.Name}@{state.Field.Offset} -> {field.Name} [{ldind[field.FieldType]}]:");

            //il.Emit(OpCodes.Ldc_I4, state.Field.Offset);
            //il.Emit(OpCodes.Call, typeof(Wtf).GetMethod("Dump"));

            il.Emit(ldind[field.FieldType]); // indirect load a type matching our field
            il.Emit(OpCodes.Stfld, field); // Store onto field
        }

        private static void EmitCopyStructIL(GeneratorState state)
        {
            var il = state.Generator;
            var field = state.FieldInfo;

            // memcpy(&local.x, ptr + offset, sizeof(float))
            il.Emit(OpCodes.Ldloca_S, state.Local);
            il.Emit(OpCodes.Ldflda, field); // Load reference to local field onto stack

            il.Emit(OpCodes.Ldarg_0); // Load source pointer onto stack
            il.Emit(OpCodes.Ldc_I4, state.Field.Offset); // Push read field offset onto stack
            il.Emit(OpCodes.Add); // Add offset to pointer

            int size = Marshal.SizeOf(field.FieldType);
            il.Emit(OpCodes.Ldc_I4, size); // Push destination struct size onto stack
            il.Emit(OpCodes.Cpblk); // Block copy from source pointer -> struct field
        }

        private static void EmitCopyDNAStructIL(GeneratorState state, FieldInfo field)
        {
            var il = state.Generator;

            // Find the matching DNA entity type from our version info
            if (state.Field.CType == null)
            {
                throw new Exception($"Null ctype for {field.Name} with offset {state.Field.Offset}");
            }

            // Create a `DNAToStructure<N>.Delegate` where N is the field's struct type.
            // If we never had one before for this type - this will recursively generate
            // the IL of the converter.
            // TODO: ... might be a problem for recursive references?
            /*var converter = typeof(DNAToStructure<>)
                    .MakeGenericType(field.FieldType)
                    .GetMethod("GetConverter")
                    .Invoke(null, new object[] { entity });
            */

            // local.x = DNAToStructure<N>.Delegate(ptr + offset);
            //var fieldTypeLocal = il.DeclareLocal(field.FieldType);

            il.Emit(OpCodes.Ldloca_S, state.Local);

            il.Emit(OpCodes.Ldarg_0); // Load source pointer onto stack
            il.Emit(OpCodes.Ldc_I4, state.Field.Offset); // Push read field offset onto stack
            il.Emit(OpCodes.Add); // Add offset to pointer

            // Delegate to DNAVersion.FromDNA<T>(ptr) to resolve and return the type.
            // This isn't as optimal as just generating the delegate right in this
            // opcode emitter and passing it down - but it ends up out of scope
            // once we actually run the opcodes.
            il.Emit(OpCodes.Call, typeof(DNAVersion).GetMethod("FromDNA").MakeGenericMethod(field.FieldType));
            // il.Emit(OpCodes.Stloc, fieldTypeLocal);

            // skip local variable - store directly from the call.
            il.Emit(OpCodes.Stfld, field);

            // TODO: Can I write directly to Call -> store?

            // &local.x = localOfXType
            //il.Emit(OpCodes.Ldloca_S, state.Local); // Push reference to local field onto the stack
            //il.Emit(OpCodes.Ldloc, fieldTypeLocal); // Push the field type local value onto the stack
            //il.Emit(OpCodes.Stfld, field); // Store field type local value into the field
        }

        #endregion
    }
}
