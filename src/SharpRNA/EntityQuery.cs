using CppAst;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DNAQueryNS
{
    public delegate T DNAToStructureDelegate<T>(IntPtr pointer);

    // Really we're just generating this as YAML data to then load back in...

    public enum DNAEntityType
    {
        Primitive = 0,
        Pointer,
        Array,
        Struct
    }

    /// <summary>
    /// Entity definition loaded from Blender version information.
    ///
    /// <para>
    ///     An entity is a data type within Blender DNA and its relation
    ///     to other data. E.g. the `Mesh` struct from Blender is an entity
    ///     as well as the `id` and `totverts` fields within that struct.
    /// </para>
    /// </summary>
    public class DNAEntity
    {
        public string Name { get; set; }

        public DNAEntityType Type { get; set; }

        // If array, CType = element type (primitive, struct, etc)
        public string CType { get; set; }

        public int SizeOf { get; set; }

        public int Offset { get; set; }

        public int Count { get; set; }

        /// <summary>
        /// Child fields if <see cref="Type"/> is <see cref="DNAEntityType.Struct"/>
        /// </summary>
        public Dictionary<string, DNAEntity> Fields { get; set; }
    }

    public class DNAQueryException : Exception
    {
        public DNAQuery Context { get; private set; }

        public DNAQueryException(string message, DNAQuery context) : base(message)
        {
            Context = context;
        }
    }

    public class DNAQuery
    {
        public DNAVersion Version { get; private set; }

        public DNAQuery Parent { get; private set; }

        public IntPtr Ptr { get; private set; }

        public DNAEntity Entity { get; private set; }

        public static DNAQuery From(DNAVersion version, IntPtr ptr, string name)
        {
            return new DNAQuery
            {
                Version = version,
                Parent = null,
                Ptr = ptr,
                Entity = version.FindEntity(name),
            };
        }

        /// <summary>
        /// Query into a field of a struct or array
        /// </summary>
        public DNAQuery Q(string name)
        {
            // Version.From(ptr, "Mesh").Q("totvert");
            // Version.From(ptr, "Mesh").Q("mvert");
            // Version.From(ptr, "Mesh").Q("id").Q("name");
            if (Entity.Type == DNAEntityType.Struct)
            {
                if (Entity.Fields.TryGetValue(name, out DNAEntity field)) {
                    return new DNAQuery
                    {
                        Version = Version,
                        Parent = this,
                        Ptr = IntPtr.Add(Ptr, field.Offset),
                        Entity = field
                    };
                }

                throw new DNAQueryException($"Unknown field [{name}]", this);
            }

            // Version.From(ptr, "Mesh").Q("id").Q("prev").Q("name");
            // Alternative syntax for:  .Q("id").Q("prev").At(0).Q("name");
            if (Entity.Type == DNAEntityType.Pointer)
            {
                return At(0).Q(name);
            }

            throw new DNAQueryException($"Cannot query field from non-struct", this);
        }

        /// <summary>
        /// Reinterpret this query as a new data type
        /// </summary>
        public DNAQuery Reinterpret(string newType)
        {
            var newEntity = Version.FindEntity(newType);
            return new DNAQuery
            {
                Version = Version,
                Parent = Parent,
                Ptr = Ptr,
                Entity = newEntity
            };
        }

        /// <summary>
        /// Do two queries point to the same memory and representation
        /// </summary>
        public bool Equals(DNAQuery other)
        {
            return Entity == other.Entity
                && Ptr == other.Ptr;
        }

        public DNAQuery At(int index)
        {
            // Version.From(ptr, "Mesh").Q("mvert").At(0) -> MVert
            // Version.From(ptr, "Mesh").Q("id").Q("prev").At(0) -> ID
            if (Entity.Type == DNAEntityType.Pointer)
            {
                IntPtr jump = IntPtr.Zero; // TODO: Read address @ this.Ptr (treat as void*)

                var elementEntity = Version.FindEntity(Entity.CType);
                return new DNAQuery
                {
                    Version = Version,
                    Parent = this,
                    Ptr = IntPtr.Add(jump, elementEntity.SizeOf * index),
                    Entity = elementEntity
                };
            }

            // Version.From(ptr, "Mesh").Q("id").Q("name").At(5) -> char
            // Byval offset within an array field (e.g. `char name[66];`)
            if (Entity.Type == DNAEntityType.Array)
            {
                if (index >= 0 && index < Entity.Count)
                {
                    var elementEntity = Version.FindEntity(Entity.CType);

                    return new DNAQuery
                    {
                        Version = Version,
                        Parent = this,
                        Ptr = IntPtr.Add(Ptr, elementEntity.SizeOf * index),
                        Entity = elementEntity,
                    };
                }

                throw new DNAQueryException($"Index [{index}] out of range", this);
            }

            throw new DNAQueryException($"Invalid type for .At({index})", this);
        }

        /// <summary>
        /// Reinterpret as primitive or struct.
        /// </summary>
        public T As<T>() where T : struct
        {
            if (Entity.Type != DNAEntityType.Primitive && Entity.Type != DNAEntityType.Struct)
            {
                throw new DNAQueryException($"Cannot reinterpret non-primitive / struct", this);
                // String interpretation may be fine if it's a char array (and other variants like that)
            }

            // TODO: Size compare structs - Or at least byte size of target struct <= SizeOf.
            // Otherwise we may access memory that we shouldn't.

            // FastStructure<T>.SizeOf <= Entity.SizeOf

            return default;
        }

        /// <summary>
        /// Copy a fixed length array to a managed array of structs
        /// </summary>
        public T[] AsArray<T>() where T : struct
        {
            if (Entity.Type != DNAEntityType.Array)
            {
                throw new DNAQueryException($"Cannot convert non-array", this);
            }

            return default;
        }

        /// <summary>
        /// Copy a pointer as a managed array of structs
        ///
        /// Note that this is potentially unsafe - as the count * struct size could
        /// exceed the number of items that are referenced by that pointer.
        ///
        /// <code>
        ///     var mverts = mesh.Q("
        /// </code>
        /// </summary>
        public T[] AsArray<T>(int count) where T : struct
        {
            if (Entity.Type != DNAEntityType.Pointer)
            {
                throw new DNAQueryException($"Cannot convert non-pointer", this);
            }

            // fixed array length stuff is probably fine too.

            //

            return default;
        }

        /// <summary>
        /// Convenience function to convert a fixed length array of `char` to a managed string.
        ///
        /// <code>
        ///     var name = mesh.Q("ID").Q("name").AsString();
        /// </code>
        /// </summary>
        /// <returns></returns>
        public string AsString()
        {
            return "TODO";
        }

        /// <summary>
        /// Is this type a null pointer
        /// </summary>
        /// <returns></returns>
        public bool IsNull()
        {
            if (Entity.Type != DNAEntityType.Pointer)
            {
                throw new DNAQueryException($"Cannot convert non-pointer", this);
            }

            // TODO: read memory at Ptr and check if null
            var value = Ptr;

            return value == IntPtr.Zero;
        }

        // AsNativeArray<...>
    }

    public class DNAVersion
    {
        public string Version { get; set; }

        // primitives could be loaded as types..
        public Dictionary<string, DNAEntity> Structs { get; set; }

        public Dictionary<string, DNAEntity> Primitives = new Dictionary<string, DNAEntity>
        {
            { "float", new DNAEntity { CType = "float", Name = "float", Type = DNAEntityType.Primitive, SizeOf = 8 } },
            { "int", new DNAEntity { CType = "int", Name = "int", Type = DNAEntityType.Primitive, SizeOf = 8 } },
            { "uint", new DNAEntity { CType = "uint", Name = "uint", Type = DNAEntityType.Primitive, SizeOf = 8 } },
            { "byte", new DNAEntity { CType = "char", Name = "byte", Type = DNAEntityType.Primitive, SizeOf = 1 } },
            // and so on.
        };

        public DNAQuery From(IntPtr ptr, string structName)
        {
            return DNAQuery.From(this, ptr, structName);
        }

        internal DNAEntity FindEntity(string name)
        {
            if (Structs.TryGetValue(name, out DNAEntity entity))
            {
                return entity;
            }

            if (Primitives.TryGetValue(name, out entity))
            {
                return entity;
            }

            throw new Exception($"Entity [{name}] not found in version {this}");
        }
    }

    /// <summary>
    /// Blender DNA binding information.
    ///
    /// This defines a mapping between a Coherence data type and a Blender DNA type
    /// to automatically fill Coherence data from direct reads of Blender DNA.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Field, AllowMultiple = false)]
    public class DNAAttribute : Attribute
    {
        public string Name { get; set; }

        public DNAAttribute(string name)
        {
            Name = name;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct InteropVector3
    {
        public float x;
        public float y;
        public float z;
    }

    // DNA annotations provide a fixed mapping,
    // which then lets us change actual offsets based on what
    // version Blender we're loading against.

    [DNA("bGPDspoint")] // Blender DNA type that maps to this type
    public struct GreasePencilStroke
    {
        public float x;

        public float y;

        public float z;

        /*
        bGPDspoint {
            float x, y, z
        }
        -> aligned the same as InteropVector3,
        so we can just specify .x and read it all into the struct.
        */
        [DNA("x")]
        public InteropVector3 co;

        [DNA("strength")]
        public float strength;

        public IntPtr ptrInfo;

        public override string ToString()
        {
            return $"co.x={co.x}, co.y={co.y}, co.z={co.z} x={x} y={y}, z={z}, strength={strength}";
        }
    }

    public class DNAFactory
    {
        public List<DNAVersion> Versions { get; set; }

        public DNAFactory()
        {
            // load versions
        }

        public DNAVersion ForVersion(string version)
        {
            // iterate versions, find best, return.
            return Versions[0];
        }
    }

    class EntityQuery
    {
        public class StructRules
        {
            /// <summary>
            /// Fixed length array fields to unpack into separate variables while parsing.
            /// If ommitted - an array will be generated with a MarshalAs attribute.
            /// </summary>
            public List<string> Unpack { get; set; }
        }

        public class Rules
        {
            public List<string> Files { get; set; }

            public Dictionary<string, StructRules> Structs { get; set; }

            public List<string> GetUnpackList(string structName)
            {
                if (Structs.TryGetValue(structName, out StructRules rules))
                {
                    if (rules != null)
                    {
                        return rules.Unpack;
                    }
                }

                return null;
            }
        }

        // A mapping that'd be pulled from data stored in YAML associated with a blender version
        static Dictionary<string, Dictionary<string, int>> VERSION_INFO = new Dictionary<string, Dictionary<string, int>>
        {
            { "bGPDspoint", new Dictionary<string, int> {
                { "x", 0 },
                { "y", 4 },
                { "z", 8 },
                { "a", 12 },
                { "strength", 16 },
            } },
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct bGPDspoint
        {
            public float x;
            public float y;
            public float z;

            public float a;

            public float strength;
        }

        static void OldMain(string[] args)
        {
            // Setup a test fixture of data at some location
            var point = new bGPDspoint
            {
                x = 1,
                y = 2,
                z = 3,
                a = 4,
                strength = 5
            };

            IntPtr nativePtr = Marshal.AllocHGlobal(Marshal.SizeOf<bGPDspoint>());
            Marshal.StructureToPtr(point, nativePtr, false);


            int REPETITIONS = 10000000;
            GreasePencilStroke gp = new GreasePencilStroke();


            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < REPETITIONS; i++)
            {
                gp = Convert(nativePtr);
            }
            watch.Stop();
            Console.WriteLine($"Naive: {watch.ElapsedMilliseconds}ms - {gp}");


            watch = Stopwatch.StartNew();
            for (int i = 0; i < REPETITIONS; i++)
            {
                gp = Convert2(nativePtr);
            }
            watch.Stop();
            Console.WriteLine($"Ptr: {watch.ElapsedMilliseconds}ms - {gp}");


            var func = DynamicFunc();
            watch = Stopwatch.StartNew();
            for (int i = 0; i < REPETITIONS; i++)
            {
                gp = func(nativePtr);
            }
            watch.Stop();
            Console.WriteLine($"Func: {watch.ElapsedMilliseconds}ms - {gp}");


            var converter = GenerateConverter<GreasePencilStroke>();
            watch = Stopwatch.StartNew();
            for (int i = 0; i < REPETITIONS; i++)
            {
                gp = converter(nativePtr);
            }
            watch.Stop();
            Console.WriteLine($"Dynamic IL: {watch.ElapsedMilliseconds}ms - {gp}");


            Marshal.FreeHGlobal(nativePtr);
        }

        public static DNAToStructureDelegate<T> GenerateConverter<T>()
        {
            var type = typeof(T);

            var method = new DynamicMethod(
                "DNAToStructure<" + type.FullName + ">",
                type,
                new Type[] {
                    typeof(IntPtr)
                },
                typeof(EntityQuery).Module
            );

            ILGenerator il = method.GetILGenerator();

            // il.Emit(OpCodes.Ldarg_0);
            // il.Emit(OpCodes.Ldobj, typeof(T));
            // il.Emit(OpCodes.Ret);
            // return (PtrToStructureDelegate<T>)method.CreateDelegate(typeof(PtrToStructureDelegate<T>));

            //var localPtr = il.DeclareLocal(typeof(long));
            // Not necessary - IntPtr is already the right pointer on the stack. Calling ToPointer() breaks it.

            //il.Emit(OpCodes.Initobj, type); Crashes / nullifies / whatever

            // Works
            //il.Emit(OpCodes.Ldloc_0);
            //il.Emit(OpCodes.Ldflda, type.GetField("strength")); // Load reference to field
            //il.Emit(OpCodes.Conv_R_Un); // Convert to pointer
            //il.Emit(OpCodes.Ldc_R4, 5.0f); // Value to put into the field
            //il.Emit(OpCodes.Stind_R4); // Store value into the field

            // byte* localPtr = (byte*)source.ToPointer();
           // il.Emit(OpCodes.Ldarg_0);
           // il.Emit(OpCodes.Call, typeof(IntPtr).GetMethod("ToPointer"));
           // il.Emit(OpCodes.Stloc, localPtr);


            // could wrap and pass down as byte* to the IL instead of calling ToPointer inside..
            // lol.

            /*
            il.Emit(OpCodes.Ldloca_S, localStruct); // Load address of local variable
            il.Emit(OpCodes.Ldloc, localPtr); // Load pointer onto stack
            //il.Emit(OpCodes.Ldc_I8, (long)16); // Push 16 onto stack
            //il.Emit(OpCodes.Add); // Add 16 to pointer
            il.Emit(OpCodes.Ldind_R4); // Indirect load float32 onto the stack
            il.Emit(OpCodes.Stfld, type.GetField("x"));
            */

            /*
            il.Emit(OpCodes.Ldloc, localStruct);
            il.Emit(OpCodes.Ldflda, type.GetField("strength")); // Load reference to field
            il.Emit(OpCodes.Conv_R_Un); // Convert to pointer

            //il.Emit(OpCodes.Ldloc, localPtr); // Load source pointer onto stack
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldind_R4); // Indirect load float32 onto the stack
            il.Emit(OpCodes.Stind_R4); // Store value into the field

            */

            // WORKS - therefore the ToPointer() call fails... but why.


            //il.Emit(OpCodes.Ldc_R4, 5.0f); // Value to put into the field
            //il.Emit(OpCodes.Stind_R4); // Store value into the field


            //il.Emit(OpCodes.Nop);

            //

            var localStruct = il.DeclareLocal(type);

            // Below this is essentially a EmitCopyIL(ILGenerator, FieldInfo, offset)
            // Where FieldInfo is the target field and offset is the source offset.
            // Then we essentially do a `foreach (var field in annotatedFields) { EmitCopyIL(il, field, annotation.offset) }`

            var annotations = type.GetCustomAttributes(typeof(DNAAttribute), false) as DNAAttribute[];
            if (annotations.Length < 1)
            {
                throw new Exception($"Struct {type} does not have a DNA annotation");
            }

            var dnaStructName = annotations[0].Name;

            // Generate IL for all fields that have a [DNA] attribute
            // mapping that field to the Blender DNA field
            foreach (var field in type.GetFields())
            {
                annotations = field.GetCustomAttributes(typeof(DNAAttribute), false) as DNAAttribute[];
                if (annotations.Length > 0)
                {
                    var dnaStructFieldName = annotations[0].Name;
                    var dnaStructFieldOffset = GetFieldOffset(dnaStructName, dnaStructFieldName);

                    EmitCopyFieldIL(il, localStruct, field, dnaStructFieldOffset);
                }
            }

            // Push the local struct copy onto the stack and return
            il.Emit(OpCodes.Ldloc, localStruct);
            il.Emit(OpCodes.Ret);

            return (DNAToStructureDelegate<T>)method.CreateDelegate(typeof(DNAToStructureDelegate<T>));
        }


        protected static int GetFieldOffset(string dnaStructName, string dnaStructFieldName)
        {
            if (!VERSION_INFO.ContainsKey(dnaStructName))
            {
                throw new Exception($"No DNA struct named {dnaStructName}");
            }

            if (!VERSION_INFO[dnaStructName].ContainsKey(dnaStructFieldName))
            {
                throw new Exception($"No field named {dnaStructFieldName} in DNA struct {dnaStructName}");
            }

            return VERSION_INFO[dnaStructName][dnaStructFieldName];
        }

        protected static void EmitCopyFieldIL(ILGenerator il, LocalBuilder localStruct, FieldInfo fieldInfo, int dnaStructOffset)
        {
            // Mapping of C# types to load indirect opcodes.
            var ldind = new Dictionary<Type, OpCode>()
            {
                { typeof(sbyte),    OpCodes.Ldind_I1 },
                { typeof(byte),     OpCodes.Ldind_U1 },
                { typeof(char),     OpCodes.Ldind_U2 },
                { typeof(short),    OpCodes.Ldind_I2 },
                { typeof(ushort),   OpCodes.Ldind_U2 },
                { typeof(int),      OpCodes.Ldind_I4 },
                { typeof(uint),     OpCodes.Ldind_U4 },
                { typeof(long),     OpCodes.Ldind_I8 },
                { typeof(ulong),    OpCodes.Ldind_I8 },
                { typeof(bool),     OpCodes.Ldind_I1 },
                { typeof(double),   OpCodes.Ldind_R8 },
                { typeof(float),    OpCodes.Ldind_R4 },
            };

            // If fieldInfo is a primitive that fits on the stack:
            if (fieldInfo.FieldType.IsPrimitive)
            {
                // local.x = *(float*)(ptr + offset);
                il.Emit(OpCodes.Ldloca_S, localStruct); // Load address of local variable
                il.Emit(OpCodes.Ldarg_0); // Load pointer onto stack
                il.Emit(OpCodes.Ldc_I4_S, dnaStructOffset); // Push read field offset onto stack
                il.Emit(OpCodes.Add); // Add offset to pointer

                //il.Emit(OpCodes.Ldind_R4); // Indirect load float32 onto the stack
                il.Emit(ldind[fieldInfo.FieldType]); // indirect load a type matching our field
                il.Emit(OpCodes.Stfld, fieldInfo); // Store onto field - GetField() called once for IL generating and not again
            }
            else // Struct (hopefully) - we block copy the value instead
            {
                // memcpy(&local.x, ptr + offset, sizeof(float))
                il.Emit(OpCodes.Ldloca_S, localStruct);
                il.Emit(OpCodes.Ldflda, fieldInfo); // Load reference to field onto stack

                il.Emit(OpCodes.Ldarg_0); // Load source pointer onto stack
                il.Emit(OpCodes.Ldc_I4_S, dnaStructOffset); // Push read field offset onto stack
                il.Emit(OpCodes.Add); // Add offset to pointer

                int size = Marshal.SizeOf(fieldInfo.FieldType);
                il.Emit(OpCodes.Ldc_I4_S, size); // Push destination struct size onto stack
                il.Emit(OpCodes.Cpblk); // Block copy from source pointer -> struct field
            }
        }

        public static GreasePencilStroke Convert(IntPtr source)
        {
            GreasePencilStroke result = new GreasePencilStroke();

            unsafe
            {

                // NetCore variant - o/w it's unsafenativemethods.
                Buffer.MemoryCopy(IntPtr.Add(source, 0).ToPointer(), &result.x, sizeof(float), sizeof(float));
                Buffer.MemoryCopy(IntPtr.Add(source, 4).ToPointer(), &result.y, sizeof(float), sizeof(float));
                Buffer.MemoryCopy(IntPtr.Add(source, 8).ToPointer(), &result.z, sizeof(float), sizeof(float));
                // skip a.
                Buffer.MemoryCopy(IntPtr.Add(source, 16).ToPointer(), &result.strength, sizeof(float), sizeof(float));
            }

            return result;
        }

        public static GreasePencilStroke Convert2(IntPtr source)
        {
            GreasePencilStroke result = new GreasePencilStroke();

            unsafe
            {
                byte* ptr = (byte*)source.ToPointer();

                // These are just the offsets to load... so why can't I just use this?
                // And load offsets from the external version data?
                // I guess generate-once IL is faster.
                result.x = *(float*)(ptr + 0);
                result.y = *(float*)(ptr + 4);
                result.z = *(float*)(ptr + 8);
                result.strength = *(float*)(ptr + 16);
            }

            return result;
        }

        public static Func<IntPtr, GreasePencilStroke> DynamicFunc()
        {
            // calculate offsets here (heavy compute)
            int offsetX = 0;
            int offsetY = 4;
            int offsetZ = 8;
            int offsetStrength = 16;

            // Return a dynamic func that can be invoked multiple times
            return source => {
                GreasePencilStroke result = new GreasePencilStroke();
                unsafe
                {
                    byte* ptr = (byte*)source.ToPointer();
                    result.x = *(float*)(ptr + offsetX);
                    result.y = *(float*)(ptr + offsetY);
                    result.z = *(float*)(ptr + offsetZ);
                    result.strength = *(float*)(ptr + offsetStrength);
                }
                return result;
            };

            // This would still need to be written per-type.
        }


        #if trash
        static void Main(string[] args)
        {
            /*var parserRules = new ParsingRules
            {
                Files = new List<string>
                {
                    "D:\\Blender\\sobotka\\source\\blender\\makesdna\\DNA_mesh_types.h",
                    "D:\\Blender\\sobotka\\source\\blender\\makesdna\\DNA_meshdata_types.h",
                },
                Structs = new Dictionary<string, StructRules>
                {
                    { "Mesh", null },
                    { "MVert", new StructRules {
                        Unpack = new List<string> { "co", "no" }
                    } },
                },
            };
            */

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var reader = new StreamReader("D:\\Blender\\DNAParser\\config.yml");
            var rules = deserializer.Deserialize<Rules>(reader);
            reader.Close();

            var options = new CppParserOptions();
            var compilation = CppParser.ParseFiles(rules.Files);

            //foreach (var message in compilation.Diagnostics.Messages)
             //   Console.WriteLine(message);

            // Print All enums
            foreach (var cppEnum in compilation.Enums)
                Console.WriteLine(cppEnum);

            // Print all structs with all fields
            foreach(var cstruct in compilation.Classes)
            {
                // Skip non struct
                if (cstruct.ClassKind != CppClassKind.Struct) continue;

                if (rules.Structs.ContainsKey(cstruct.Name))
                {
                    CheckRecursiveByValReferences(cstruct);
                    Console.WriteLine(GenerateStruct(cstruct, rules));
                }
            }
        }

        const string INDENT = "    ";

        static string GenerateStruct(CppClass cstruct, Rules rules)
        {
            var result =
                "[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]\n" +
                $"public struct {cstruct.Name}\n" +
                "{\n";

            // Print all fields
            foreach(var field in cstruct.Fields)
            {
                result += FieldToCS(field, rules);
            }

            if (cstruct.Fields.Count < 1)
            {
                throw new Exception(
                    $"Got stub for {cstruct.Name}. " +
                    $"it must be in one of the source files listed"
                );
            }

            result += "}\n";
            return result;
        }

        static string FieldToCS(CppField field, Rules rules)
        {
            var type = field.Type;
            var name = field.Name;

            if (type is CppPointerType ptr)
            {
                return $"{INDENT}public IntPtr {name}; // {ptr.ElementType.GetDisplayName()}*\n";
            }

            if (type is CppArrayType arr)
            {
                var unpack = rules.GetUnpackList((field.Parent as CppClass).Name);

                // If there's a rule to flatten this field - do it
                if (unpack != null && unpack.Contains(field.Name))
                {
                    var fields = "";
                    for (var i = 0; i < arr.Size; i++)
                    {
                        fields += $"{INDENT}public {arr.ElementType} {name}_{i};\n";
                    }
                    return fields;
                }

                // Otherwise use the marshaller for a constant size array
                return  $"{INDENT}[MarshalAs(UnmanagedType.ByValArray, SizeConst = {arr.Size})]\n" +
                        $"{INDENT}public {arr.ElementType}[] {name};\n";
            }

            if (type is CppPrimitiveType primitive)
            {
                return $"{INDENT}public {ConvertCppPrimitive(primitive)} {name};\n";
            }

            if (type is CppClass cls)
            {
                // If it's not a requested class, don't try to load it.
                // Just pad the caller with bytes and move on.
                // TODO: If that class is referencing structs elsewhere
                // that aren't loaded files - the byte count will probably be off.
                if (!rules.Structs.ContainsKey(cls.Name))
                {
                    return  $"{INDENT}[MarshalAs(UnmanagedType.ByValArray, SizeConst = {cls.SizeOf})]\n" +
                            $"{INDENT}public byte[] {name}; // {cls.Name} \n";
                }

                return $"{INDENT}public {cls.Name} {name};\n";
            }

            throw new Exception($"Unhandled type {type} for field {field}");
            // return $"/* {type} - sizeof={type.SizeOf}, kind={type.TypeKind}, canonical={type.GetCanonicalType()} */";
        }

        static void CheckRecursiveByValReferences(CppClass cstruct)
        {
            if (cstruct.Fields.Count < 1)
            {
                throw new Exception(
                    $"Found stub for [{cstruct.Name}] - may have been excluded from loaded files. " +
                    $"This prevents us from calculating accurate struct sizes while converting."
                );
            }

            foreach (var field in cstruct.Fields)
            {
                if (field.Type is CppClass cls)
                {
                    CheckRecursiveByValReferences(cls);
                }
            }
        }

        static string ConvertCppPrimitive(CppPrimitiveType primitive)
        {
            switch (primitive.Kind)
            {
                case CppPrimitiveKind.Bool: return "bool";
                case CppPrimitiveKind.Char: return "char";
                case CppPrimitiveKind.Double: return "double";
                case CppPrimitiveKind.Float: return "float";
                case CppPrimitiveKind.Int: return "int";
                case CppPrimitiveKind.Short: return "short";
                case CppPrimitiveKind.UnsignedChar: return "byte";
                case CppPrimitiveKind.UnsignedInt: return "uint";
                case CppPrimitiveKind.UnsignedShort: return "ushort";
                default:
                    throw new Exception($"Unsupported type [{primitive.Kind}] on [{primitive}]");
            }
        }
        #endif
    }
}
