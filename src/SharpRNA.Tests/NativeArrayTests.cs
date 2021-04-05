using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpRNA;
using SharpRNA.Tests.Properties;

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

    [DNA("CustomData")]
    struct CD_LData
    {
        // TODO: This *should* be supported, as we don't necessarily
        // know the size within CustomData. We know the size from Mesh
        // and we can do a floats.Reinterpret<float>(size); to work with it.
        [DNA("data")]
        public NativeArray<float> floats;
    }

    [DNA("Mesh")]
    struct Mesh_Ldata
    {
        [DNA("ldata")]
        public CD_LData ldata;
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
        private RNA rna;

        [ClassCleanup]
        public static void ClassCleanup()
        {
            Mocks.Dispose();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            using var stream = new MemoryStream(Resources.MockDNA);
            using var reader = new StreamReader(stream);
            rna = RNA.FromDNA(reader);
        }

        [TestMethod]
        public void Mesh_NativeArray_FixedSize()
        {
            var ptr = Mocks.GetNativeMeshPtr();

            var result = rna.Transcribe<Mesh_WithID>(ptr);

            Assert.AreEqual(66, result.id.name.Count);

            Assert.AreEqual((byte)'A', result.id.name[0]);
            Assert.AreEqual((byte)'B', result.id.name[1]);
            Assert.AreEqual((byte)'C', result.id.name[2]);
            Assert.AreEqual(0, result.id.name[3]);
        }

        [TestMethod]
        public void CustomData_NativeArray_UnknownSize()
        {
            // Ensure that NativeArrayConverter.EmitPointerAsNativeArrayIL
            // can handle array element types that are not DNA entities

            var ptr = Mocks.GetNativeMeshPtr();

            var result = rna.Transcribe<Mesh_Ldata>(ptr);

            // ldata.floats is an array without a defined size.
            // Any access without reinterpreting will result in an exception
            Assert.ThrowsException<IndexOutOfRangeException>(() =>
            {
                var x = result.ldata.floats[0];
            });
        }

        [TestMethod]
        public void CustomData_NativeArray_Reinterpret()
        {
            var ptr = Mocks.GetNativeMeshPtr();

            var result = rna.Transcribe<Mesh_Ldata>(ptr);

            var floats = result.ldata.floats.Reinterpret<float>(3);

            Assert.AreEqual(3, floats.Count);
            Assert.AreEqual(0, floats[0]);
            Assert.AreEqual(1, floats[1]);
            Assert.AreEqual(2, floats[2]);

            Assert.ThrowsException<IndexOutOfRangeException>(() =>
            {
                var x = floats[3];
            });
        }

        [TestMethod]
        public void Mesh_NativeArray_DynamicSize()
        {
            var ptr = Mocks.GetNativeMeshPtr();

            var result = rna.Transcribe<Mesh_NativeArrayVertices>(ptr);

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
