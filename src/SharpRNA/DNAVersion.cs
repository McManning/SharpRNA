using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SharpRNA
{
    /// <summary>
    /// DNA entity version data factory
    /// </summary>
    public static class DNAVersion
    {
        /// <summary>
        /// Structs loaded from the DNA version
        /// </summary>
        private static Dictionary<string, Entity> Structs { get; set; }

        private static Dictionary<Type, Entity> typeCache = new Dictionary<Type, Entity>();

        #region Public API

        public static T FromDNA<T>(IntPtr ptr)
        {
            // Find the [DNA] match referenced by T
            var entity = FindEntityForType<T>();
            if (entity == null)
            {
                throw new Exception($"Missing [DNA] attribute for type {typeof(T)}");
            }

            // Run custom IL to generate T and return it.
            return DNAToStructure<T>.Convert(entity, ptr);
        }

        public static Entity FindEntityForType(Type type)
        {
            if (!typeCache.ContainsKey(type))
            {
                // Evaluate the type mapping for T and cache
                var attr = type.GetCustomAttributes(typeof(DNAAttribute), false) as DNAAttribute[];
                if (attr.Length < 1)
                {
                    return null; // Not a mapped type
                }

                typeCache[type] = Structs[attr[0].Name];
            }

            return typeCache[type];
        }

        public static Entity FindEntityForType<T>()
        {
            return FindEntityForType(typeof(T));
        }

        public static Entity FindEntityByID(int id)
        {
            foreach (var entity in Structs.Values)
            {
                if (entity.ID == id)
                {
                    return entity;
                }
            }

            throw new Exception($"No entity exists with ID [{id}]");
        }

        public static Entity FindEntityByName(string name)
        {
            return Structs[name];
        }

        #endregion

        #region Load New Version Bindings

        class YAMLDefinitions
        {
            public string Version { get; set; }
            public Dictionary<string, Entity> Entities { get; set; }
        }

        public static void LoadVersion(string version)
        {
            // TODO: Read from a global YAML file.
        }

        public static void LoadEntitiesFromYAML(string yaml)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(LowerCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var reader = new StreamReader(yaml);
            var definitions = deserializer.Deserialize<YAMLDefinitions>(reader);
            reader.Close();

            // Assuming only structs are stored @ top level.
            Structs = definitions.Entities;

            // Attach additional metadata while loading in entity definitions
            int id = 1;
            foreach (var entity in Structs.Values)
            {
                entity.ID = id++;
            }
        }

        #endregion
    }
}
