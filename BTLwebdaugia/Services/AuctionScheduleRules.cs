namespace BTLwebdaugia.Services;

public static class AuctionScheduleRules
{
    public const int MinDurationMinutes = 60;
    public const int MaxDurationMinutes = 43200;
    public const string DurationMode = "Duration";
    public const string SpecificInputMode = "Specific";
    public const string FixedEndTimeMode = "FixedEndTime";

    public static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(MinDurationMinutes);
    public static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(MaxDurationMinutes);
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static DateTime ToUtc(DateTime vietnamTime)
    {
        return new DateTimeOffset(
            DateTime.SpecifyKind(vietnamTime, DateTimeKind.Unspecified), VietnamOffset).UtcDateTime;
    }
}
