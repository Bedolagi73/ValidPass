using ValidPass.Core.Models;

namespace ValidPass.Core.Rules;

/// <summary>Обязательные наборы: строчные, заглавные, цифры, спецсимволы.</summary>
public sealed class CharsetRule : IPasswordRule
{
    public string Name => "Наборы символов";

    public StrengthImpact Impact => StrengthImpact.None;

    public RuleResult Check(string password, PasswordPolicy policy)
    {
        var missing = new List<string>(4);
        if (policy.RequireLower && !password.Any(CharacterSets.IsLower)) missing.Add("строчных букв");
        if (policy.RequireUpper && !password.Any(CharacterSets.IsUpper)) missing.Add("заглавных букв");
        if (policy.RequireDigits && !password.Any(CharacterSets.IsDigit)) missing.Add("цифр");
        if (policy.RequireSpecial && !password.Any(CharacterSets.IsSpecial)) missing.Add("спецсимволов");

        if (missing.Count > 0)
            return RuleResult.Fail(Name, $"Не хватает: {string.Join(", ", missing)}.");

        bool anyRequired = policy.RequireLower || policy.RequireUpper || policy.RequireDigits || policy.RequireSpecial;
        return RuleResult.Pass(Name, anyRequired ? "Все обязательные наборы есть." : "Наборы не требуются.");
    }
}
