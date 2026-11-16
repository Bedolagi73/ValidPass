namespace ValidPass.Core;

/// <summary>
/// Список паролей в памяти: словарь частых или список утечек.
/// Строки нормализованы в нижний регистр, поиск — за O(1).
/// </summary>
public sealed class PasswordList
{
    public PasswordList(HashSet<string> items, string source, int skippedLines = 0)
    {
        Items = items;
        Source = source;
        SkippedLines = skippedLines;
    }

    public static PasswordList Empty { get; } = new(new HashSet<string>(StringComparer.Ordinal), "пусто");

    public IReadOnlySet<string> Items { get; }

    /// <summary>Откуда загружен — для сообщений.</summary>
    public string Source { get; }

    /// <summary>Сколько строк отброшено как мусор (бинарные, слишком длинные).</summary>
    public int SkippedLines { get; }

    public int Count => Items.Count;

    public bool Contains(string normalized) => Items.Contains(normalized);
}
