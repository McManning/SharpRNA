using System;
using System.Collections.Generic;

namespace SharpRNA
{
    public enum EntityType
    {
        Primitive = 0,
        Pointer,
        Array,
        Struct
    }

    /// <summary>
    /// A set of entity definitions matching one or more version numbers
    /// </summary>
    public class DNA
    {
        public string Version { get; set; }

        public string Min { get; set; }

        public string Max { get; set; }

        public Dictionary<string, Entity> Entities { get; set; }
    }

    /// <summary>
    /// Multiple versions of the same DNA bundled together
    /// </summary>
    public class DNAVersions
    {
        public List<DNA> Versions { get; set; }

        /// <summary>
        /// Find the DNA instance that covers the given version
        /// </summary>
        public DNA Find(string version)
        {
            var cmp = new Version(version);

            return Versions.Find((dna) =>
            {
                var min = new Version(!string.IsNullOrEmpty(dna.Min) ? dna.Min : dna.Version);
                var max = new Version(!string.IsNullOrEmpty(dna.Max) ? dna.Max : dna.Version);
                return cmp >= min && cmp <= max;
            });
        }
    }

    /// <summary>
    /// Entity definition loaded from DNA version information.
    ///
    /// <para>
    ///     An entity is a structure within DNA and its relation
    ///     to other entities. E.g. the `Mesh` struct from Blender is an entity
    ///     as well as the `id` and `totverts` fields within that struct.
    /// </para>
    /// </summary>
    public class Entity
    {
        public DNA DNA { get; internal set; }

        public EntityType Type { get; set; }

        // If array, CType = element type (primitive, struct, etc)

        /// <summary>
        /// The underlying DNA type for this entity.
        ///
        /// <para>
        ///     If this is <see cref="EntityType.Array"/> or <see cref="EntityType.Pointer"/>
        ///     then this is the type of referenced elements.
        /// </para>
        /// </summary>
        public string CType { get; set; }

        /// <summary>
        /// Size of this entity in bytes.
        ///
        /// <para>
        ///     If this is <see cref="EntityType.Array"/> then this is the
        ///     size of the element type in the array. Total storage size
        ///     is then <see cref="Size"/> * <see cref="Offset"/>.
        /// </para>
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// If a field, this is the offset in bytes within the parent <see cref="Entity"/>.
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// Number of elements if <see cref="Type"/> is <see cref="EntityType.Struct"/>.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Child fields if <see cref="Type"/> is <see cref="EntityType.Struct"/>
        /// </summary>
        public Dictionary<string, Entity> Fields { get; set; }
    }
}
