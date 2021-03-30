using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SharpRNA
{
    /// <summary>
    /// Combine multiple DNA YAML files into a single versioned file.
    /// </summary>
    class DNAMerger
    {
        public static DNAVersions Merge(List<FileInfo> files)
        {
            var versions = new DNAVersions();

            foreach (var file in files)
            {
                using var stream = file.OpenRead();
                using var reader = new StreamReader(stream);

                var dna = Serializer.FromYAML(reader);
                versions.Versions.Add(dna);
            }

            // sort versions
            // iterate through, diff 'em, etc.
            // For the sake of laziness - it all gets dumped as-is in one blob.

            return versions;
        }
    }
}
