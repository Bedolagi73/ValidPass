using System.Text;

namespace ValidPass.Core.Rules;

/// <summary>Приведение пароля к виду для сравнения со словарём.</summary>
public static class Normalization
{
    /// <summary>Ядро короче — слишком много случайных совпадений («abc» внутри чего угодно).</summary>
    public const int MinCoreLength = 4;

    // Обратные leetspeak-замены. Единица бывает и «i», и «l» — проверяем оба варианта
    private static readonly Dictionary<char, char> LeetToI = new()
    {
        ['@'] = 'a', ['4'] = 'a', ['3'] = 'e', ['1'] = 'i', ['!'] = 'i',
        ['0'] = 'o', ['$'] = 's', ['5'] = 's', ['7'] = 't', ['+'] = 't',
    };

    private static readonly Dictionary<char, char> LeetToL = new(LeetToI) { ['1'] = 'l', ['|'] = 'l' };

    public static string Lower(string password) => password.ToLowerInvariant();

    /// <summary>
    /// Ядро: без цифр и спецсимволов по краям. «password123» → «password», «!qwerty!» → «qwerty».
    /// null — если ядро короче <see cref="MinCoreLength"/> или совпадает с исходной строкой.
    /// </summary>
    public static string? Core(string lower)
    {
        int start = 0, end = lower.Length;
        while (start < end && !char.IsLetter(lower[start])) start++;
        while (end > start && !char.IsLetter(lower[end - 1])) end--;

        int length = end - start;
        if (length < MinCoreLength || length == lower.Length) return null;
        return lower.Substring(start, length);
    }

    /// <summary>Варианты с обратной leetspeak-заменой: p@ssw0rd → password. Без замен — пусто.</summary>
    public static IEnumerable<string> LeetVariants(string lower)
    {
        string a = Replace(lower, LeetToI);
        if (a != lower) yield return a;

        string b = Replace(lower, LeetToL);
        if (b != lower && b != a) yield return b;
    }

    private static string Replace(string text, Dictionary<char, char> map)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
            sb.Append(map.TryGetValue(c, out char r) ? r : c);
        return sb.ToString();
    }
}
