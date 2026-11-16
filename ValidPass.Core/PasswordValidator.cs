using ValidPass.Core.Models;
using ValidPass.Core.Rules;

namespace ValidPass.Core;

/// <summary>Координатор правил: запускает все правила, считает энтропию и выносит вердикт.</summary>
public sealed class PasswordValidator
{
    public PasswordValidator(IEnumerable<IPasswordRule> rules)
    {
        Rules = rules.ToList();
    }

    public IReadOnlyList<IPasswordRule> Rules { get; }

    /// <summary>Стандартный набор из шести правил.</summary>
    public static PasswordValidator CreateDefault(PasswordList dictionary, LeakedPasswordRule? leakedRule = null) =>
        new(new IPasswordRule[]
        {
            new LengthRule(),
            new CharsetRule(),
            new DictionaryRule(dictionary),
            new SubstitutionRule(dictionary),
            leakedRule ?? new LeakedPasswordRule(),
            new CommonPatternRule(),
        });

    public ValidationResult Validate(string? password, PasswordPolicy policy)
    {
        password ??= "";
        var results = new List<RuleResult>(Rules.Count);
        var strengthCap = Strength.VeryStrong;
        int downgrade = 0;

        foreach (var rule in Rules)
        {
            var result = rule.Check(password, policy);
            results.Add(result);
            if (result.Passed) continue;

            if (rule.Impact == StrengthImpact.ForceWeak) strengthCap = Strength.Weak;
            else if (rule.Impact == StrengthImpact.OneLevel) downgrade++;
        }

        double bits = EntropyCalculator.Estimate(password);
        var byEntropy = EntropyCalculator.Classify(bits, policy);

        // Понижения за шаблоны, затем «потолок» за словарь и утечки
        var strength = (Strength)Math.Max((int)byEntropy - downgrade, (int)Strength.Weak);
        if (strength > strengthCap) strength = strengthCap;

        bool isValid = password.Length > 0 && results.TrueForAll(r => r.Passed);
        return new ValidationResult(results, bits, strength, isValid);
    }
}
