using System;
using System.IO;
using YamlDotNet.Serialization;

namespace SharpRNA
{
    class Serializer
    {
        /// <summary>
        /// Create a <see cref="DNA"/> from a YAML stream
        /// </summary>
        public static DNA FromYAML(TextReader reader)
        {
            var deserializer = new DeserializerBuilder()
                //.WithNamingConvention(LowerCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var dna = deserializer.Deserialize<DNA>(reader);
            return dna;
        }

        /// <summary>
        /// Create a <see cref="DNAVersions"/> from a YAML stream
        /// </summary>
        public static DNAVersions FromVersionedYAML(TextReader reader)
        {
            var deserializer = new DeserializerBuilder()
                //.WithNamingConvention(LowerCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
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
