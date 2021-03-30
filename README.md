# SharpRNA

TODO: Write this.

The tl;dr:

1. Take one or more versions of C structs loaded in from a set of header files
2. Convert those to a "DNA" YAML format
3. Load that YAML file into a C# project
4. Tag structs in C# with what DNA entities / fields they should reference
5. Use RNA converters to build dynamic IL to quickly convert source DNA data from a pointer to fill in C# structs in managed memory

Ez pz.

Made for an overly complicated Blender plugin due to Python API limitations.
