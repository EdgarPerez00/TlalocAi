using System.Security.Cryptography;
using System.Text;

namespace TlalocAi.SharedKernel;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new("None", string.Empty);
}

public sealed class Result<T>
{
    private Result(bool isSuccess, T? value, Error error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error Error { get; }

    public static Result<T> Success(T value) => new(true, value, Error.None);

    public static Result<T> Failure(string code, string message) => new(false, default, new Error(code, message));
}

public static class ApiKeyHasher
{
    public static string GenerateKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return $"tlaloc_{Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=')}";
    }

    public static string Hash(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes);
    }

    public static bool Verify(string apiKey, string hash) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(apiKey)),
            Encoding.UTF8.GetBytes(hash));
}

public static class Clock
{
    public static DateTime UtcNow => DateTime.UtcNow;
}
