using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using YamlDotNet.Serialization;

namespace SharpRNA
{
    class Args
    {
        public List<string> Include { get; set; }

        public List<string> Exclude { get; set; }

        public bool IncludeByRef { get; set; }

        public List<FileInfo> Headers { get; set; }

        public DirectoryInfo HeadersPath { get; set; }

        public string DnaVersion { get; set; }

        public List<FileInfo> Merge { get; set; }

        public FileInfo Output { get; set; }

        public bool Verbose { get; set; }

        public List<string> GetHeaderFilenames()
        {
            var headers = new List<string>();
            if (Headers != null)
            {
                foreach (var file in Headers)
                {
                    headers.Add(file.FullName);
                    if (Verbose)
                    {
                        Console.WriteLine($"Load File: {file.FullName}");
                    }
                }
            }

            if (HeadersPath != null)
            {
                if (Verbose)
                {
                    Console.WriteLine($"Load Path: {HeadersPath.FullName}");
                }

                var files = HeadersPath.GetFiles();
                foreach (var file in files)
                {
                    headers.Add(file.FullName);
                    if (Verbose)
                    {
                        Console.WriteLine($"Load File: {file.FullName}");
                    }
                }
            }

            return headers;
        }
    }

    class Program
    {
        static int Main(string[] args)
        {
            var root = new RootCommand
            {
                new Option<List<FileInfo>>(
                    new[] { "--headers", "-f" },
                    "C header files to scan"
                ).ExistingOnly(),
                new Option<DirectoryInfo>(
                    new[] { "--headers-path", "-d" },
                    "Directory containing C headers to scan"
                ).ExistingOnly(),
                new Option<List<string>>(
                    new[] { "--include", "-i" },
                    "DNA struct names to include in the transcript YAML"
                ),
                new Option<List<string>>(
                    new[] { "--exclude", "-e" },
                    "DNA struct names to exclude in the transcript YAML. This only applies if --include is not specified."
                ),
                new Option<bool>(
                    new[] { "--include-by-ref", "-r" },
                    false,
                    "Should structs referenced by-ref in included structs also be included in " +
                    "the transcript YAML. This only applies if --include is used (Defaults to true)"
                ),
                new Option<string>(
                    new[] { "--dna-version", "-dv" },
                    "1.0.0",
                    "Output DNA version number in the form MAJOR.MINOR.REVISION (Defaults to 1.0.0)"
                ),
                new Option<List<FileInfo>>(
                    new[] { "--merge", "-m" },
                    "Input YAML files to merge into a single multi-versioned output. " +
                    "This is mutually exclusive with --header-files."
                ).ExistingOnly(),
                new Option<FileInfo>(
                    new[] { "--output", "-o" },
                    "Output YAML file. If not specified, results go to stdout."
                ),
                new Option<bool>(
                    new[] { "--verbose", "-vv" },
                    false,
                    "Show verbose output."
                ),
                new Argument("Output"),
            };

            // root.Description = "TODO";

            root.Handler = CommandHandler.Create((Args args) =>
            {
                var headers = args.GetHeaderFilenames();

                // Figure out pipeline - either generating DNA files or merging them
                if (headers.Count > 0 && args.Merge != null)
                {
                    Console.Error.WriteLine("You cannot specify both --header-files and --merge.");
                    return 1;
                }

                if (headers.Count > 0)
                {
                    var dna = DNABuilder.Create(
                        args.Include,
                        args.Exclude,
                        args.IncludeByRef,
                        headers,
                        args.DnaVersion,
                        args.Verbose
                    );

                    Serializer.ToYAML(
                        dna,
                        args.Output != null
                        ? args.Output.CreateText()
                        : Console.Out
                    );

                    return 0;
                }

                if (args.Merge != null)
                {
                    var merged = DNAMerger.Merge(args.Merge);
                    Serializer.ToVersionedYAML(
                        merged,
                        args.Output != null
                        ? args.Output.CreateText()
                        : Console.Out
                    );

                }

                Console.Error.WriteLine("You must specify either --header-files or --merge.");
                return 1;
            });

            return root.Invoke(args);
        }
    }
}
