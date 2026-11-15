namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// The entrants of the two continental cups of a season, or the number of guests that is missing for them.
/// </summary>
/// <param name="Status">Whether both cups have their entrants.</param>
/// <param name="Cup1">
/// For <see cref="ContinentalEntrantSelectionStatus.Selected"/>, the league team ids of the first cup's entrants in pot
/// order: its league clubs, then its guests. Empty otherwise.
/// </param>
/// <param name="Cup2">
/// For <see cref="ContinentalEntrantSelectionStatus.Selected"/>, the league team ids of the second cup's entrants in
/// pot order: its league clubs, then its guests. Empty otherwise.
/// </param>
/// <param name="Needed">The number of guests the two cups need together to fill their empty places.</param>
/// <param name="Found">The number of guests that were available.</param>
internal sealed record ContinentalEntrantSelection(
    ContinentalEntrantSelectionStatus Status,
    IReadOnlyList<int> Cup1,
    IReadOnlyList<int> Cup2,
    int Needed,
    int Found);
