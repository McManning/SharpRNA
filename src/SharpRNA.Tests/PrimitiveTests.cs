using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpRNA;

#pragma warning disable CS0649

namespace SharpRNATests
{
    [DNA("CustomData")]
    struct CustomData_OnlyPrimitives
    {
        [DNA("type")]
        public int type;
    }

    /// <summary>
    /// Test fixture for only loading primitives from a complex structure
    /// </summary>
    [DNA("Mesh")]
    struct Mesh_OnlyPrimitives
    {
        [DNA("id")]
        public byte id0;

        [DNA("flag1")]
        public int flag1;

        [DNA("flag2")]
        public int flag2;

        [DNA("flag3")]
        public float flag3;

        [DNA("totverts")]
        public int totverts;

        [DNA("mverts")]
        public long mverts;

        // Alternative representation
        [DNA("mverts")]
        public IntPtr verticesPtr;

        // First entry in both of these is an int we can read
        [DNA("rdata")]
        public int rdata_type;

        [DNA("ldata")]
        public int ldata_type;
    }

    /// <summary>
    /// Test fixture for loading primitives into a sub-struct
    /// </summary>
    [DNA("Mesh")]
    struct Mesh_NestedStructs
    {
        [DNA("ldata")]
        public CustomData_OnlyPrimitives ldata;
    }

    [DNA("TestPrimitives")]
    struct Primitives
    {
        // Both in a different order than the source DNA type
        // and different names.

        [DNA("intVal")]
        public int myInt;

        [DNA("floatVal")]
        public float myFloat;

        [DNA("shortVal")]
        public short myShort;
    }

    [DNA("TestPrimitives")]
    struct Primitives_WithNestedStruct
    {
        [DNA("x")]
        public FloatVector3 position;

        [DNA("byteVal1")]
        public byte flag;
    }


    [DNA("TestNestedPrimitives")]
    struct Primitives_WithNestedPrimitives
    {
        [DNA("x")]
        public float flag;

        [DNA("primitives")]
        public Primitives_WithNestedStruct nested;
    }

    [TestClass]
    public class PrimitiveTests
    {
        [ClassCleanup]
        public static void ClassCleanup()
        {
            Mocks.Dispose();
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            DNAVersion.LoadEntitiesFromYAML(Mocks.YAML_PATH);
        }

        [TestMethod]
        public void PrimitiveTypes()
        {
            // Pointer that would come from Blender/Python
            var ptr = Mocks.GetNativeTestPrimitivesPtr();

            // Convert to a C# representation only parsing out primitives
            var result = DNAVersion.FromDNA<Primitives>(ptr);

            Assert.AreEqual(0.14f, result.myFloat);
            Assert.AreEqual(14, result.myInt);
            Assert.AreEqual(17, result.myShort);
        }

        [TestMethod]
        public void ValueTypeStruct()
        {
            var ptr = Mocks.GetNativeTestPrimitivesPtr();

            // Same source type but converted to one with a nested struct
            // that should fit a sequence of values.
            var result = DNAVersion.FromDNA<Primitives_WithNestedStruct>(ptr);

            Assert.AreEqual(15, result.flag);

            // float x, y, z -> represented as Vector3 struct
            Assert.AreEqual(1f, result.position.x);
            Assert.AreEqual(0, result.position.y);
            Assert.AreEqual(-1f, result.position.z);
        }

        [TestMethod]
        public void NestedDNAStruct()
        {
            var ptr = Mocks.GetNativeTestNestedPrimitivesPtr();

            // Struct that contains an nested value type that
            // ALSO has a [DNA] attribute. This requires recursive
            // calls to generated IL - and tests how poorly I wrote
            // the callvirt opcodes.
            var result = DNAVersion.FromDNA<Primitives_WithNestedPrimitives>(ptr);

            Assert.AreEqual(14, result.flag);

            Assert.AreEqual(1f, result.nested.position.x);
            Assert.AreEqual(0, result.nested.position.y);
            Assert.AreEqual(-1f, result.nested.position.z);
        }

        [TestMethod]
        public void Mesh_OnlyPrimitives()
        {
            var ptr = Mocks.GetNativeMeshPtr();

            // Convert to a C# representation only parsing out primitives
            var result = DNAVersion.FromDNA<Mesh_OnlyPrimitives>(ptr);

            Assert.AreEqual((byte)'A', result.id0);
            Assert.AreEqual(5, result.totverts);
            Assert.AreEqual(13, result.flag1);
            Assert.AreEqual(14, result.flag2);
            Assert.AreEqual(0.15f, result.flag3);
            Assert.AreEqual(result.mverts, (long)result.verticesPtr);
        }
    }
}
