namespace ValidPass.Core;

/// <summary>Наборы символов для генерации и оценки пароля.</summary>
public static class CharacterSets
{
    public const string Lower = "abcdefghijklmnopqrstuvwxyz";
    public const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string Digits = "0123456789";
    public const string DefaultSpecial = "!@#$%^&*()-_=+[]{};:,.<>?/";

    /// <summary>Символы, которые легко спутать при чтении.</summary>
    public const string Ambiguous = "0Oo1lI|";

    /// <summary>Сколько всего спецсимволов ASCII (печатные, кроме букв, цифр и пробела) — для оценки энтропии.</summary>
    public const int AsciiSpecialCount = 32;

    public static bool IsLower(char c) => c is >= 'a' and <= 'z';
    public static bool IsUpper(char c) => c is >= 'A' and <= 'Z';
    public static bool IsDigit(char c) => c is >= '0' and <= '9';

    /// <summary>Спецсимвол — печатный ASCII, не буква, не цифра и не пробел.</summary>
    public static bool IsSpecial(char c) => c is > ' ' and <= '~' && !char.IsAsciiLetterOrDigit(c);
}
