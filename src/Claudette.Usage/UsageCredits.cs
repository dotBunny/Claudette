using System.Globalization;
using Claudette.Core;
using Claudette.Core.Sessions;

namespace Claudette.Usage;

/// <summary>
/// The plan's usage credits as <c>get_usage</c> reports them, <c>rate_limits.extra_usage</c> (DESIGN.md §6, "Usage
/// credits"): what pays for Claude once a plan limit is used up, up to a cap.
/// </summary>
/// <param name="IsEnabled">Whether credits can pay for requests now: false while they're off, or the cap is reached.</param>
/// <param name="Used">This billing period's spend, in minor units of <paramref name="Currency"/> (cents for USD).</param>
/// <param name="MonthlyLimit">The cap, in the same units; null when the account has no cap of its own, which isn't no cap.</param>
/// <param name="Currency">An ISO 4217 code; none is taken for USD.</param>
/// <param name="At">When <c>get_usage</c> reported them.</param>
public sealed record UsageCredits(bool IsEnabled, double? Used, double? MonthlyLimit, string? Currency, DateTimeOffset At)
{
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.Ordinal) { "JPY", "KRW", "CLP", "ISK", "VND" };

    /// <summary>"$12.40 of $50.00 spent this month.", or without the cap; null when the spend isn't known.</summary>
    public string? SpendText => Used is not { } used
        ? null
        : MonthlyLimit is { } limit
            ? $"{Amount(used, Currency)} of {Amount(limit, Currency)} spent this month."
            : $"{Amount(used, Currency)} spent this month.";

    /// <summary>An amount in minor units of a currency, written as the header writes money: "$12.40", "€8.00", "1200 JPY".</summary>
    public static string Amount(double minor, string? currency)
    {
        var code = currency is { Length: > 0 } c ? c.Trim().ToUpperInvariant() : "USD";
        var zeroDecimal = ZeroDecimalCurrencies.Contains(code);
        var value = zeroDecimal ? minor : minor / 100;
        return code switch
        {
            "USD" => Formats.Dollars(value),
            "EUR" => string.Create(CultureInfo.InvariantCulture, $"€{value:0.00}"),
            "GBP" => string.Create(CultureInfo.InvariantCulture, $"£{value:0.00}"),
            _ when zeroDecimal => string.Create(CultureInfo.InvariantCulture, $"{value:0} {code}"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{value:0.00} {code}"),
        };
    }
}

/// <summary>
/// What Claude Code last said about usage credits: a <c>rate_limit_event</c>'s <c>isUsingOverage</c> (undocumented), with
/// the limit the event names and when that resets.
/// </summary>
/// <param name="IsUsing">Usage credits are paying for Claude's requests.</param>
/// <param name="LimitType">The event's <c>rateLimitType</c>, such as <c>five_hour</c>.</param>
/// <param name="ResetsAt">The event's <c>resetsAt</c>: when that limit resets.</param>
/// <param name="At">When the event came.</param>
public sealed record CreditsReport(bool IsUsing, string? LimitType, DateTimeOffset? ResetsAt, DateTimeOffset At);

/// <summary>
/// Usage credits paying for Claude now, for the notice under the header and its alert (DESIGN.md §6, "Usage credits").
/// </summary>
/// <param name="LimitType">The limit that's used up, as a <c>rateLimitType</c>: <c>five_hour</c>, <c>seven_day</c>; null when not known.</param>
/// <param name="Until">When that limit resets and the plan pays again; null when it isn't known.</param>
/// <param name="Credits">The spend and cap, once <c>get_usage</c> has reported them.</param>
public sealed record CreditUse(string? LimitType, DateTimeOffset? Until, UsageCredits? Credits)
{
    public const string Title = "Using usage credits";

    /// <summary>The limit by the usage header's name for it: "session limit", "weekly limit".</summary>
    public string LimitName => LimitWait.NameOf(LimitType);

    /// <summary>
    /// Whether usage credits are paying, by the newer of what Claude Code last said and what <c>get_usage</c> last
    /// reported:
    /// <list type="bullet">
    /// <item>Credits reported unable to pay since Claude Code last said they were paying: no.</item>
    /// <item>Claude Code said they're paying: yes, until the limit it named resets.</item>
    /// <item>Claude Code said they aren't, since the last poll: no.</item>
    /// <item>Otherwise, credits that are on pay once the session or the weekly limit is used up, until it resets. A
    /// model's own limit doesn't count here: only that model's requests would pay, and the poll can't say which model
    /// Claude is using.</item>
    /// </list>
    /// </summary>
    public static CreditUse? From(UsageSnapshot? snapshot, DateTimeOffset now)
    {
        if (snapshot is null)
        {
            return null;
        }
        var credits = snapshot.Credits;
        var report = snapshot.CreditsReport;
        if (credits is { IsEnabled: false } && (report is null || credits.At >= report.At))
        {
            return null;
        }
        if (report is { IsUsing: true })
        {
            // Without the event's own reset, the latest reset of any limit that's used up, a model's too.
            var until = report.ResetsAt ?? snapshot.Limits.Where(l => l.Percent >= 100).Max(l => l.ResetsAt);
            if (until is not { } end || end > now)
            {
                return new CreditUse(report.LimitType, until, credits);
            }
        }
        else if (report is { IsUsing: false } && (credits is null || report.At >= credits.At))
        {
            return null;
        }
        return credits is { IsEnabled: true } && UsedUp(snapshot, now) is { } limit
            ? new CreditUse(limit.Kind == LimitKind.Session ? "five_hour" : "seven_day", limit.ResetsAt, credits)
            : null;
    }

    /// <summary>
    /// The notice's line: "Your session limit is used up, so your usage credits pay for Claude until it resets at 15:45.
    /// $12.40 of $50.00 spent this month."
    /// </summary>
    public string Describe(TimeProvider clock)
    {
        var line = Until is { } until
            ? $"Your {LimitName} is used up, so your usage credits pay for Claude until it resets {Formats.When(until, clock)}."
            : $"Your {LimitName} is used up, so your usage credits pay for Claude.";
        return Credits?.SpendText is { } spend ? $"{line} {spend}" : line;
    }

    /// <summary>The session or weekly limit that's used up; when both are, the one that resets last, since credits pay until then.</summary>
    private static LimitReading? UsedUp(UsageSnapshot snapshot, DateTimeOffset now) =>
        snapshot.Limits
            .Where(l => l.Kind is LimitKind.Session or LimitKind.WeeklyAll && l.Percent >= 100 && (l.ResetsAt is null || l.ResetsAt > now))
            .OrderByDescending(l => l.ResetsAt ?? DateTimeOffset.MaxValue)
            .FirstOrDefault();
}
