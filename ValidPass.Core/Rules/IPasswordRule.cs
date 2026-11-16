using ValidPass.Core.Models;

namespace ValidPass.Core.Rules;

/// <summary>Как нарушение правила влияет на шкалу стойкости.</summary>
public enum StrengthImpact
{
    /// <summary>Только делает пароль невалидным (длина, наборы).</summary>
    None,

    /// <summary>Понижает шкалу на одну ступень (последовательности, повторы).</summary>
    OneLevel,

    /// <summary>Пароль слабый, какой бы ни была энтропия (словарь, подмены, утечка).</summary>
    ForceWeak,
}

/// <summary>Правило проверки пароля — чистая функция без UI и файлов.</summary>
public interface IPasswordRule
{
    string Name { get; }

    StrengthImpact Impact { get; }

    RuleResult Check(string password, PasswordPolicy policy);
}
