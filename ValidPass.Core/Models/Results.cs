namespace ValidPass.Core.Models;

/// <summary>Шкала стойкости.</summary>
public enum Strength
{
    Weak,
    Medium,
    Strong,
    VeryStrong,
}

/// <summary>Результат одного правила.</summary>
/// <param name="RuleName">Имя правила для пользователя.</param>
/// <param name="Passed">Пройдено или нарушено.</param>
/// <param name="Message">Человеческое сообщение: что не так или что проверено.</param>
public sealed record RuleResult(string RuleName, bool Passed, string Message)
{
    public static RuleResult Pass(string rule, string message) => new(rule, true, message);
    public static RuleResult Fail(string rule, string message) => new(rule, false, message);
}

/// <summary>Итог проверки пароля.</summary>
public sealed record ValidationResult(
    IReadOnlyList<RuleResult> Rules,
    double EntropyBits,
    Strength Strength,
    bool IsValid)
{
    public IEnumerable<RuleResult> Violations => Rules.Where(r => !r.Passed);
}

/// <summary>Строка CSV-экспорта.</summary>
public sealed record CsvRecord(string Password, int Length, double EntropyBits, Strength Strength, DateTime Timestamp);

public static class StrengthText
{
    /// <summary>Подпись шкалы по-русски — в UI и в CSV.</summary>
    public static string ToDisplay(this Strength strength) => strength switch
    {
        Strength.Weak => "слабый",
        Strength.Medium => "средний",
        Strength.Strong => "сильный",
        _ => "очень сильный",
    };
}
