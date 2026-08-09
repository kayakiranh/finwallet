using FakeCutoff.Api.Contracts;
using FakeCutoff.Api.Models;

namespace FakeCutoff.Api.Services;

/// <summary>
/// TR: FakeCutoff simulatorının deterministic çalışma günü, cutoff ve settlement hesaplama motorudur; takvim seed'leri yalnızca geliştirme/test simülasyonu içindir.
/// EN: Deterministic business-day, cutoff and settlement calculation engine for the FakeCutoff simulator; calendar seeds are intended only for development/test simulation.
/// </summary>
public sealed class CutoffCalendarService
{
    private static readonly IReadOnlyDictionary<string, CutoffRule> Rules = CreateRules();

    private static readonly IReadOnlyDictionary<string, HashSet<(int Month, int Day)>> SimulatedFixedHolidays =
        new Dictionary<string, HashSet<(int Month, int Day)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["TR"] = new HashSet<(int Month, int Day)>
            {
                (1, 1), (4, 23), (5, 1), (5, 19), (7, 15), (8, 30), (10, 29)
            },
            ["AZ"] = new HashSet<(int Month, int Day)>
            {
                (1, 1), (1, 2), (3, 8), (5, 28), (6, 15), (11, 8), (12, 31)
            }
        };

    /// <summary>
    /// TR: Dış işlem context'ini ülke/currency/işlem tipi kuralıyla eşleştirir ve provider local timezone'unda processing/settlement kararını üretir.
    /// EN: Matches external operation context with a country/currency/transaction-type rule and produces processing/settlement decisions in the provider's local timezone.
    /// </summary>
    /// <param name="request">
    /// TR: Cutoff değerlendirmesi yapılacak dış provider isteği.
    /// EN: External-provider request to evaluate for cutoff behavior.
    /// </param>
    /// <returns>
    /// TR: Anlık işlenebilirlik, processing tarihi, settlement tarihi ve karar nedenini içeren provider yanıtını döndürür.
    /// EN: Returns the provider response containing immediate processability, processing date, settlement date and decision reason.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// TR: Zorunlu alanlar boşsa veya desteklenen cutoff kuralı bulunamazsa oluşur.
    /// EN: Thrown when required fields are empty or no supported cutoff rule exists.
    /// </exception>
    public CutoffEvaluationResponse Evaluate(CutoffEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CountryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TransactionType);

        var countryCode = request.CountryCode.Trim().ToUpperInvariant();
        var currency = request.Currency.Trim().ToUpperInvariant();
        var transactionType = request.TransactionType.Trim();
        var ruleKey = CreateRuleKey(countryCode, currency, transactionType);

        if (!Rules.TryGetValue(ruleKey, out var rule))
        {
            throw new ArgumentException("No cutoff rule exists for the supplied country, currency and transaction type.", nameof(request));
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(rule.TimeZoneId);
        var localRequestedAt = TimeZoneInfo.ConvertTime(request.RequestedAt, timeZone);
        var localDate = DateOnly.FromDateTime(localRequestedAt.DateTime);
        var localTime = TimeOnly.FromDateTime(localRequestedAt.DateTime);

        var isBusinessDay = IsBusinessDay(rule.CountryCode, localDate);
        var canProcessNow = isBusinessDay && localTime <= rule.CutoffTime;
        var processingDate = canProcessNow
            ? localDate
            : NextBusinessDay(rule.CountryCode, localDate);
        var settlementDate = AddBusinessDays(
            rule.CountryCode,
            processingDate,
            rule.SettlementBusinessDays);

        var reason = !isBusinessDay
            ? "NON_BUSINESS_DAY"
            : localTime > rule.CutoffTime
                ? "AFTER_CUTOFF"
                : "WITHIN_CUTOFF";

        return new CutoffEvaluationResponse(
            Guid.NewGuid(),
            canProcessNow,
            processingDate,
            settlementDate,
            rule.CutoffTime,
            rule.TimeZoneId,
            reason);
    }

    /// <summary>
    /// TR: Verilen ülke ve tarihin hafta sonu veya simulator sabit tatil seed'i nedeniyle business day dışı olup olmadığını belirler.
    /// EN: Determines whether a country/date is a business day after considering weekends and simulator fixed-holiday seeds.
    /// </summary>
    /// <param name="countryCode">TR: Takvimi kullanılacak ülke kodu. EN: Country code whose calendar is used.</param>
    /// <param name="date">TR: Değerlendirilecek yerel tarih. EN: Local date to evaluate.</param>
    /// <returns>TR: Tarih business day ise true döndürür. EN: Returns true when the date is a business day.</returns>
    private static bool IsBusinessDay(string countryCode, DateOnly date)
    {
        var dayOfWeek = date.DayOfWeek;
        if (dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        return !SimulatedFixedHolidays.TryGetValue(countryCode, out var holidays)
            || !holidays.Contains((date.Month, date.Day));
    }

    /// <summary>
    /// TR: Verilen tarihten sonraki ilk business day'i bulur; mevcut tarihi tekrar kullanmaz.
    /// EN: Finds the first business day after the supplied date and never reuses the current date.
    /// </summary>
    /// <param name="countryCode">TR: Takvimi kullanılacak ülke kodu. EN: Country code whose calendar is used.</param>
    /// <param name="date">TR: Aramanın başlayacağı yerel tarih. EN: Local date from which the search begins.</param>
    /// <returns>TR: Sonraki business day'i döndürür. EN: Returns the next business day.</returns>
    private static DateOnly NextBusinessDay(string countryCode, DateOnly date)
    {
        var candidate = date.AddDays(1);
        while (!IsBusinessDay(countryCode, candidate))
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }

    /// <summary>
    /// TR: Processing tarihine yalnızca business day'leri sayarak settlement gün sayısını ekler.
    /// EN: Adds settlement days to the processing date while counting business days only.
    /// </summary>
    /// <param name="countryCode">TR: Takvimi kullanılacak ülke kodu. EN: Country code whose calendar is used.</param>
    /// <param name="startDate">TR: Processing başlangıç tarihi. EN: Processing start date.</param>
    /// <param name="businessDays">TR: Eklenecek business-day sayısı. EN: Number of business days to add.</param>
    /// <returns>TR: Hesaplanan settlement business tarihini döndürür. EN: Returns the calculated settlement business date.</returns>
    private static DateOnly AddBusinessDays(string countryCode, DateOnly startDate, int businessDays)
    {
        var result = startDate;
        for (var index = 0; index < businessDays; index++)
        {
            result = NextBusinessDay(countryCode, result);
        }

        return result;
    }

    /// <summary>
    /// TR: Simulatorın desteklediği başlangıç cutoff kurallarını oluşturur; değerler production banka SLA'sı değil deterministic test seed'idir.
    /// EN: Creates the simulator's initial supported cutoff rules; values are deterministic test seeds rather than production bank SLAs.
    /// </summary>
    /// <returns>TR: Normalize composite key ile indekslenmiş cutoff kurallarını döndürür. EN: Returns cutoff rules indexed by normalized composite key.</returns>
    private static IReadOnlyDictionary<string, CutoffRule> CreateRules()
    {
        var rules = new[]
        {
            new CutoffRule("TR", "TRY", "Withdrawal", "Europe/Istanbul", new TimeOnly(16, 30), 1),
            new CutoffRule("TR", "USD", "Withdrawal", "Europe/Istanbul", new TimeOnly(15, 30), 2),
            new CutoffRule("TR", "EUR", "Withdrawal", "Europe/Istanbul", new TimeOnly(15, 30), 2),
            new CutoffRule("TR", "TRY", "BankTransfer", "Europe/Istanbul", new TimeOnly(16, 0), 1),
            new CutoffRule("AZ", "USD", "Withdrawal", "Asia/Baku", new TimeOnly(15, 0), 2),
            new CutoffRule("AZ", "EUR", "Withdrawal", "Asia/Baku", new TimeOnly(15, 0), 2)
        };

        return rules.ToDictionary(
            static rule => CreateRuleKey(rule.CountryCode, rule.Currency, rule.TransactionType),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// TR: Cutoff kuralı lookup'ı için ülke, currency ve işlem tipinden normalize composite key üretir.
    /// EN: Creates a normalized composite key from country, currency and transaction type for cutoff-rule lookup.
    /// </summary>
    /// <param name="countryCode">TR: Ülke kodu. EN: Country code.</param>
    /// <param name="currency">TR: Para birimi kodu. EN: Currency code.</param>
    /// <param name="transactionType">TR: İşlem tipi. EN: Transaction type.</param>
    /// <returns>TR: Normalize lookup anahtarını döndürür. EN: Returns the normalized lookup key.</returns>
    private static string CreateRuleKey(string countryCode, string currency, string transactionType)
    {
        return $"{countryCode.Trim().ToUpperInvariant()}|{currency.Trim().ToUpperInvariant()}|{transactionType.Trim().ToUpperInvariant()}";
    }
}
