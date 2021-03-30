using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpRNA;

namespace SharpRNATests
{
    [TestClass]
    public class VersionTests
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
        public void LoadEntitiesFromYAML()
        {
            var mesh = DNAVersion.FindEntityByName("Mesh");

            foreach (var field in mesh.Fields.Keys)
            {
                Console.WriteLine($"{field} = {mesh.Fields[field]}");
            }

            Assert.AreEqual(0, mesh.Fields["id"].Offset);
            Assert.AreEqual(270, mesh.Size);
            Assert.AreEqual("Mesh", mesh.CType);
        }
    }
}
