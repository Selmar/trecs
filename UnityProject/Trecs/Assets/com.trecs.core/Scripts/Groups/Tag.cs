using System;
using System.Runtime.CompilerServices;
using Trecs.Internal;

namespace Trecs
{
    /// <summary>
    /// Lightweight semantic label used to classify entities into <see cref="GroupIndex"/>s.
    /// Each tag type (an <see cref="ITag"/> struct) maps to a unique <see cref="TypeId"/>;
    /// <see cref="Value"/> exposes that as an int for hashing and dictionary keys.
    /// Tags are combined into <see cref="TagSet"/>s to define groups; entities with the same
    /// tag combination share a group and its contiguous component buffers.
    /// Use <see cref="Tag{T}"/> for zero-allocation access to a tag's runtime value.
    /// </summary>
    public readonly struct Tag : IEquatable<Tag>
    {
        readonly TypeId _inner;

        public int Value => _inner.Value;

        public Tag(int value)
        {
            _inner = new TypeId(value);
        }

        public Tag(TypeId id)
        {
            _inner = id;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is Tag other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(Tag other)
        {
            return _inner == other._inner;
        }

        // Stable hash across sessions.
        public override readonly int GetHashCode()
        {
            return _inner.GetHashCode();
        }

        // Debug-only path: the underlying TypeId is registered against typeof(T)
        // by TypeId<T>'s static ctor, so we can recover the name from the reverse
        // lookup. Falls back to the raw int when no managed code has registered
        // the type (e.g. an id deserialized from a snapshot before warmup).
        public override readonly string ToString()
        {
            return TypeIdReverseLookup.TryGetTypeFromId(_inner, out var type)
                ? type.Name
                : _inner.Value.ToString();
        }

        public static bool operator ==(Tag c1, Tag c2)
        {
            return c1._inner == c2._inner;
        }

        public static bool operator !=(Tag c1, Tag c2)
        {
            return c1._inner != c2._inner;
        }

        // Safe widening: a Tag is a TypeId (of an ITag struct). Code that takes a TypeId
        // receives one transparently; constructing a Tag from a TypeId requires `new()`.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator TypeId(Tag tag) => tag._inner;
    }

    /// <summary>
    /// Zero-allocation accessor for the <see cref="Tag"/> corresponding to an
    /// <see cref="ITag"/> struct type. Access the value via <c>Tag&lt;MyTag&gt;.Value</c>.
    /// Equivalent to <c>new Tag(TypeId&lt;T&gt;.Value)</c> — a tag's underlying value IS the
    /// <see cref="TypeId"/> of <typeparamref name="T"/>, so this is a thin wrapper that
    /// defers Burst-safety and the warmup contract to <see cref="TypeId{T}"/> (which also
    /// registers <c>typeof(T)</c> with <see cref="TypeIdReverseLookup"/> for debug name
    /// recovery in <see cref="Tag.ToString"/>). Routing through <see cref="TypeId{T}.Value"/>
    /// — a <c>readonly</c> Burst-constant in default mode, a <c>SharedStatic</c> in strict
    /// mode — is what makes <see cref="Value"/> safe to read from Burst-compiled code; a
    /// cached mutable static field here would trip Burst error BC1040.
    /// </summary>
    public static class Tag<T>
        where T : struct, ITag
    {
        public static Tag Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(TypeId<T>.Value);
        }

        public static void Warmup() => _ = TypeId<T>.Value;
    }
}
