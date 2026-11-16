using System.Security.Cryptography;
using System.Text;
using ValidPass.Core.Models;

namespace ValidPass.Core.Rules;

/// <summary>
/// Пароль из локального списка утечек. В списке может быть сам пароль
/// или его SHA-1 (как в выгрузках Have I Been Pwned) — проверяем оба.
/// </summary>
public sealed class LeakedPasswordRule : IPasswordRule
{
    /// <summary>Список утечек; null или пустой — проверка пропускается.</summary>
    public PasswordList? Leaks { get; set; }

    public string Name => "Утечки";

    public StrengthImpact Impact => StrengthImpact.ForceWeak;

    public RuleResult Check(string password, PasswordPolicy policy)
    {
        if (Leaks is not { Count: > 0 } leaks)
            return RuleResult.Pass(Name, "Список утечек не загружен — проверка пропущена.");

        if (password.Length > 0 && (leaks.Contains(Normalization.Lower(password)) || leaks.Contains(Sha1Hex(password))))
            return RuleResult.Fail(Name, $"Пароль найден в списке утечек «{leaks.Source}».");

        return RuleResult.Pass(Name, $"Нет в списке утечек ({leaks.Count} записей).");
    }

    /// <summary>SHA-1 в нижнем регистре hex — список при загрузке тоже приводится к нижнему регистру.</summary>
    public static string Sha1Hex(string password) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
}
