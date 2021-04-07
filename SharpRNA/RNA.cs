using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace SharpRNA
{
    public class RNAGeneratorException : Exception
    {
        public RNAGeneratorException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Primary API for converting native structures in memory described by DNA to C# types
    /// </summary>
    public class RNA
    {
        // Attached to a specific DNA version

        public DNA DNA { get; private set; }

        private readonly static Dictionary<Type, Entity> typeCache = new Dictionary<Type, Entity>();

        public static RNA FromDNA(TextReader reader, string version = null)
        {
            DNA dna;

            if (!string.IsNullOrEmpty(version))
            {
                var versions = Serializer.FromVersionedYAML(reader);
                dna = versions.Find(version);
                if (dna == null)
                {
                    // TODO: Better error message that includes the ranges checked
                    // (similar to version errors for npm / composer)
                    throw new ArgumentException(
                        $"Cannot resolve DNA version [{version}]"
                    );
                }
            }
            else
            {
                dna = Serializer.FromYAML(reader);
            }

            return new RNA()
            {
                DNA = dna,
            };
        }

        /// <summary>
        /// Convert a native DNA type in memory to a managed C# type <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ptr"></param>
        /// <returns></returns>
        public T Transcribe<T>(IntPtr ptr)
        {
            var entity = FindEntityForType(typeof(T));
            if (entity == null)
            {
                throw new Exception($"Missing [DNA] attribute for type {typeof(T)}");
            }

            // Run custom IL to generate T and return it.
            return RNA<T>.Transcribe(this, entity, ptr);
        }

        public Entity FindEntityForCType(string ctype)
        {
            if (DNA.Entities.TryGetValue(ctype, out var entity))
            {
                return entity;
            }

            return null;
        }

        public Entity FindEntityForType(Type type)
        {
            if (!typeCache.ContainsKey(type))
            {
                // Evaluate the type mapping for T and cache
                var attr = type.GetCustomAttributes(typeof(DNAAttribute), false) as DNAAttribute[];
                if (attr.Length < 1)
                {
                    return null; // Not a mapped type
                }

                typeCache[type] = FindEntityForCType(attr[0].Name);
            }

            return typeCache[type];
        }
    }

    /// <summary>
    /// RNA converter(s) to a specific managed type from entity definitions
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class RNA<T>
    {
        public delegate T Delegate(IntPtr ptr, RNA rna);

        private static Delegate copyDelegate;

        private static readonly Dictionary<Entity, Delegate> cache
            = new Dictionary<Entity, Delegate>();

        #region Public API

        public static T Transcribe(RNA rna, Entity entity, IntPtr ptr)
        {
            var converter = GetDelegate(rna, entity);
            return converter(ptr, rna);
        }

        public static Delegate GetDelegate(RNA rna, Entity entity)
        {
            if (cache.TryGetValue(entity, out Delegate converter))
            {
                return converter;
            }

            // Create IL for Entity -> T, store in cache and return
            converter = CreateDelegate(rna, entity);
            cache[entity] = converter;
            return converter;
        }

        public static Delegate GetCopyDelegate()
        {
            if (copyDelegate == null)
            {
                copyDelegate = CreateCopyDelegate();
            }

            return copyDelegate;
        }

        #endregion

        #region IL Generator

        /// <summary>
        /// Create a delegate instance that performs an as-is memcpy
        /// instead of trying to convert from a DNA type.
        /// </summary>
        /// <returns></returns>
        static Delegate CreateCopyDelegate()
        {
            var type = typeof(T);

            var method = new DynamicMethod(
                "DNAToStructureCopy<" + type.FullName + ">",
                type,
                new Type[] { typeof(IntPtr), typeof(RNA) }, // RNA unused but needs to match the signature.
                typeof(Delegate).Module
            );

            var il = method.GetILGenerator();
            var localStruct = il.DeclareLocal(typeof(T));

            // Note this will fail for unmarshallable types
            // e.g. the struct contains a NativeArray within it.
            var size = Marshal.SizeOf(typeof(T));

            // Destination, source, then size on the stack
            il.Emit(OpCodes.Ldloca_S, localStruct);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, size);

            il.Emit(OpCodes.Cpblk);

            il.Emit(OpCodes.Ldloc, localStruct);
            il.Emit(OpCodes.Ret);

            return (Delegate)method.CreateDelegate(typeof(Delegate));
        }

        static Delegate CreateDelegate(RNA rna, Entity entity)
        {
            var type = typeof(T);

            var method = new DynamicMethod(
                "DNAToStructureDyn<" + type.FullName + ">",
                type,
                new Type[] { typeof(IntPtr), typeof(RNA) },
                typeof(Entity).Module
            );

            var il = method.GetILGenerator();
            var localStruct = il.DeclareLocal(type);

            var state = new ILState
            {
                Type = type,
                Entity = entity,
                Generator = il,
                Local = localStruct,
                RNA = rna,
            };

            // Generate IL for all fields that have a [DNA] attribute
            // that map that field to one or more DNA entities.
            foreach (var field in type.GetFields())
            {
                var dna = field.GetCustomAttribute<DNAAttribute>();
                if (dna != null)
                {
                    state.Field = entity.Fields[dna.Name];
                    state.DNAInfo = dna;
                    state.FieldInfo = field;
                    EmitCopyFieldIL(state);
                }
            }

            // Push the local struct copy onto the stack and return
            il.Emit(OpCodes.Ldloc, localStruct);
            il.Emit(OpCodes.Ret);

            return (Delegate)method.CreateDelegate(typeof(Delegate));
        }

        private static void EmitCopyFieldIL(ILState state)
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
                throw new RNAGeneratorException(
                    $"DNA attribute on field [{field.Name}] has unsupported type [{field.FieldType}]"
                );
            }
        }

        private static void EmitCopyPrimitiveFieldIL(ILState state)
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
            Console.WriteLine($"Emit {state.Entity.CType}.{state.DNAInfo.Name}@{state.Field.Offset} -> {field.Name} [{ldind[field.FieldType]}]:");

            // local.x = *(float*)(ptr + offset);
            il.Emit(OpCodes.Ldloca_S, state.Local); // Load address of local variable
            il.Emit(OpCodes.Ldarg_0); // Load pointer onto stack
            il.Emit(OpCodes.Ldc_I4, state.Field.Offset); // Push read field offset onto stack
            il.Emit(OpCodes.Add); // Add offset to pointer

            il.EmitWriteLine($"Copy {state.Entity.CType}.{state.DNAInfo.Name}@{state.Field.Offset} -> {field.Name} [{ldind[field.FieldType]}]:");

            //il.Emit(OpCodes.Ldc_I4, state.Field.Offset);
            //il.Emit(OpCodes.Call, typeof(Wtf).GetMethod("Dump"));

            il.Emit(ldind[field.FieldType]); // indirect load a type matching our field
            il.Emit(OpCodes.Stfld, field); // Store onto field
        }

        private static void EmitCopyStructIL(ILState state)
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

        private static void EmitCopyDNAStructIL(ILState state, FieldInfo field)
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

            il.Emit(OpCodes.Ldarg_1); // Load RNA instance onto stack

            il.Emit(OpCodes.Ldarg_0); // Load source pointer onto stack
            il.Emit(OpCodes.Ldc_I4, state.Field.Offset); // Push read field offset onto stack
            il.Emit(OpCodes.Add); // Add offset to pointer

            // This could be faster... a lot of work in RNA.Transcribe<T> prior to doing the il.
            il.Emit(OpCodes.Call, typeof(RNA).GetMethod("Transcribe").MakeGenericMethod(field.FieldType));

            // il.Emit(OpCodes.Stloc, fieldTypeLocal);

            // skip local variable - store directly from the call.
            il.Emit(OpCodes.Stfld, field);

            // &local.x = localOfXType
            //il.Emit(OpCodes.Ldloca_S, state.Local); // Push reference to local field onto the stack
            //il.Emit(OpCodes.Ldloc, fieldTypeLocal); // Push the field type local value onto the stack
            //il.Emit(OpCodes.Stfld, field); // Store field type local value into the field
        }

        #endregion
    }
}
