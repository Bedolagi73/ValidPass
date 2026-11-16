using ValidPass.Core.Models;

namespace ValidPass.Core.Rules;

/// <summary>
/// Частый пароль из словаря. Сравнение без учёта регистра: сам пароль и его ядро
/// без цифр и символов по краям (Password123 → password).
/// </summary>
public sealed class DictionaryRule : IPasswordRule
{
    public DictionaryRule(PasswordList dictionary)
    {
        Dictionary = dictionary;
    }

    /// <summary>Словарь можно заменить на лету.</summary>
    public PasswordList Dictionary { get; set; }

    public string Name => "Словарь частых паролей";

    public StrengthImpact Impact => StrengthImpact.ForceWeak;

    public RuleResult Check(string password, PasswordPolicy policy)
    {
        if (Dictionary.Count == 0)
            return RuleResult.Pass(Name, "Словарь пуст — проверка пропущена.");

        string lower = Normalization.Lower(password);
        if (Dictionary.Contains(lower))
            return RuleResult.Fail(Name, "Пароль есть в словаре частых паролей.");

        if (Normalization.Core(lower) is { } core && Dictionary.Contains(core))
            return RuleResult.Fail(Name, $"Основа пароля — частый пароль «{core}», цифры и символы по краям не спасают.");

        return RuleResult.Pass(Name, $"Нет в словаре ({Dictionary.Count} паролей).");
    }
}
