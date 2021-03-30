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
        private readonly DNAToStructure<T>.Delegate converter;

        public NativeListEnumerator(IntPtr current, DNAToStructure<T>.Delegate converter)
        {
            this.current = current;
            this.converter = converter;
        }

        public T Current => converter(current);

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
        readonly Entity entity;

        readonly IntPtr first;
        readonly IntPtr last;

        readonly DNAToStructure<T>.Delegate converter;

        public NativeList(IntPtr first, Entity entity)
        {
            this.first = first;
            this.entity = entity;
            converter = DNAToStructure<T>.GetConverter(entity);
        }

        public IEnumerator<T> GetEnumerator()
        {
            // generate a converter for version, pass down.
            return new NativeListEnumerator<T>(first, converter);
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

        public void GenerateIL(GeneratorState state)
        {
            throw new NotImplementedException();
        }
    }
}
