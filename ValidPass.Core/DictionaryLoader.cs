using System.Reflection;

namespace ValidPass.Core;

/// <summary>
/// Загрузка словаря частых паролей и списков утечек: по строке на пароль или SHA-1.
/// Пустые строки, дубликаты, бинарный мусор и слишком длинные строки отбрасываются.
/// </summary>
public static class DictionaryLoader
{
    /// <summary>Строка длиннее — считаем мусором.</summary>
    public const int MaxLineLength = 256;

    private const string CommonPasswordsResource = "ValidPass.Core.common_passwords.txt";

    /// <summary>Встроенный словарь частых паролей (Resources/common_passwords.txt).</summary>
    public static PasswordList LoadCommonPasswords()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(CommonPasswordsResource)
                           ?? throw new InvalidOperationException($"Нет встроенного ресурса {CommonPasswordsResource}.");
        using var reader = new StreamReader(stream);
        return Load(reader, "встроенный словарь", null, CancellationToken.None, totalBytes: 0);
    }

    /// <summary>Из готовых строк — удобно для тестов без файлов.</summary>
    public static PasswordList FromLines(IEnumerable<string> lines, string source = "список")
    {
        var items = new HashSet<string>(StringComparer.Ordinal);
        int skipped = 0;
        foreach (string line in lines)
        {
            if (Normalize(line) is { } value) items.Add(value);
            else if (line.Trim().Length > 0) skipped++;
        }
        return new PasswordList(items, source, skipped);
    }

    /// <summary>
    /// Файл загружается в фоновом потоке; прогресс — доля 0..1 прочитанных байт.
    /// Ошибки доступа (нет файла, нет прав) пробрасываются — их показывает UI.
    /// </summary>
    public static Task<PasswordList> LoadFileAsync(string path, IProgress<double>? progress = null,
                                                    CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            return Load(reader, Path.GetFileName(path), progress, cancellationToken, stream.Length);
        }, cancellationToken);

    private static PasswordList Load(StreamReader reader, string source, IProgress<double>? progress,
                                     CancellationToken cancellationToken, long totalBytes)
    {
        var items = new HashSet<string>(StringComparer.Ordinal);
        int skipped = 0;
        long lineNumber = 0;
        double lastReported = -1;

        while (reader.ReadLine() is { } line)
        {
            if (Normalize(line) is { } value) items.Add(value);
            else if (line.Trim().Length > 0) skipped++;

            // Отмену и прогресс проверяем не на каждой строке — так быстрее
            if (++lineNumber % 4096 != 0) continue;
            cancellationToken.ThrowIfCancellationRequested();
            if (progress != null && totalBytes > 0)
            {
                double done = (double)reader.BaseStream.Position / totalBytes;
                if (done - lastReported >= 0.01)
                {
                    progress.Report(Math.Min(done, 1));
                    lastReported = done;
                }
            }
        }

        progress?.Report(1);
        return new PasswordList(items, source, skipped);
    }

    /// <summary>
    /// Строка → элемент списка: обрезка пробелов, нижний регистр.
    /// Формат HIBP «HASH:count» — берём хеш. null — строку пропустить.
    /// </summary>
    public static string? Normalize(string line)
    {
        string value = line.Trim();
        if (value.Length == 0 || value.Length > MaxLineLength) return null;
        if (value.Any(char.IsControl) || value.Contains('�')) return null; // бинарные данные

        if (value.IndexOf(':') == 40 && !value.AsSpan(0, 40).ContainsAnyExcept("0123456789abcdefABCDEF"))
            value = value[..40];

        return value.ToLowerInvariant();
    }
}
