namespace ValidPass.Core.Models;

/// <summary>Параметры генерации паролей.</summary>
public sealed class GenerationOptions
{
    public const int MinLength = 4;
    public const int MaxLength = 128;
    public const int MaxCount = 1000;

    /// <summary>Длина пароля, 4–128.</summary>
    public int Length { get; set; } = 16;

    /// <summary>Сколько паролей сгенерировать за раз, 1–1000.</summary>
    public int Count { get; set; } = 10;

    public bool UseLower { get; set; } = true;
    public bool UseUpper { get; set; } = true;
    public bool UseDigits { get; set; } = true;
    public bool UseSpecial { get; set; } = true;

    /// <summary>Набор спецсимволов — настраивается.</summary>
    public string SpecialChars { get; set; } = CharacterSets.DefaultSpecial;

    /// <summary>Исключить неоднозначные символы: 0 O o 1 l I |.</summary>
    public bool ExcludeAmbiguous { get; set; }

    /// <summary>Минимум символов из каждого набора (учитывается, только если набор включён).</summary>
    public int MinLower { get; set; }
    public int MinUpper { get; set; }
    public int MinDigits { get; set; }
    public int MinSpecial { get; set; }
}
