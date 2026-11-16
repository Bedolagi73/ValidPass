using System.Windows.Media;
using ValidPass.Core.Models;

namespace ValidPass.App;

/// <summary>Строка таблицы сгенерированных паролей.</summary>
public sealed record GeneratedPassword(int Index, string Password, int Length, double EntropyBits, Strength Strength, DateTime Timestamp)
{
    public string StrengthText => Strength.ToDisplay();

    public Brush StrengthBrush => StrengthColors.For(Strength);

    public CsvRecord ToCsv() => new(Password, Length, EntropyBits, Strength, Timestamp);
}

/// <summary>Строка списка правил на вкладке «Валидация».</summary>
public sealed record RuleView(string Name, string Message, bool Passed)
{
    public string Icon => Passed ? "✔" : "✖";

    public Brush IconBrush => Passed ? StrengthColors.Pass : StrengthColors.Fail;
}

public static class StrengthColors
{
    public static readonly Brush Pass = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)));
    public static readonly Brush Fail = Freeze(new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)));

    private static readonly Brush Weak = Fail;
    private static readonly Brush Medium = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)));
    private static readonly Brush Strong = Freeze(new SolidColorBrush(Color.FromRgb(0x65, 0xA3, 0x0D)));
    private static readonly Brush VeryStrong = Freeze(new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3D)));

    public static Brush For(Strength strength) => strength switch
    {
        Strength.Weak => Weak,
        Strength.Medium => Medium,
        Strength.Strong => Strong,
        _ => VeryStrong,
    };

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
