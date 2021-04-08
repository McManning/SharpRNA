# SharpRNA
[![Release Status][workflow-image]][workflow-url]
[![NuGet Release][nuget-image]][nuget-url]

Reinterpret C structures as managed C# code

This was made for a Blender plugin that needs access to data not exposed to the Python API in an efficient way.

Other potential applications are anything that needs to interact with a constantly changing codebase in a consistent manner. By only updating a DNA definition file you can adapt to changes without needing to define new C# types or rebuild your plugin / mod / hack / etc.


# Usage

Given some data structures defined in one or more C header files:

```cpp
typedef struct bMeshData {
  CustomDataLayer *layers;
  int typemap[50];
  char _pad[4];
  int totlayer, maxlayer;
  int totsize;
  struct BLI_mempool *pool;
  CustomDataExternal *external;
} bMeshData;
```

The command line tool will generate a "DNA" YAML definition that represents the data structures you want to extract from your C headers:


```yaml
---
Version: 9.20.0     # User defined version number for the C code
Entities:
  bMeshData:        # Extracted C data structure
    Type: Struct    # Entity type (Struct, Primitive, Array, Pointer)
    CType: bMeshData
    Size: 240       # Total size (in bytes) of the structure
    Fields:
      mverts:
        Type: Pointer
        CType: MVert
        Size: 8     # Size of the field (in bytes)
      totverts:
        Type: Primitive
        CType: int
        Size: 4
        Offset: 8   # Offset (in bytes) from the struct's pointer
      typemap:
        Type: Array
        CType: int
        Size: 4
        Offset: 12
        Count: 50
      ...
```

In your C# plugin / mod you create structs that bind to these DNA entities using attributes:

```cs
[DNA("bMeshData")]
public struct Mesh
{
    // Simple primitive conversion
    [DNA("maxlayer")]
    public int maxLayer;

    // More complex types using a custom RNA Converter
    [DNA("mvert", SizeField = "totverts")]
    public NativeArray<Vertex> vertices;

    // And other data not copied from DNA
    public int otherUserData;
}
```

You can then instantiate an RNA instance from a DNA definition and read a native pointer containing your C data structure as your managed C# type:

```cs
RNA myRNA = RNA.FromDNA(yaml);
Mesh mesh = myRNA.Transcribe<Mesh>(nativePtr);

for (int i = 0; i < mesh.layers.Count; i++) {
    CustomDataLayer layer = mesh.layers[i];
    ...
}
```

>Note that this is a **read-only** process at this time and it cannot write back into the unmanaged memory that it started in.

For more usage examples, check out the test cases.


# Command Line Tool

This project can be built as a command line tool to generate DNA YAML files from C headers or to merge multiple DNA files into a single versioned DNA YAML.

```text
# SharpRNA --help

SharpRNA:
  Build DNA YAML from C headers

Usage:
  SharpRNA [options]

Options:
  -f, --headers <headers>              C header files to scan
  -d, --headers-path <headers-path>    Directory containing C headers to
                                       scan
  -i, --include <include>              DNA struct names to include in the
                                       transcript YAML
  -e, --exclude <exclude>              DNA struct names to exclude in the
                                       transcript YAML. This only applies if
                                       --include is not specified.
  -r, --include-by-ref                 Should structs referenced by-ref in
                                       included structs also be included in
                                       the transcript YAML. This only
                                       applies if --include is used
                                       (Defaults to true)
  -dv, --dna-version <dna-version>     Output DNA version number in the form
                                       MAJOR.MINOR.REVISION (Defaults to
                                       1.0.0)
  -m, --merge <merge>                  Input YAML files to merge into a
                                       single multi-versioned output. This
                                       is mutually exclusive with
                                       --header-files.
  -o, --output <output>                Output YAML file. If not specified,
                                       results go to stdout.
  -vv, --verbose                       Show verbose output.
  --version                            Display version information
```


## Creating DNA

The creation process will generate a DNA YAML file from one or more headers. Behind the scenes this uses [CppAst.NET](https://github.com/xoofx/CppAst.NET) to parse header files - so it will attempt to follow `#include` statements and resolve complex types as best it can.

```sh
SharpRNA
    --headers /path/to/foo.h /path/to/bar.h
    --include FooStruct BarStruct FizzStruct BuzzStruct
    --dna-version 1.2.3
    --output dna.yml
```

>Note that many C++ features (classes, templates, etc) are not supported. Complex preprocessors may cause the parser to fail.


## Merging DNA

Multiple YAML files can be merged into one. This allows you to publish one DNA file alongside an application and let it pick the right set of entities based on whichever version of the C application you are reading from.

```sh
SharpRNA
    --merge /path/to/dna-v920.yml /path/to/dna-v930.yml
    --output merged.yml
```

When loading from a versioned DNA file you need to specify a version number that you want to instantiate RNA for:

```cs
RNA RNAv920 = RNA.FromDNA(yaml, "9.20.0");
```


<!-- Links: -->
[workflow-image]: https://github.com/McManning/SharpRNA/actions/workflows/release.yml/badge.svg
[workflow-url]: https://github.com/McManning/SharpRNA/actions/workflows/release.yml

[nuget-image]: https://img.shields.io/nuget/v/SharpRNA.svg
[nuget-url]: https://www.nuget.org/packages/SharpRNA/
