using Softela.SoccerManager.Generator.Application.Services.Knockout;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Builds the knockout bracket of a continental cup from its finished league-phase table.
/// </summary>
public static class ContinentalBracketBuilder
{
    /// <summary>
    /// Builds the whole knockout bracket. Nothing is drawn: every tie follows from the places in the table, so the
    /// same table always gives the same bracket. The caller's list is copied, not kept.
    /// </summary>
    /// <param name="table">
    /// The league team ids of the clubs, in the order of the finished league-phase table with place 1 first. There are
    /// 16 or 32 of them.
    /// </param>
    /// <returns>The bracket, with its rounds and ties numbered as <see cref="ContinentalBracketShape"/> says.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="table"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the number of clubs is not 16 or 32, or a club is listed more than once. The first club found a
    /// second time, in list order, is the one named.
    /// </exception>
    public static ContinentalBracket Build(IReadOnlyList<int> table)
    {
        ContinentalInputValidator.ValidateClubs(table, nameof(table));

        IReadOnlyList<int> places = Array.AsReadOnly(table.ToArray());
        KnockoutBracket knockout = ContinentalBracketShape.Build(places);

        return new ContinentalBracket(places, knockout);
    }
}
