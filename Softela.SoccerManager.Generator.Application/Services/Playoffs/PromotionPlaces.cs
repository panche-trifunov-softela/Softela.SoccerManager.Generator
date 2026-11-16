namespace Softela.SoccerManager.Generator.Application.Services.Playoffs;

/// <summary>
/// What a division's settings say about the places of its table. The first places go up without a playoff, the next
/// places play the promotion playoff for the last promotion place, and the bottom places go down.
/// </summary>
/// <param name="DivisionId">The competition id of the division.</param>
/// <param name="ClubCount">The number of clubs in the division.</param>
/// <param name="Promoted">The number of clubs that go up from the division, the playoff's winner included.</param>
/// <param name="PlayoffClubs">The number of clubs that play the promotion playoff: 0, 2 or 4.</param>
/// <param name="Relegated">The number of clubs that go down from the division.</param>
public sealed record PromotionPlaces(int DivisionId, int ClubCount, int Promoted, int PlayoffClubs, int Relegated)
{
    /// <summary>
    /// Gets the number of clubs that go up without a playoff: the places from 1 up. With a playoff one promotion place
    /// is left for its winner.
    /// </summary>
    public int Automatic => PlayoffClubs > 0 ? Promoted - 1 : Promoted;

    /// <summary>
    /// Gets the table place of the best-placed club in the playoff, which is the number of clubs that go up. It means
    /// something only when <see cref="PlayoffClubs"/> is above 0.
    /// </summary>
    public int FirstPlayoffPlace => Promoted;
}
