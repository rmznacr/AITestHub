namespace AITestHub.Services;

/// <summary>
/// Thrown when the EVREN LLM API is not usable (missing API key, non-success response
/// or an unparsable response). Contains no sensitive data such as the API key.
/// </summary>
public class EvrenConfigurationException : Exception
{
    public EvrenConfigurationException(string message) : base(message)
    {
    }

    public EvrenConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
