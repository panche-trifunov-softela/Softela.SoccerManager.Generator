namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// The audit values every row the generator writes carries.
/// </summary>
internal static class GeneratorAudit
{
    /// <summary>
    /// The user that every row the generator writes is created and modified by. No user table holds it; the backend's
    /// importer stamps its own rows with the id that ends in 1.
    /// </summary>
    internal static readonly Guid UserId = new("00000000-0000-0000-0000-000000000002");
}
