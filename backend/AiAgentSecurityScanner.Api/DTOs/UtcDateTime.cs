namespace AiAgentSecurityScanner.Api.DTOs;

public static class UtcDateTime
{
    public static DateTime FromStorage(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public static DateTime? FromStorage(DateTime? value)
    {
        return value is null ? null : FromStorage(value.Value);
    }
}
