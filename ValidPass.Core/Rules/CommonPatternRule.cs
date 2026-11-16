using ValidPass.Core.Models;

namespace ValidPass.Core.Rules;

/// <summary>
/// Предсказуемые куски: последовательности (abc, 123, zyx, 321), ряды клавиатуры (qwerty, asdf)
/// и 3+ одинаковых символа подряд (aaa, !!!).
/// </summary>
public sealed class CommonPatternRule : IPasswordRule
{
    public const int MinSequence = 3;
    public const int MinRepeat = 3;
    public const int MinKeyboardRun = 4;

    private const int MaxExamples = 5;

    private static readonly string[] KeyboardRows =
    [
        "qwertyuiop", "asdfghjkl", "zxcvbnm", "1234567890",
        "йцукенгшщзхъ", "фывапролджэ", "ячсмитьбю",
    ];

    public string Name => "Последовательности и повторы";

    public StrengthImpact Impact => StrengthImpact.OneLevel;

    public RuleResult Check(string password, PasswordPolicy policy)
    {
        string lower = Normalization.Lower(password);
        var sequences = FindSequences(lower).Concat(FindKeyboardRuns(lower)).Distinct().Take(MaxExamples).ToList();
        var repeats = FindRepeats(lower).Distinct().Take(MaxExamples).ToList();

        if (sequences.Count == 0 && repeats.Count == 0)
            return RuleResult.Pass(Name, "Последовательностей и повторов нет.");

        var parts = new List<string>(2);
        if (sequences.Count > 0) parts.Add($"последовательности {Quote(sequences)}");
        if (repeats.Count > 0) parts.Add($"повторы {Quote(repeats)}");
        return RuleResult.Fail(Name, $"Найдены {string.Join("; ", parts)}.");
    }

    /// <summary>Подряд идущие буквы или цифры с шагом +1 или −1: abc, 123, zyx.</summary>
    public static IEnumerable<string> FindSequences(string lower)
    {
        int i = 0;
        while (i < lower.Length - 1)
        {
            int step = lower[i + 1] - lower[i];
            int j = i + 1;
            if (step is 1 or -1 && SameClass(lower[i], lower[i + 1]))
            {
                while (j + 1 < lower.Length && lower[j + 1] - lower[j] == step && SameClass(lower[j], lower[j + 1]))
                    j++;
                if (j - i + 1 >= MinSequence)
                {
                    yield return lower.Substring(i, j - i + 1);
                    i = j + 1;
                    continue;
                }
            }
            i++;
        }
    }

    /// <summary>3+ одинаковых символа подряд.</summary>
    public static IEnumerable<string> FindRepeats(string lower)
    {
        int i = 0;
        while (i < lower.Length)
        {
            int j = i;
            while (j + 1 < lower.Length && lower[j + 1] == lower[i]) j++;
            if (j - i + 1 >= MinRepeat)
                yield return lower.Substring(i, j - i + 1);
            i = j + 1;
        }
    }

    /// <summary>Куски из 4+ соседних клавиш одного ряда, в любую сторону: qwer, lkjh.</summary>
    public static IEnumerable<string> FindKeyboardRuns(string lower)
    {
        int i = 0;
        while (i <= lower.Length - MinKeyboardRun)
        {
            int best = 0;
            foreach (string row in KeyboardRows)
            {
                int length = LongestRunInRow(lower, i, row);
                if (length > best) best = length;
            }

            if (best >= MinKeyboardRun)
            {
                yield return lower.Substring(i, best);
                i += best;
            }
            else
            {
                i++;
            }
        }
    }

    private static int LongestRunInRow(string text, int start, string row)
    {
        int pos = row.IndexOf(text[start]);
        if (pos < 0) return 0;

        int best = 1;
        foreach (int step in (ReadOnlySpan<int>)[1, -1])
        {
            int length = 1;
            while (start + length < text.Length)
            {
                int next = pos + step * length;
                if (next < 0 || next >= row.Length || row[next] != text[start + length]) break;
                length++;
            }
            best = Math.Max(best, length);
        }
        return best;
    }

    private static bool SameClass(char a, char b) =>
        (char.IsLetter(a) && char.IsLetter(b)) || (char.IsDigit(a) && char.IsDigit(b));

    // Длинный кусок обрезаем, чтобы сообщение не растянулось на весь экран
    private static string Quote(IEnumerable<string> items) =>
        string.Join(", ", items.Select(s => s.Length > 12 ? $"«{s[..12]}…» ({s.Length} симв.)" : $"«{s}»"));
}
