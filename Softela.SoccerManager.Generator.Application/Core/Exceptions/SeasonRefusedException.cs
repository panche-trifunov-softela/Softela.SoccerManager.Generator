namespace Softela.SoccerManager.Generator.Application.Core.Exceptions;

/// <summary>
/// Thrown when a league's newest season cannot be written as its rows stand. The message says what is wrong and how
/// to fix it, and the caller logs it and moves on to the next league. Nothing else catches it.
/// </summary>
public sealed class SeasonRefusedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonRefusedException"/> class.
    /// </summary>
    /// <param name="message">What is wrong with the season's rows and how to fix it.</param>
    public SeasonRefusedException(string message)
        : base(message)
    {
    }
}
