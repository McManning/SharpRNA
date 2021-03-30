using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using DNAQueryNS;

namespace SharpRNATests
{
    struct MVert
    {
        public float co_x, co_y, co_z;
        public short no_x, no_y, no_z;
    }

    [TestClass]
    public class DNAQueryTests
    {



      // [TestMethod]
        public void TestUsage()
        {
            // Factory for a specific supported Blender version
            var factory = new DNAFactory().ForVersion("2.80");

            // Create an instance of a type from a direct pointer
            var mesh = factory.From(IntPtr.Zero, "Mesh");

            var totverts = mesh.Q("totverts").As<int>();

            // Extract a byval struct field
            var ldata = mesh.Q("ldata"); // ldata.CType == "CustomData"

            // Reinterpret `int totlayer;` as a primitive int
            var totlayer = ldata.Q("totlayer").As<int>();

            // Reinterpret `MVert* mvert;` as a managed array of struct
            // (or boxed NativeArray<MVert>)
            var verts = mesh.Q("mvert").AsArray<MVert>(totverts);

           // var untypedVerts = mesh.Q("mvert").AsArray(totverts);

           // var vert5 = untypedVerts.At<MVert>(5); // Is a copy to managed mem. Not great.

            // say we provided the verts pointer directly
            var vertsPtr = IntPtr.Zero;
            var vertCount = 15;

            //var vertsFromPtr = factory.CreateArray(vertsPtr, vertCount).As<MVert>();
        }

        struct GreasePencilPoint
        {
            public float x, y, z;
            public float pressure;
            public float strength;
        }

        struct InteropVector3
        {
            public float x, y, z;
        }

        public void GreasePencil()
        {
            var factory = new DNAFactory().ForVersion("2.80");
            var frame = factory.From(IntPtr.Zero, "bGPDframe");

            // no clue how many strokes? ListBase strokes has a first and last pointer.
            // Do we just

            // ListBase.first is a void* - but storing bGPDstroke entries
            var strokes = frame.Q("strokes").Q("first").Reinterpret("bGPDstroke");
            var last = frame.Q("strokes").Q("last").Reinterpret("bGPDstroke");

            var index = 0;
            DNAQuery stroke;
            do
            {
                stroke = strokes.At(index);
                // Do stuff with stroke

                var totpoints = stroke.Q("totpoints").As<int>();
                var points = stroke.Q("points");
                for (int i = 0; i < totpoints; i++)
                {
                    var point = points.At(i);

                    // x/y/z are separate but adjacent in Blender - convert to vec3
                    var co = point.Q("x").As<InteropVector3>();
                    var pressure = point.Q("pressure").As<float>();
                    var strength = point.Q("strength").As<float>();
                    // 6 DNAQuery allocations here... pretty not great.

                    // do stuff.
                }


                index++;
            } while (!stroke.Equals(last));

            // Alternatively - can just use next/prev in stroke itself...
            while (!stroke.IsNull())
            {
                // Do work with stroke

                stroke = stroke.Q("next");
            }
        }
    }
}
