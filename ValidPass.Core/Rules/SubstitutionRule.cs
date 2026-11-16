using ValidPass.Core.Models;

namespace ValidPass.Core.Rules;

/// <summary>
/// Подмены-«обманки»: после обратной leetspeak-замены (@→a, 0→o, 3→e, 1→i/l, $→s, 7→t)
/// пароль оказывается словарным. p@ssw0rd → password.
/// </summary>
public sealed class SubstitutionRule : IPasswordRule
{
    public SubstitutionRule(PasswordList dictionary)
    {
        Dictionary = dictionary;
    }

    public PasswordList Dictionary { get; set; }

    public string Name => "Подмены символов (leetspeak)";

    public StrengthImpact Impact => StrengthImpact.ForceWeak;

    public RuleResult Check(string password, PasswordPolicy policy)
    {
        if (Dictionary.Count == 0)
            return RuleResult.Pass(Name, "Словарь пуст — проверка пропущена.");

        string lower = Normalization.Lower(password);

        // Целиком (s3cur3 → secure) и ядро без краёв (P@ssword1 → p@ssword → password)
        var sources = new List<string>(2) { lower };
        if (Normalization.Core(lower) is { } core) sources.Add(core);

        foreach (string source in sources)
        {
            foreach (string variant in Normalization.LeetVariants(source))
            {
                if (Dictionary.Contains(variant))
                    return RuleResult.Fail(Name, $"После замены похожих символов это частый пароль «{variant}».");
            }
        }

        return RuleResult.Pass(Name, "Подмен словарных паролей не найдено.");
    }
}
