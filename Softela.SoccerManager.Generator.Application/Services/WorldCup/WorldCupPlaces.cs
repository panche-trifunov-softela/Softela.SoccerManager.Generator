using System.Globalization;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Shares the places of the World Cup finals among the confederations in proportion to their nations, and gives each
/// confederation at least one. It uses whole numbers only, reads nothing and writes nothing.
/// </summary>
internal static class WorldCupPlaces
{
    /// <summary>
    /// Shares the places. A confederation whose quota, its share of the places by its share of the nations, is below 1
    /// gets exactly 1 place and leaves the sharing; the others share the places that are left, and the quotas of the
    /// remaining confederations are worked out again until none is below 1. Each remaining confederation then gets the
    /// whole part of its quota, and the places still left go one each to the largest fractions of the quotas, a tie
    /// going to the confederation with more nations and then to the one with the lower confederation value.
    /// </summary>
    /// <param name="nationsByConfederation">
    /// The number of nations of each confederation that takes part; a confederation with no nations is not listed.
    /// </param>
    /// <param name="places">The number of places to share.</param>
    /// <returns>The number of places of each confederation, in the order of the confederations.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="nationsByConfederation"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="places"/> is less than 1 or less than the number of confederations, a confederation
    /// has less than one nation, or the nations are fewer than the places.
    /// </exception>
    internal static IReadOnlyDictionary<Confederation, int> Share(
        IReadOnlyDictionary<Confederation, int> nationsByConfederation,
        int places)
    {
        ArgumentNullException.ThrowIfNull(nationsByConfederation);

        CheckInput(nationsByConfederation, places);

        SortedDictionary<Confederation, int> given = new();
        Dictionary<Confederation, int> pool = new(nationsByConfederation);
        int placesLeft = places;

        while (true)
        {
            long nations = pool.Values.Sum(count => (long)count);

            List<Confederation> leaving = pool
                .Where(pair => (long)placesLeft * pair.Value < nations)
                .Select(pair => pair.Key)
                .ToList();

            if (leaving.Count == 0)
            {
                break;
            }

            foreach (Confederation confederation in leaving)
            {
                given[confederation] = 1;
                pool.Remove(confederation);
            }

            placesLeft -= leaving.Count;
        }

        Divide(pool, placesLeft, given);

        return given;
    }

    /// <summary>
    /// Checks what the sharing needs: a number of places that every confederation can have one of, no empty
    /// confederation, and at least as many nations as places.
    /// </summary>
    /// <param name="nationsByConfederation">The number of nations of each confederation.</param>
    /// <param name="places">The number of places to share.</param>
    /// <exception cref="ArgumentException">Thrown when the input cannot be shared.</exception>
    private static void CheckInput(IReadOnlyDictionary<Confederation, int> nationsByConfederation, int places)
    {
        if (places < 1)
        {
            throw new ArgumentException("At least one place is needed.", nameof(places));
        }

        foreach ((Confederation confederation, int count) in nationsByConfederation.OrderBy(pair => pair.Key))
        {
            if (count < 1)
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{ConfederationLabel.Of(confederation)} has {count} nations, but a confederation that takes ")
                    + "part has at least one.",
                    nameof(nationsByConfederation));
            }
        }

        if (places < nationsByConfederation.Count)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{places} places cannot give each of {nationsByConfederation.Count} confederations a place."),
                nameof(places));
        }

        long nations = nationsByConfederation.Values.Sum(count => (long)count);

        if (nations < places)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The confederations have {nations} nations in all, which is fewer than the {places} places."),
                nameof(nationsByConfederation));
        }
    }

    /// <summary>
    /// Divides the places that are left among the confederations that remain: the whole part of each quota, then one
    /// more place each for the largest fractions.
    /// </summary>
    /// <param name="pool">The number of nations of each remaining confederation, every quota being 1 or more.</param>
    /// <param name="placesLeft">The number of places that are left.</param>
    /// <param name="given">The places given so far; the remaining confederations' places are added.</param>
    private static void Divide(
        Dictionary<Confederation, int> pool,
        int placesLeft,
        SortedDictionary<Confederation, int> given)
    {
        long nations = pool.Values.Sum(count => (long)count);

        List<Quota> quotas = pool
            .Select(pair => new Quota(
                pair.Key,
                pair.Value,
                (int)(placesLeft * (long)pair.Value / nations),
                placesLeft * (long)pair.Value % nations))
            .ToList();

        int extra = placesLeft - quotas.Sum(quota => quota.Whole);

        IEnumerable<Confederation> gainers = quotas
            .OrderByDescending(quota => quota.Fraction)
            .ThenByDescending(quota => quota.Nations)
            .ThenBy(quota => quota.Confederation)
            .Take(extra)
            .Select(quota => quota.Confederation);

        HashSet<Confederation> gaining = gainers.ToHashSet();

        foreach (Quota quota in quotas)
        {
            given[quota.Confederation] = quota.Whole + (gaining.Contains(quota.Confederation) ? 1 : 0);
        }
    }

    /// <summary>
    /// A remaining confederation's quota: its places by whole number, and what is left of the quota after them.
    /// </summary>
    /// <param name="Confederation">The confederation.</param>
    /// <param name="Nations">The number of the confederation's nations.</param>
    /// <param name="Whole">The whole part of the quota.</param>
    /// <param name="Fraction">
    /// The rest of the quota, as the remainder of the division that gave <paramref name="Whole"/>; the remainders of
    /// all confederations are over the same divisor, so they compare.
    /// </param>
    private sealed record Quota(Confederation Confederation, int Nations, int Whole, long Fraction);
}
