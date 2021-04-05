using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpRNA
{
    public class NativeListEnumerator<T> : IEnumerator<T> where T : struct
    {
        private IntPtr current;

        /// <summary>
        /// RNA instance used for conversion
        /// </summary>
        private readonly RNA rna;

        /// <summary>
        /// Transcribe delegate from a DNA type to a C# representation.
        /// </summary>
        private readonly RNA<T>.Delegate rnaDelegate;

        public NativeListEnumerator(RNA rna, IntPtr current, RNA<T>.Delegate rnaDelegate)
        {
            this.rna = rna;
            this.current = current;
            this.rnaDelegate = rnaDelegate;
        }

        public T Current => rnaDelegate(current, rna);

        object IEnumerator.Current => Current;

        public void Dispose()
        {

        }

        public bool MoveNext()
        {
            if (current == IntPtr.Zero)
            {
                return false;
            }

            // First entry of the struct is a pointer to the next struct
            current = Marshal.ReadIntPtr(current);
            return current != IntPtr.Zero;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Iterator for Blender's ListBase.
    /// </summary>
    /// <typeparam name="T">A type that can be cast to from Blender DNA</typeparam>
    public class NativeList<T> : IEnumerable<T> where T : struct
    {
        readonly IntPtr first;
        readonly IntPtr last;

        readonly RNA rna;
        readonly RNA<T>.Delegate rnaDelegate;

        public NativeList(RNA rna, IntPtr first, Entity entity)
        {
            this.rna = rna;
            this.first = first;

            rnaDelegate = RNA<T>.GetDelegate(rna, entity);
        }

        public IEnumerator<T> GetEnumerator()
        {
            // generate a converter for version, pass down.
            return new NativeListEnumerator<T>(rna, first, rnaDelegate);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Equals(NativeList<T> other)
        {
            // iterate both - memcmp Entity.SizeOf * Count regions.
            // Return false on first mismatch.
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// DNA converter to support <see cref="NativeList{T}"/> fields.
    /// </summary>
    public class NativeListConverter : IConverter
    {
        public bool CanConvert(Entity from, Type to)
        {
            return to.IsGenericType && to.GetGenericTypeDefinition() == typeof(NativeList<>);
        }

        public void GenerateIL(ILState state)
        {
            throw new NotImplementedException();
        }
    }
}
