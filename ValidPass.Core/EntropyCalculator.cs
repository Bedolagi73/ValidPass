using System.Globalization;
using ValidPass.Core.Models;

namespace ValidPass.Core;

/// <summary>Энтропия = длина × log₂(размер алфавита) и шкала стойкости.</summary>
public static class EntropyCalculator
{
    /// <summary>Сколько добавляют символы вне ASCII (кириллица, эмодзи): грубая оценка.</summary>
    public const int OtherCharsPool = 100;

    public static double FromAlphabet(int length, int alphabetSize) =>
        length <= 0 || alphabetSize <= 1 ? 0 : length * Math.Log2(alphabetSize);

    /// <summary>Оценка для введённого пароля: алфавит — сумма наборов, из которых в нём есть символы.</summary>
    public static double Estimate(string password) =>
        FromAlphabet(Length(password), EstimateAlphabet(password));

    /// <summary>Строчные 26 + заглавные 26 + цифры 10 + спецсимволы 32 + прочие 100 — по факту наличия.</summary>
    public static int EstimateAlphabet(string password)
    {
        bool lower = false, upper = false, digit = false, special = false, other = false;
        foreach (char c in password)
        {
            if (CharacterSets.IsLower(c)) lower = true;
            else if (CharacterSets.IsUpper(c)) upper = true;
            else if (CharacterSets.IsDigit(c)) digit = true;
            else if (CharacterSets.IsSpecial(c)) special = true;
            else other = true;
        }

        return (lower ? 26 : 0) + (upper ? 26 : 0) + (digit ? 10 : 0)
               + (special ? CharacterSets.AsciiSpecialCount : 0) + (other ? OtherCharsPool : 0);
    }

    /// <summary>Длина в видимых символах: эмодзи из двух char считается одним.</summary>
    public static int Length(string password) =>
        string.IsNullOrEmpty(password) ? 0 : new StringInfo(password).LengthInTextElements;

    /// <summary>Шкала по порогам политики; нижняя граница включена.</summary>
    public static Strength Classify(double bits, PasswordPolicy policy) =>
        bits >= policy.VeryStrongThreshold ? Strength.VeryStrong
        : bits >= policy.StrongThreshold ? Strength.Strong
        : bits >= policy.MediumThreshold ? Strength.Medium
        : Strength.Weak;
}
