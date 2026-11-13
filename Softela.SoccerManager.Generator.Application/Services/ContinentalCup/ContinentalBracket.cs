using Softela.SoccerManager.Generator.Application.Services.Knockout;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// The whole knockout bracket of a continental cup, together with the league-phase table it follows from. Only
/// <see cref="ContinentalBracketBuilder"/> creates one, so a table is never paired with a bracket it does not belong
/// to.
/// </summary>
public sealed record ContinentalBracket
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContinentalBracket"/> class.
    /// </summary>
    /// <param name="table">
    /// The league team ids in place order; checked and copied into a read-only list by the builder.
    /// </param>
    /// <param name="knockout">The bracket built from the table.</param>
    internal ContinentalBracket(IReadOnlyList<int> table, KnockoutBracket knockout)
    {
        Table = table;
        Knockout = knockout;
    }

    /// <summary>
    /// The league team ids of every club of the league phase, in place order with place 1 first. Only places 1 to 8 of
    /// a 16-club cup and 1 to 24 of a 32-club cup have a tie; the rest are kept so that places can be compared.
    /// </summary>
    public IReadOnlyList<int> Table { get; }

    /// <summary>
    /// Every round and tie of the knockout. The rounds and ties are numbered as the bracket's shape says: round 1 is
    /// the quarter-final of a 16-club cup and the play-off of a 32-club cup, and the last round is the final.
    /// </summary>
    public KnockoutBracket Knockout { get; }
}
