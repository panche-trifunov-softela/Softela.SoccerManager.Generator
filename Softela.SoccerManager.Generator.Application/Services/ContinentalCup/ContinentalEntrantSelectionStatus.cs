namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// What choosing the entrants of the two continental cups came to.
/// </summary>
internal enum ContinentalEntrantSelectionStatus
{
    /// <summary>
    /// Both cups have their full list of entrants.
    /// </summary>
    Selected,

    /// <summary>
    /// The guests cannot fill the places the league clubs leave empty, so neither cup has entrants.
    /// </summary>
    TooFewGuests,
}
