using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectFactories;

namespace SharpRNA
{
    class EntityFactory : IObjectFactory
    {
        private readonly DefaultObjectFactory fallback = new DefaultObjectFactory();
        private DNA dna;

        public object Create(Type type)
        {
            if (type == typeof(DNA))
            {
                dna = new DNA();
                return dna;
            }

            if (type == typeof(Entity))
            {
                return new Entity
                {
                    DNA = dna
                };
            }

            return fallback.Create(type);
        }
    }

    class Serializer
    {
        /// <summary>
        /// Create a <see cref="DNA"/> from a YAML stream
        /// </summary>
        public static DNA FromYAML(TextReader reader)
        {
            var deserializer = new DeserializerBuilder()
                //.WithNamingConvention(LowerCaseNamingConvention.Instance)
                // .IgnoreUnmatchedProperties()
                .WithObjectFactory(new EntityFactory())
                .Build();

            // That's a problem.

            var dna = deserializer.Deserialize<DNA>(reader);

            // Make sure entities and version information has loaded
            if (string.IsNullOrEmpty(dna.Version))
            {
                throw new Exception("Could not find DNA version information from YAML");
            }

            if (dna.Entities == null || dna.Entities.Count < 1)
            {
                throw new Exception("Could not find any DNA entities from YAML");
            }

            return dna;
        }

        /// <summary>
        /// Create a <see cref="DNAVersions"/> from a YAML stream
        /// </summary>
        public static DNAVersions FromVersionedYAML(TextReader reader)
        {
            var deserializer = new DeserializerBuilder()
                //.WithNamingConvention(LowerCaseNamingConvention.Instance)
                //.IgnoreUnmatchedProperties()
                .Build();

            var versions = deserializer.Deserialize<DNAVersions>(reader);
            return versions;
        }

        /// <summary>
        /// Serialize a DNA instance into a YAML stream
        /// </summary>
        public static void ToYAML(DNA dna, TextWriter writer)
        {
            var serializer = new SerializerBuilder()
                // .JsonCompatible()
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                .Build();

            serializer.Serialize(writer, dna);
        }

        /// <summary>
        /// Serialize DNAVersions into a YAML stream
        /// </summary>
        public static void ToVersionedYAML(DNAVersions versions, TextWriter writer)
        {
            var serializer = new SerializerBuilder()
                // .JsonCompatible()
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                .Build();

            serializer.Serialize(writer, versions);
        }
    }
}
