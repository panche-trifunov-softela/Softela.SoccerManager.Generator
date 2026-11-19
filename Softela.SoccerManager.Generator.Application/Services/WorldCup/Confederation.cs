namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// The confederation of a national team, as the national teams table stores it. It mirrors the backend's
/// <c>Confederation</c> enum and the <c>CK_NationalTeams_ConfederationKnown</c> check of backend script V1_0_0_66, and
/// lists every value the check allows. The order of the values is the order the World Cup qualifiers are drawn in.
/// </summary>
internal enum Confederation : byte
{
    /// <summary>
    /// The Asian confederation. The value is the backend's <c>Confederation.Afc</c>.
    /// </summary>
    Afc = 1,

    /// <summary>
    /// The African confederation. The value is the backend's <c>Confederation.Caf</c>.
    /// </summary>
    Caf = 2,

    /// <summary>
    /// The confederation of North and Central America and the Caribbean. The value is the backend's
    /// <c>Confederation.Concacaf</c>.
    /// </summary>
    Concacaf = 3,

    /// <summary>
    /// The South American confederation. The value is the backend's <c>Confederation.Conmebol</c>.
    /// </summary>
    Conmebol = 4,

    /// <summary>
    /// The Oceanian confederation. The value is the backend's <c>Confederation.Ofc</c>.
    /// </summary>
    Ofc = 5,

    /// <summary>
    /// The European confederation. The value is the backend's <c>Confederation.Uefa</c>.
    /// </summary>
    Uefa = 6,
}
