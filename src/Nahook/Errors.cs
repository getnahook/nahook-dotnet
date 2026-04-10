using System;

namespace Nahook;

/// <summary>
/// Base exception for all Nahook SDK errors.
/// </summary>
public class NahookException : Exception
{
    public NahookException(string message) : base(message) { }
    public NahookException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when the Nahook API returns an error response (4xx/5xx).
/// </summary>
public sealed class NahookApiException : NahookException
{
    public int Status { get; }
    public string Code { get; }
    public int? RetryAfter { get; }

    public bool IsRetryable => Status >= 500 || Status == 429;
    public bool IsAuthError => Status == 401 || (Status == 403 && Code == "token_disabled");
    public bool IsNotFound => Status == 404;
    public bool IsRateLimited => Status == 429;
    public bool IsValidationError => Status == 400;

    public NahookApiException(string message, int status, string code, int? retryAfter = null)
        : base(message)
    {
        Status = status;
        Code = code;
        RetryAfter = retryAfter;
    }
}

/// <summary>
/// Thrown when a network-level error occurs (DNS, connection refused, etc.).
/// </summary>
public sealed class NahookNetworkException : NahookException
{
    public NahookNetworkException(Exception innerException)
        : base($"Network error: {innerException.Message}", innerException)
    {
    }
}

/// <summary>
/// Thrown when a request exceeds the configured timeout.
/// </summary>
public sealed class NahookTimeoutException : NahookException
{
    public int TimeoutMs { get; }

    public NahookTimeoutException(int timeoutMs)
        : base($"Request timed out after {timeoutMs}ms")
    {
        TimeoutMs = timeoutMs;
    }
}
