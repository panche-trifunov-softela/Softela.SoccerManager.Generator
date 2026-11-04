using System.Security.Cryptography;
using System.Text;

namespace Softela.SoccerManager.Generator.Application.Core.Hashing;

/// <summary>
/// Gives the fingerprint of a generated plan. A commit compares it with the reviewed preview.
/// </summary>
public static class PlanHash
{
    /// <summary>
    /// Computes the SHA-256 of the plan's canonical JSON.
    /// </summary>
    /// <param name="plan">The plan to fingerprint.</param>
    /// <returns>Sixty-four lowercase hexadecimal characters.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="plan"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the plan holds a <see cref="DateTime"/> (as a value or as a dictionary key) whose kind is not UTC,
    /// or a non-finite number (NaN or Infinity).
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// Thrown when the plan holds a reference cycle or is nested deeper than 64 levels.
    /// </exception>
    public static string Compute(object plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        string canonical = CanonicalJson.Serialize(plan);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
