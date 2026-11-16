using ValidPass.Core.Models;

namespace ValidPass.Core.Rules;

/// <summary>Минимальная и максимальная длина. Длина — в видимых символах.</summary>
public sealed class LengthRule : IPasswordRule
{
    public string Name => "Длина";

    public StrengthImpact Impact => StrengthImpact.None;

    public RuleResult Check(string password, PasswordPolicy policy)
    {
        int length = EntropyCalculator.Length(password);

        if (length == 0)
            return RuleResult.Fail(Name, "Пароль пустой.");
        if (length < policy.MinLength)
            return RuleResult.Fail(Name, $"Слишком короткий: {length} симв., нужно не меньше {policy.MinLength}.");
        if (policy.MaxLength > 0 && length > policy.MaxLength)
            return RuleResult.Fail(Name, $"Слишком длинный: {length} симв., допускается не больше {policy.MaxLength}.");

        string range = policy.MaxLength > 0 ? $"{policy.MinLength}–{policy.MaxLength}" : $"от {policy.MinLength}";
        return RuleResult.Pass(Name, $"{length} симв. — в пределах {range}.");
    }
}
