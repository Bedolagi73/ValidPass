using System.Globalization;
using System.Text;
using ValidPass.Core.Models;

namespace ValidPass.Core;

/// <summary>Экспорт в CSV: заголовки, настраиваемый разделитель, UTF-8 с BOM, экранирование по RFC 4180.</summary>
public static class CsvExporter
{
    public static readonly string[] Header = ["password", "length", "entropyBits", "strength", "timestamp"];

    /// <summary>
    /// Пишет файл целиком. Пустой список — InvalidOperationException, файл не создаётся.
    /// Текст собирается в памяти и пишется одним вызовом — при ошибке записи данные в приложении не теряются.
    /// </summary>
    public static void ExportToFile(string path, IReadOnlyCollection<CsvRecord> records, char delimiter = ',', bool mask = false)
    {
        if (records.Count == 0)
            throw new InvalidOperationException("Нечего экспортировать: список пуст.");

        var writer = new StringWriter(CultureInfo.InvariantCulture);
        Write(writer, records, delimiter, mask);
        File.WriteAllText(path, writer.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public static void Write(TextWriter writer, IEnumerable<CsvRecord> records, char delimiter = ',', bool mask = false)
    {
        writer.Write(string.Join(delimiter, Header));
        writer.Write("\r\n");

        foreach (var r in records)
        {
            string[] fields =
            [
                Escape(mask ? Mask(r.Password) : r.Password, delimiter),
                r.Length.ToString(CultureInfo.InvariantCulture),
                r.EntropyBits.ToString("0.0", CultureInfo.InvariantCulture),
                Escape(r.Strength.ToDisplay(), delimiter),
                r.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            ];
            writer.Write(string.Join(delimiter, fields));
            writer.Write("\r\n");
        }
    }

    /// <summary>Поле с разделителем, кавычкой или переводом строки — в кавычки, кавычки удваиваются.</summary>
    public static string Escape(string field, char delimiter)
    {
        bool needsQuotes = field.Contains(delimiter) || field.Contains('"') || field.Contains('\r') || field.Contains('\n')
                           || (field.Length > 0 && (field[0] == ' ' || field[^1] == ' '));
        return needsQuotes ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
    }

    /// <summary>Маска: abc***xyz. Короткие пароли раскрываются меньше, до 4 символов — только звёздочки.</summary>
    public static string Mask(string password)
    {
        if (password.Length <= 4) return "***";
        if (password.Length < 8) return $"{password[0]}***{password[^1]}";
        return $"{password[..3]}***{password[^3..]}";
    }
}
