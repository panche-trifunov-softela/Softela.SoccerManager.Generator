namespace Softela.SoccerManager.Generator.Application.Core.Exceptions;

/// <summary>
/// Thrown when a step cannot write a league's newest season as its rows stand. The message says what is wrong and how
/// to fix it. The step that called the code catches it and logs it, and the tick moves on to its next step.
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
