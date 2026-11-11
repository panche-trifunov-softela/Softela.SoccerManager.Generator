namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// One side of a cup tie: either a known club, or the winner of an earlier tie. Exactly one of the two forms is set.
/// </summary>
public sealed record DomesticCupSide
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomesticCupSide"/> class.
    /// </summary>
    /// <param name="leagueTeamId">The league team id of the club, or null when the side is a winner.</param>
    /// <param name="winnerOfRoundNumber">The round of the earlier tie, or null when the side is a club.</param>
    /// <param name="winnerOfTieNumber">The number of the earlier tie, or null when the side is a club.</param>
    private DomesticCupSide(int? leagueTeamId, int? winnerOfRoundNumber, int? winnerOfTieNumber)
    {
        LeagueTeamId = leagueTeamId;
        WinnerOfRoundNumber = winnerOfRoundNumber;
        WinnerOfTieNumber = winnerOfTieNumber;
    }

    /// <summary>
    /// The league team id of the club, or null when the side is the winner of an earlier tie.
    /// </summary>
    public int? LeagueTeamId { get; }

    /// <summary>
    /// The round of the earlier tie whose winner this side is, or null when the side is a club.
    /// </summary>
    public int? WinnerOfRoundNumber { get; }

    /// <summary>
    /// The number of the earlier tie whose winner this side is, or null when the side is a club.
    /// </summary>
    public int? WinnerOfTieNumber { get; }

    /// <summary>
    /// Gets a value that says whether the side is a known club.
    /// </summary>
    public bool IsClub => LeagueTeamId.HasValue;

    /// <summary>
    /// Creates a side that is a known club.
    /// </summary>
    /// <param name="leagueTeamId">The league team id of the club.</param>
    /// <returns>A side holding the club.</returns>
    public static DomesticCupSide Club(int leagueTeamId)
    {
        return new DomesticCupSide(leagueTeamId, null, null);
    }

    /// <summary>
    /// Creates a side that is the winner of an earlier tie.
    /// </summary>
    /// <param name="roundNumber">The round of the earlier tie.</param>
    /// <param name="tieNumber">The number of the earlier tie within its round.</param>
    /// <returns>A side holding the winner of that tie.</returns>
    public static DomesticCupSide WinnerOf(int roundNumber, int tieNumber)
    {
        return new DomesticCupSide(null, roundNumber, tieNumber);
    }
}
