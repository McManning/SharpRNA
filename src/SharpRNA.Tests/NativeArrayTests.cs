using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpRNA;

#pragma warning disable CS0649

namespace SharpRNATests
{
    [DNA("MVert")]
    struct Vertex
    {
        [DNA("co")]
        public FloatVector3 position;

        [DNA("no")]
        public ShortVector3 normal;
    }

    [DNA("Mesh")]
    struct Mesh
    {
        [DNA("mvert", SizeField = "totverts")]
        public NativeArray<Vertex> vertices;

        [DNA("totverts")]
        public int totverts;
    }

    [DNA("Mesh")]
    struct Mesh_NativeArrayVertices
    {
        // Size is dynamically determined via another field in the DNA struct
        [DNA("mverts", SizeField = "totverts")]
        public NativeArray<Vertex> vertices;

        // Checking against the pointer stored in NativeArray
        [DNA("mverts")]
        public IntPtr verticesPtr;
    }


    [DNA("ID")]
    struct MeshID // ID bits relevant to mesh data
    {
        [DNA("name")] // No size field - it'll use the YML for fixed sizes.
        public NativeArray<byte> name;
    }

    [DNA("Mesh")]
    struct Mesh_WithID
    {
        [DNA("id")]
        public MeshID id;
    }

    [TestClass]
    public class NativeArrayTests
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
        public void Mesh_NativeArray_FixedSize()
        {
            var ptr = Mocks.GetNativeMeshPtr();

            var result = DNAVersion.FromDNA<Mesh_WithID>(ptr);

            Assert.AreEqual(66, result.id.name.Count);

            Assert.AreEqual((byte)'A', result.id.name[0]);
            Assert.AreEqual((byte)'B', result.id.name[1]);
            Assert.AreEqual((byte)'C', result.id.name[2]);
            Assert.AreEqual(0, result.id.name[3]);
        }

        [TestMethod]
        public void Mesh_NativeArray_DynamicSize()
        {
            var ptr = Mocks.GetNativeMeshPtr();

            var result = DNAVersion.FromDNA<Mesh_NativeArrayVertices>(ptr);

            Assert.AreEqual(5, result.vertices.Count);

            // Check that an IntPtr read matches what NativeArray got
            Assert.AreEqual(result.verticesPtr, result.vertices.Ptr);

            // Read vertex data
            for (int i = 0; i < result.vertices.Count; i++)
            {
                Assert.AreEqual(i, result.vertices[i].position.x);
                Assert.AreEqual(i + 5, result.vertices[i].position.y);
                Assert.AreEqual(i + 10, result.vertices[i].position.z);
            }
        }
    }
}
