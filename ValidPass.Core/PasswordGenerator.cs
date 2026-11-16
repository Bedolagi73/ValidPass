using ValidPass.Core.Models;

namespace ValidPass.Core;

/// <summary>Генерация паролей на криптографическом ГСЧ.</summary>
public sealed class PasswordGenerator
{
    private readonly IRandomProvider _random;

    public PasswordGenerator(IRandomProvider? random = null)
    {
        _random = random ?? CryptoRandomProvider.Instance;
    }

    /// <summary>Проверяет параметры. Пустой список — можно генерировать.</summary>
    public static IReadOnlyList<string> ValidateOptions(GenerationOptions options)
    {
        var errors = new List<string>();
        var sets = GetSets(options);

        if (sets.Count == 0)
            errors.Add("Выберите хотя бы один набор символов.");
        if (options.Length is < GenerationOptions.MinLength or > GenerationOptions.MaxLength)
            errors.Add($"Длина должна быть от {GenerationOptions.MinLength} до {GenerationOptions.MaxLength} (сейчас {options.Length}).");
        if (options.Count is < 1 or > GenerationOptions.MaxCount)
            errors.Add($"Количество должно быть от 1 до {GenerationOptions.MaxCount} (сейчас {options.Count}).");

        foreach (var set in sets)
        {
            if (set.Chars.Length == 0)
                errors.Add($"Набор «{set.Name}» пуст — добавьте символы или выключите его.");
            if (set.Min < 0)
                errors.Add($"Минимум для набора «{set.Name}» не может быть отрицательным.");
        }

        int minSum = sets.Sum(s => Math.Max(s.Min, 0));
        if (minSum > options.Length)
            errors.Add($"Сумма минимумов по наборам ({minSum}) больше длины пароля ({options.Length}).");

        return errors;
    }

    /// <summary>Все допустимые символы с учётом исключений, без повторов.</summary>
    public static string BuildAlphabet(GenerationOptions options) =>
        new(GetSets(options).SelectMany(s => s.Chars).Distinct().ToArray());

    /// <summary>Энтропия пароля из этого генератора: длина × log₂(размер алфавита).</summary>
    public static double EntropyOf(GenerationOptions options) =>
        EntropyCalculator.FromAlphabet(options.Length, BuildAlphabet(options).Length);

    /// <summary>Один пароль. Неверные параметры — ArgumentException с текстом ошибок.</summary>
    public string Generate(GenerationOptions options)
    {
        ThrowIfInvalid(options);
        return GenerateCore(options, GetSets(options), BuildAlphabet(options));
    }

    /// <summary>options.Count паролей.</summary>
    public IReadOnlyList<string> GenerateMany(GenerationOptions options)
    {
        ThrowIfInvalid(options);
        var sets = GetSets(options);
        string alphabet = BuildAlphabet(options);

        var result = new string[options.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = GenerateCore(options, sets, alphabet);
        return result;
    }

    private string GenerateCore(GenerationOptions options, IReadOnlyList<CharSet> sets, string alphabet)
    {
        var buffer = new char[options.Length];
        int index = 0;

        // 1. Обязательный минимум из каждого набора
        foreach (var set in sets)
        {
            for (int k = 0; k < set.Min; k++)
                buffer[index++] = set.Chars[_random.Next(set.Chars.Length)];
        }

        // 2. Остальное — равновероятно из всего алфавита
        while (index < buffer.Length)
            buffer[index++] = alphabet[_random.Next(alphabet.Length)];

        // 3. Перемешивание Фишера–Йетса, чтобы обязательные символы не стояли в начале
        for (int i = buffer.Length - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }

        var password = new string(buffer);
        Array.Clear(buffer); // не оставляем копию пароля в памяти дольше нужного
        return password;
    }

    private static void ThrowIfInvalid(GenerationOptions options)
    {
        var errors = ValidateOptions(options);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(options));
    }

    private sealed record CharSet(string Name, string Chars, int Min);

    private static List<CharSet> GetSets(GenerationOptions o)
    {
        var sets = new List<CharSet>(4);
        if (o.UseLower) sets.Add(new("строчные", Filter(CharacterSets.Lower, o), o.MinLower));
        if (o.UseUpper) sets.Add(new("заглавные", Filter(CharacterSets.Upper, o), o.MinUpper));
        if (o.UseDigits) sets.Add(new("цифры", Filter(CharacterSets.Digits, o), o.MinDigits));
        if (o.UseSpecial) sets.Add(new("спецсимволы", Filter(o.SpecialChars ?? "", o), o.MinSpecial));
        return sets;
    }

    /// <summary>Убирает пробельные, повторы и (если включено) неоднозначные символы.</summary>
    private static string Filter(string chars, GenerationOptions o) =>
        new(chars.Where(c => !char.IsWhiteSpace(c) && !char.IsControl(c)
                             && !(o.ExcludeAmbiguous && CharacterSets.Ambiguous.Contains(c)))
                 .Distinct()
                 .ToArray());
}
