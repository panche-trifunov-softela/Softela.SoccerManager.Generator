namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// What a season's matches and standings say about its two continental cups.
/// </summary>
/// <param name="Status">How much of the cups is written.</param>
/// <param name="Reason">
/// For <see cref="ContinentalCupStateStatus.Partial"/>, one sentence that says why and names the cup or club at fault.
/// Empty for the other statuses.
/// </param>
internal sealed record ContinentalCupState(ContinentalCupStateStatus Status, string Reason);
