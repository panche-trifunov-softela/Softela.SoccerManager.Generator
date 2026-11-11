namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// One row of a squad read: a player and that player's current rating. The same player can appear on several
/// rows, one for each position.
/// </summary>
/// <param name="PlayerId">The id of the player.</param>
/// <param name="Rating">The player's current rating, or null when there is none.</param>
public sealed record SquadPlayerRating(int PlayerId, int? Rating);
