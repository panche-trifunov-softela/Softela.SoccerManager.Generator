using Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

namespace Softela.SoccerManager.Generator.Application.Services.Rollover;

/// <summary>
/// The clubs of each division in the next season, and which clubs moved to get there.
/// </summary>
/// <param name="Divisions">
/// The divisions of the next season in division order, top division first, each with its clubs in ascending league team
/// id order.
/// </param>
/// <param name="Promoted">
/// Every club that moves up, in division order and then place order; the playoff winner comes after the clubs that go
/// up without a playoff.
/// </param>
/// <param name="Relegated">Every club that moves down, in division order and then place order.</param>
internal sealed record RolloverMoves(
    IReadOnlyList<LeagueDivision> Divisions,
    IReadOnlyList<int> Promoted,
    IReadOnlyList<int> Relegated);
