using System.Text;

namespace Softela.SoccerManager.Generator.Application.Core.Randomness;

/// <summary>
/// A deterministic pseudo-random source (SplitMix64). The same seed gives the same sequence on every platform and .NET
/// version, which <see cref="Random"/> does not promise. It is not thread-safe.
/// </summary>
public sealed class SeededRandom
{
    private const ulong StateIncrement = 0x9E3779B97F4A7C15UL;
    private const ulong FnvOffsetBasis = 0xCBF29CE484222325UL;
    private const ulong FnvPrime = 0x100000001B3UL;

    private ulong _state;

    /// <summary>
    /// Creates a source whose sequence is fixed by the seed.
    /// </summary>
    /// <param name="seed">The seed that fixes the whole sequence.</param>
    public SeededRandom(long seed)
    {
        Seed = seed;
        _state = unchecked((ulong)seed);
    }

    /// <summary>
    /// The seed this source was created with.
    /// </summary>
    public long Seed { get; }

    /// <summary>
    /// Returns the next 64 random bits and advances the sequence.
    /// </summary>
    /// <returns>An unsigned 64-bit value.</returns>
    public ulong NextUInt64()
    {
        unchecked
        {
            _state += StateIncrement;

            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;

            return z ^ (z >> 31);
        }
    }

    /// <summary>
    /// Returns a uniform integer in [0, <paramref name="maxExclusive"/>), with no modulo bias.
    /// </summary>
    /// <param name="maxExclusive">The exclusive upper bound; it must be positive.</param>
    /// <returns>An integer from 0 up to, but not including, <paramref name="maxExclusive"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxExclusive"/> is zero or negative.
    /// </exception>
    public int NextInt(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);

        ulong bound = (ulong)maxExclusive;

        // Raw values below the threshold would make the lowest residues more likely, so they are rejected.
        ulong threshold = unchecked(0UL - bound) % bound;

        while (true)
        {
            ulong value = NextUInt64();

            if (value >= threshold)
            {
                return (int)(value % bound);
            }
        }
    }

    /// <summary>
    /// Shuffles the list in place with a Fisher-Yates shuffle.
    /// </summary>
    /// <typeparam name="T">The type of the list's items.</typeparam>
    /// <param name="items">The list to shuffle.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="items"/> is read-only.</exception>
    public void Shuffle<T>(IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = NextInt(i + 1);

            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>
    /// Creates an independent source for one named purpose. It depends on <see cref="Seed"/> only, so the number of
    /// values this source has already drawn never changes it.
    /// </summary>
    /// <param name="purpose">The name of the purpose; an empty string is allowed.</param>
    /// <returns>A new source that is the same for the same seed and purpose.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="purpose"/> is null.</exception>
    public SeededRandom Derive(string purpose)
    {
        ArgumentNullException.ThrowIfNull(purpose);

        ulong hash = Fnv1a64(purpose);
        ulong derivedSeed = new SeededRandom(unchecked((long)((ulong)Seed ^ hash))).NextUInt64();

        return new SeededRandom(unchecked((long)derivedSeed));
    }

    /// <summary>
    /// Hashes the text with 64-bit FNV-1a over its UTF-8 bytes. <see cref="string.GetHashCode()"/> is randomised per
    /// process, so it cannot be used here.
    /// </summary>
    /// <param name="text">The text to hash.</param>
    /// <returns>The 64-bit hash.</returns>
    private static ulong Fnv1a64(string text)
    {
        ulong hash = FnvOffsetBasis;

        foreach (byte b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash = unchecked(hash * FnvPrime);
        }

        return hash;
    }
}
