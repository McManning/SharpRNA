using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpRNA;
using SharpRNA.Tests.Properties;

namespace SharpRNA.Tests
{
    [TestClass]
    public class VersionTests
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

        // TODO: Tests!
    }
}
