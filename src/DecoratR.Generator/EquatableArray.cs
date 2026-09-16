using System.Collections;
using System.Collections.Immutable;

namespace DecoratR.Generator;

/// <summary>
/// An immutable array with structural equality, so it can be part of incremental pipeline values.
/// A <see langword="default"/> instance behaves like an empty array.
/// </summary>
internal readonly struct EquatableArray<T>(ImmutableArray<T> array)
    : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

    private readonly ImmutableArray<T> _array = array;

    public int Length => _array.IsDefault ? 0 : _array.Length;

    public bool IsEmpty => Length == 0;

    public T this[int index] => _array[index];

    public ImmutableArray<T> AsImmutableArray() => _array.IsDefault ? ImmutableArray<T>.Empty : _array;

    public bool Contains(T item)
    {
        if (_array.IsDefault) return false;

        foreach (var element in _array)
            if (element.Equals(item))
                return true;

        return false;
    }

    public bool Equals(EquatableArray<T> other)
    {
        var length = Length;
        if (length != other.Length) return false;

        for (var i = 0; i < length; i++)
            if (!_array[i].Equals(other._array[i]))
                return false;

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_array.IsDefault) return 0;

        unchecked
        {
            var hash = 17;
            foreach (var item in _array)
                hash = hash * 31 + item.GetHashCode();

            return hash;
        }
    }

    public ImmutableArray<T>.Enumerator GetEnumerator() => AsImmutableArray().GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();

    public static implicit operator EquatableArray<T>(ImmutableArray<T> array) => new(array);

    public static EquatableArray<T> From(IEnumerable<T> items) => new(items.ToImmutableArray());
}
