using System;
using System.Collections.Generic;
using System.IO;
using CppAst;

namespace SharpRNA
{
    /// <summary>
    /// Create a DNA YAML file from one or more source C headers
    /// </summary>
    class DNABuilder
    {
        public static DNA Create(
            List<string> include,
            List<string> exclude,
            bool includeByRef,
            List<string> headers,
            string dnaVersion,
            bool verbose
        ) {
            var options = new CppParserOptions();
            // options.ParseAsCpp = false;

            var compilation = CppParser.ParseFiles(headers, options);

            foreach (var message in compilation.Diagnostics.Messages)
            {
                if (message.Type == CppLogMessageType.Error || verbose)
                {
                    Console.Error.WriteLine(message);
                }
            }

            // TODO: Support enums somehow
            // foreach (var cppEnum in compilation.Enums)
            //    Console.WriteLine(cppEnum);

            var dna = new DNA
            {
                Version = dnaVersion,
                Entities = new Dictionary<string, Entity>()
            };

            // Print all structs with all fields
            foreach(var cstruct in compilation.Classes)
            {
                var name = cstruct.Name;

                // Skip non structs
                if (cstruct.ClassKind != CppClassKind.Struct)
                {
                    if (include != null && include.Contains(name))
                    {
                        throw new Exception(
                            $"Required include [{name}] is not a supported type. Found [{cstruct.ClassKind}]."
                        );
                    }
                    continue;
                }

                // If it's on the include list OR we don't have one and it's not in the exclusions, add.
                if ((include != null && include.Contains(name)) ||
                    (include == null && exclude != null && !exclude.Contains(name)) ||
                    (include == null && exclude == null)
                ) {
                    AddEntity(dna, cstruct, includeByRef);
                }
            }

            // Check - make sure all entities in the include list were found.
            if (include != null)
            {
                var missing = new List<string>();
                foreach (var name in include)
                {
                    if (!dna.Entities.ContainsKey(name))
                    {
                        missing.Add(name);
                    }
                }

                if (missing.Count > 0)
                {
                    throw new Exception("Could not load required names: " + string.Join(", ", missing));
                }
            }

            return dna;
        }

        /// <summary>
        /// Add an entity representation to the given DNA definition
        /// </summary>
        private static void AddEntity(DNA dna, CppClass cstruct, bool includeByRef)
        {
            if (dna.Entities.ContainsKey(cstruct.Name))
            {
                return; // Already added
            }

            if (cstruct.Fields.Count < 1)
            {
                throw new Exception(
                    $"Found stub for [{cstruct.Name}] - may have been excluded from loaded files. " +
                    $"This prevents us from calculating accurate struct sizes while converting."
                );
            }

            var entity = new Entity
            {
                Type = EntityType.Struct,
                CType = cstruct.Name,
                Size = cstruct.SizeOf,
                Offset = 0,
                Fields = new Dictionary<string, Entity>()
            };

            // Add a nested entity per field - each with an offset from the parent
            // based on their individual byte sizes.
            var offset = 0;
            foreach (var cfield in cstruct.Fields)
            {
                var type = cfield.Type;
                var field = new Entity();

                if (type is CppPointerType pointer)
                {
                    field.Type = EntityType.Pointer;
                    field.CType = pointer.ElementType.GetDisplayName();
                    field.Size = pointer.SizeOf;
                    field.Offset = offset;
                    offset += field.Size;
                }
                else if (type is CppArrayType arr)
                {
                    field.Type = EntityType.Array;
                    field.CType = arr.ElementType.GetDisplayName();
                    field.Size = arr.ElementType.SizeOf;
                    field.Count = arr.Size;
                    field.Offset = offset;
                    offset += field.Size * field.Count;
                }
                else if (type is CppPrimitiveType primitive)
                {
                    field.Type = EntityType.Primitive;
                    field.CType = primitive.GetDisplayName();
                    field.Size = primitive.SizeOf;
                    field.Offset = offset;
                    offset += field.Size;
                }
                else if (type is CppClass cls)
                {
                    if (cls.ClassKind != CppClassKind.Struct)
                    {
                        throw new Exception(
                            $"Cannot parse field [{cstruct.Name}.{cfield.Name}] " +
                            $"- Type [{cls.ClassKind}] is not supported"
                        );
                    }

                    field.Type = EntityType.Struct;
                    field.CType = cls.GetDisplayName();
                    field.Size = cls.SizeOf;
                    field.Offset = offset;
                    offset += field.Size;

                    // Recursively include anything added by-ref if we're configured to.
                    if (includeByRef)
                    {
                        AddEntity(dna, cls, true);
                    }
                }

                entity.Fields[cfield.Name] = field;
            }

            dna.Entities[cstruct.Name] = entity;
        }

        private static string ConvertCppPrimitive(CppPrimitiveType primitive)
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
    }
}
