namespace forzion.tech.Domain.Services;

public static class ConversaoFuso
{
    public static DateTime? ParaUtc(DateTime wallClock, TimeZoneInfo fuso)
    {
        var local = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);

        if (fuso.IsInvalidTime(local))
            return null;

        if (fuso.IsAmbiguousTime(local))
            return DateTime.SpecifyKind(local - fuso.BaseUtcOffset, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeToUtc(local, fuso);
    }
}
