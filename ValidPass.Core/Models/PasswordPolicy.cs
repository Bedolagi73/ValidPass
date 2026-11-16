namespace ValidPass.Core.Models;

/// <summary>Требования к паролю при проверке.</summary>
public sealed class PasswordPolicy
{
    /// <summary>Минимальная длина (граница включена: 8 при минимуме 8 — проходит).</summary>
    public int MinLength { get; set; } = 8;

    /// <summary>Максимальная длина; 0 — без ограничения.</summary>
    public int MaxLength { get; set; } = 128;

    public bool RequireLower { get; set; } = true;
    public bool RequireUpper { get; set; } = true;
    public bool RequireDigits { get; set; } = true;
    public bool RequireSpecial { get; set; } = true;

    /// <summary>Пороги шкалы, бит. Нижняя граница включена: ровно 40 — уже «средний».</summary>
    public double MediumThreshold { get; set; } = 40;
    public double StrongThreshold { get; set; } = 60;
    public double VeryStrongThreshold { get; set; } = 80;
}
