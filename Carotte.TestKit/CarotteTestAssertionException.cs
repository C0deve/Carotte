namespace Carotte;

/// <summary>
/// Exception thrown when a Carotte TestKit message assertion fails.
/// </summary>
public class CarotteTestAssertionException : Exception
{
    public CarotteTestAssertionException(string message) : base(message)
    {
    }

    public CarotteTestAssertionException(string message, Exception? innerException) : base(message, innerException)
    {
    }
}
