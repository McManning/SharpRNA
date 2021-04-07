using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using SharpRNA;

namespace SharpRNA.Tests
{
    public static class Mocks
    {
        /// <summary>
        /// 32 byte block for arbitrary data
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe struct Block32 // 32
        {
            public fixed byte data[32];

            /*
            public Block32(byte[] _ = null)
            {
                data = new byte[32];
            }

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] data; // 0, 32
            */
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe struct ID // 98
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 66)]
            public byte[] name; // 0, 66

            public Block32 padding; // 66, 32
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct CustomData // 44
        {
            public int type; // 0, 4
            public IntPtr data; // 4, 8
            public Block32 padding; // 12, 32
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct MVert // 22
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public float[] co; // 0, 12

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public short[] no; // 12, 6

            public int flag; // 18, 4
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Mesh // 274
        {
            public ID id; // 0, 98

            public IntPtr mverts; // 98, 8

            public int totverts; // 106, 4

            public int flag1; // 110, 4

            public Block32 padding1; // 114, 32

            public int flag2; // 146, 4

            public CustomData rdata; // 150, 44
            public CustomData ldata; // 194, 44

            public Block32 padding2; // 238, 32

            public float flag3; // 270, 4
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct TestPrimitives // 56
        {
            public float floatVal; // 0, 4
            public int intVal; // 4, 4
            public byte byteVal1; // 8, 1

            public byte byteVal2; // 9, 1

            public Block32 padding1; // 10, 32


            public short shortVal; // 42, 2

            public float x; // 44, 4
            public float y; // 48, 4
            public float z; // 52, 4
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct TestNestedPrimitives // 36
        {
            public float x; // 0, 4
            public float y; // 4, 4
            public float z; // 8, 4

            public TestPrimitives primitives; // 12, 24
        }

        static IntPtr meshPtr;
        static IntPtr mvertsPtr;
        static IntPtr ldataDataPtr;
        static IntPtr primitivesPtr;
        static IntPtr nestedPrimitivesPtr;

        public static IntPtr GetNativeTestPrimitivesPtr()
        {
            if (primitivesPtr != IntPtr.Zero)
            {
                return primitivesPtr;
            }

            var test = new TestPrimitives
            {
                floatVal = 0.14f,
                intVal = 14,
                byteVal1 = 15,
                byteVal2 = 16,
                shortVal = 17,
                x = 1f,
                y = 0,
                z = -1f
            };

            primitivesPtr = Marshal.AllocHGlobal(Marshal.SizeOf<TestPrimitives>());
            Marshal.StructureToPtr(test, primitivesPtr, false);

            Console.WriteLine("[Mocks] TestPrimitives size: " + Marshal.SizeOf<TestPrimitives>());
            return primitivesPtr;
        }

        public static IntPtr GetNativeTestNestedPrimitivesPtr()
        {
            if (nestedPrimitivesPtr != IntPtr.Zero)
            {
                return nestedPrimitivesPtr;
            }

            var test = new TestNestedPrimitives
            {
                x = 14,
                y = 15,
                z = 16,
                primitives = new TestPrimitives
                {
                    floatVal = 0.13f,
                    intVal = 14,
                    byteVal1 = 15,
                    byteVal2 = 16,
                    shortVal = 17,
                    x = 1f,
                    y = 0,
                    z = -1f
                }
            };

            nestedPrimitivesPtr = Marshal.AllocHGlobal(Marshal.SizeOf<TestNestedPrimitives>());
            Marshal.StructureToPtr(test, nestedPrimitivesPtr, false);

            Console.WriteLine("[Mocks] TestNestedPrimitives size: " + Marshal.SizeOf<TestNestedPrimitives>());
            return nestedPrimitivesPtr;
        }

        public static IntPtr GetNativeMeshPtr()
        {
            if (meshPtr != IntPtr.Zero)
            {
                return meshPtr;
            }

            // Allocate and fill with mock data
            var name = new byte[66];
            name[0] = (byte)'A';
            name[1] = (byte)'B';
            name[2] = (byte)'C';
            name[3] = 0;

            var id = new ID { name = name };

            var mverts = new MVert[5]
            {
                new MVert { co = new[] { 0f, 5f, 10f }, no = new short[] { 1, 0, -1 } },
                new MVert { co = new[] { 1f, 6f, 11f }, no = new short[] { 1, 0, -1 } },
                new MVert { co = new[] { 2f, 7f, 12f }, no = new short[] { 1, 0, -1 } },
                new MVert { co = new[] { 3f, 8f, 13f }, no = new short[] { 1, 0, -1 } },
                new MVert { co = new[] { 4f, 9f, 14f }, no = new short[] { 1, 0, -1 } },
            };

            mvertsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<MVert>() * mverts.Length);
            for (int i = 0; i < mverts.Length; i++)
            {
                var ptr = IntPtr.Add(mvertsPtr, Marshal.SizeOf<MVert>() * i);
                Marshal.StructureToPtr(mverts[i], ptr, false);
            }

            // Just allocate some float3's for ldata.
            ldataDataPtr = Marshal.AllocHGlobal(3 * Marshal.SizeOf<float>() * mverts.Length);

            // Dump incrementing values into the array for test data
            unsafe
            {
                var floatSize = Marshal.SizeOf<float>();
                for (int i = 0; i < mverts.Length; i++)
                {
                    var ptr = (float*)(ldataDataPtr + floatSize * i);
                    *ptr = i;
                }
            }

            var ldata = new CustomData
            {
                type = 42,
                data = ldataDataPtr
            };

            var mesh = new Mesh
            {
                id = id,
                mverts = mvertsPtr,
                totverts = mverts.Length,
                ldata = ldata,
                rdata = new CustomData { type = 12 },
                flag1 = 13,
                flag2 = 14,
                flag3 = 0.15f,
            };

            meshPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Mesh>());
            Marshal.StructureToPtr(mesh, meshPtr, true);

            Console.WriteLine("[Mocks] Mesh size: " + Marshal.SizeOf<Mesh>());
            Console.WriteLine("[Mocks] Ptr size : " + Marshal.SizeOf<IntPtr>());
            Console.WriteLine("[Mocks] CustomData size : " + Marshal.SizeOf<CustomData>());
            Console.WriteLine("[Mocks] Totverts offset: " + Marshal.OffsetOf(typeof(Mesh), "totverts"));

            Console.WriteLine("[Mocks] MVERTS PTR LONG: " + mvertsPtr);

            unsafe
            {
                var ptr = IntPtr.Add(meshPtr, 0);
                var value = *(byte*)ptr;
                Console.WriteLine("[Mocks] VALUE IS: " + value + " at " + ptr); // correct.
            }

            return meshPtr;
        }

        public static void Dispose()
        {
            Marshal.FreeHGlobal(ldataDataPtr);
            Marshal.FreeHGlobal(mvertsPtr);
            Marshal.FreeHGlobal(meshPtr);
            Marshal.FreeHGlobal(primitivesPtr);
            Marshal.FreeHGlobal(nestedPrimitivesPtr);

            ldataDataPtr = IntPtr.Zero;
            mvertsPtr = IntPtr.Zero;
            meshPtr = IntPtr.Zero;
            primitivesPtr = IntPtr.Zero;
            nestedPrimitivesPtr = IntPtr.Zero;
        }
    }
}
