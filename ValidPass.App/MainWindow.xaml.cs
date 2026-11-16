using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ValidPass.Core;
using ValidPass.Core.Models;
using ValidPass.Core.Rules;

namespace ValidPass.App;

/// <summary>
/// Окно: только UI. Вся логика — в ValidPass.Core.
/// Пароли живут только в памяти окна: не пишутся ни в логи, ни в статусную строку, ни в настройки.
/// </summary>
public partial class MainWindow : Window
{
    private readonly PasswordGenerator _generator = new();
    private readonly LeakedPasswordRule _leakedRule = new();
    private readonly PasswordValidator _validator;
    private readonly PasswordPolicy _policy = new();
    private readonly List<CsvRecord> _checked = new();
    private List<GeneratedPassword> _generated = new();

    private CancellationTokenSource? _leaksCts;
    private bool _ready;
    private bool _syncingPassword;
    private double _strengthFraction;

    public MainWindow()
    {
        InitializeComponent();

        SpecialCharsBox.Text = CharacterSets.DefaultSpecial;
        var dictionary = DictionaryLoader.LoadCommonPasswords();
        _validator = PasswordValidator.CreateDefault(dictionary, _leakedRule);
        DictionaryInfo.Text = $"Встроенный словарь: {dictionary.Count} частых паролей.";

        ((FrameworkElement)StrengthBar.Parent).SizeChanged += (_, _) => UpdateStrengthBar();
        Closed += (_, _) => _leaksCts?.Cancel();

        _ready = true;
        UpdateGenerationInfo();
    }

    // ================= Генерация =================

    private void GenerationOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready) UpdateGenerationInfo();
    }

    /// <summary>Подсказка под кнопкой: размер алфавита и энтропия, или что не так с параметрами.</summary>
    private void UpdateGenerationInfo()
    {
        if (TryReadGenerationOptions(out var options, out string error))
        {
            double bits = PasswordGenerator.EntropyOf(options);
            var strength = EntropyCalculator.Classify(bits, _policy);
            GenerationInfo.Text = $"Алфавит: {PasswordGenerator.BuildAlphabet(options).Length} симв. · " +
                                  $"энтропия {bits:0.0} бит · {strength.ToDisplay()}";
            GenerationInfo.Foreground = (Brush)FindResource("MutedBrush");
        }
        else
        {
            GenerationInfo.Text = error;
            GenerationInfo.Foreground = StrengthColors.Fail;
        }
    }

    private bool TryReadGenerationOptions(out GenerationOptions options, out string error)
    {
        options = new GenerationOptions
        {
            Length = (int)LengthSlider.Value,
            UseLower = LowerCheck.IsChecked == true,
            UseUpper = UpperCheck.IsChecked == true,
            UseDigits = DigitsCheck.IsChecked == true,
            UseSpecial = SpecialCheck.IsChecked == true,
            SpecialChars = SpecialCharsBox.Text,
            ExcludeAmbiguous = AmbiguousCheck.IsChecked == true,
        };

        if (!TryParseInt(CountBox.Text, out int count))
        {
            error = $"Количество — целое число от 1 до {GenerationOptions.MaxCount}.";
            return false;
        }
        options.Count = count;

        if (!TryParseInt(MinLowerBox.Text, out int minLower) || !TryParseInt(MinUpperBox.Text, out int minUpper)
            || !TryParseInt(MinDigitsBox.Text, out int minDigits) || !TryParseInt(MinSpecialBox.Text, out int minSpecial))
        {
            error = "Минимумы по наборам — целые числа (0 — без минимума).";
            return false;
        }
        options.MinLower = minLower;
        options.MinUpper = minUpper;
        options.MinDigits = minDigits;
        options.MinSpecial = minSpecial;

        var errors = PasswordGenerator.ValidateOptions(options);
        error = string.Join(Environment.NewLine, errors);
        return errors.Count == 0;
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadGenerationOptions(out var options, out string error))
        {
            SetStatus(error, isError: true);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        // В фоне — чтобы окно не замирало даже на 1000 длинных паролей
        var passwords = await Task.Run(() => _generator.GenerateMany(options));
        stopwatch.Stop();

        double bits = PasswordGenerator.EntropyOf(options);
        var strength = EntropyCalculator.Classify(bits, _policy);
        var now = DateTime.Now;
        _generated = passwords.Select((p, i) => new GeneratedPassword(i + 1, p, p.Length, bits, strength, now)).ToList();

        PasswordsGrid.ItemsSource = _generated;
        EmptyGridHint.Visibility = _generated.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        SetStatus($"Сгенерировано паролей: {_generated.Count} за {stopwatch.ElapsedMilliseconds} мс. Двойной клик — скопировать.");
    }

    private void PasswordsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PasswordsGrid.SelectedItem is GeneratedPassword item)
            CopyToClipboard(item.Password, "Пароль скопирован в буфер обмена.");
    }

    private void CopySelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = PasswordsGrid.SelectedItems.OfType<GeneratedPassword>().OrderBy(p => p.Index).ToList();
        if (selected.Count == 0)
        {
            SetStatus("Выделите пароль в таблице.", isError: true);
            return;
        }
        CopyToClipboard(string.Join(Environment.NewLine, selected.Select(p => p.Password)),
                        selected.Count == 1 ? "Пароль скопирован в буфер обмена." : $"Скопировано паролей: {selected.Count}.");
    }

    private void CopyAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_generated.Count == 0)
        {
            SetStatus("Сначала сгенерируйте пароли.", isError: true);
            return;
        }
        CopyToClipboard(string.Join(Environment.NewLine, _generated.Select(p => p.Password)),
                        $"Скопировано паролей: {_generated.Count}.");
    }

    private void ExportGeneratedButton_Click(object sender, RoutedEventArgs e) =>
        ExportCsv(_generated.Select(p => p.ToCsv()).ToList(), "passwords", GenDelimiterCombo, GenMaskCheck);

    // ================= Валидация =================

    private string CurrentPassword => ShowPasswordCheck.IsChecked == true ? PasswordPlain.Text : PasswordInput.Password;

    private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_ready && !_syncingPassword) ValidateCurrent(record: false);
    }

    private void PasswordPlain_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready && !_syncingPassword) ValidateCurrent(record: false);
    }

    private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        ValidateCurrent(record: true);
        e.Handled = true;
    }

    /// <summary>Показать / скрыть: переносим текст между PasswordBox и TextBox, открытую копию стираем.</summary>
    private void ShowPasswordCheck_Changed(object sender, RoutedEventArgs e)
    {
        _syncingPassword = true;
        if (ShowPasswordCheck.IsChecked == true)
        {
            PasswordPlain.Text = PasswordInput.Password;
            PasswordInput.Visibility = Visibility.Collapsed;
            PasswordPlain.Visibility = Visibility.Visible;
            PasswordPlain.Focus();
            PasswordPlain.CaretIndex = PasswordPlain.Text.Length;
        }
        else
        {
            PasswordInput.Password = PasswordPlain.Text;
            PasswordPlain.Clear();
            PasswordPlain.Visibility = Visibility.Collapsed;
            PasswordInput.Visibility = Visibility.Visible;
            PasswordInput.Focus();
        }
        _syncingPassword = false;
    }

    private void ValidateButton_Click(object sender, RoutedEventArgs e) => ValidateCurrent(record: true);

    private void PolicyOption_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        ValidateCurrent(record: false);
        UpdateGenerationInfo();
    }

    /// <summary>
    /// Проверяет введённый пароль. record = true — по кнопке «Проверить» или Enter:
    /// результат попадает в список для экспорта.
    /// </summary>
    private void ValidateCurrent(bool record)
    {
        if (!TryReadPolicy(out string error))
        {
            SetStatus(error, isError: true);
            return;
        }

        string password = CurrentPassword;
        if (password.Length == 0 && !record)
        {
            ShowEmptyResult();
            return;
        }

        var result = _validator.Validate(password, _policy);
        var strengthBrush = StrengthColors.For(result.Strength);

        VerdictText.Inlines.Clear();
        VerdictText.Inlines.Add(new Run(result.IsValid ? "Валиден" : "Невалиден")
        {
            Foreground = result.IsValid ? StrengthColors.Pass : StrengthColors.Fail,
        });
        VerdictText.Inlines.Add(new Run(" · "));
        VerdictText.Inlines.Add(new Run(result.Strength.ToDisplay()) { Foreground = strengthBrush });

        int length = EntropyCalculator.Length(password);
        EntropyText.Text = $"{result.EntropyBits:0.0} бит · алфавит {EntropyCalculator.EstimateAlphabet(password)} · {length} симв.";

        // Шкала из четырёх ступеней — как подписи под ней
        _strengthFraction = ((int)result.Strength + 1) / 4.0;
        StrengthBar.Background = strengthBrush;
        UpdateStrengthBar();

        RulesList.ItemsSource = result.Rules.Select(r => new RuleView(r.RuleName, r.Message, r.Passed)).ToList();

        if (!record) return;
        if (password.Length > 0)
        {
            _checked.Add(new CsvRecord(password, length, result.EntropyBits, result.Strength, DateTime.Now));
            CheckedCountText.Text = $"Проверено: {_checked.Count}";
        }
        int violations = result.Rules.Count(r => !r.Passed);
        SetStatus(result.IsValid
            ? $"Пароль валиден, стойкость — {result.Strength.ToDisplay()}."
            : $"Пароль невалиден: нарушено правил — {violations}.", isError: !result.IsValid);
    }

    private void ShowEmptyResult()
    {
        VerdictText.Inlines.Clear();
        VerdictText.Text = "Введите пароль";
        EntropyText.Text = "";
        _strengthFraction = 0;
        UpdateStrengthBar();
        RulesList.ItemsSource = null;
    }

    private void UpdateStrengthBar()
    {
        var track = (FrameworkElement)StrengthBar.Parent;
        StrengthBar.Width = Math.Max(0, track.ActualWidth * _strengthFraction);
    }

    private bool TryReadPolicy(out string error)
    {
        error = "";
        if (!TryParseInt(MinLengthBox.Text, out int minLength) || minLength is < 0 or > 1000)
        {
            error = "Мин. длина — целое число от 0 до 1000.";
            return false;
        }
        if (!TryParseInt(MaxLengthBox.Text, out int maxLength) || maxLength < 0 || (maxLength > 0 && maxLength < minLength))
        {
            error = "Макс. длина — 0 (без ограничения) или число не меньше минимальной.";
            return false;
        }
        if (!TryParseDouble(MediumBox.Text, out double medium) || !TryParseDouble(StrongBox.Text, out double strong)
            || !TryParseDouble(VeryStrongBox.Text, out double veryStrong) || medium < 0 || medium > strong || strong > veryStrong)
        {
            error = "Пороги шкалы — числа по возрастанию: средний ≤ сильный ≤ очень сильный.";
            return false;
        }

        _policy.MinLength = minLength;
        _policy.MaxLength = maxLength;
        _policy.RequireLower = ReqLowerCheck.IsChecked == true;
        _policy.RequireUpper = ReqUpperCheck.IsChecked == true;
        _policy.RequireDigits = ReqDigitsCheck.IsChecked == true;
        _policy.RequireSpecial = ReqSpecialCheck.IsChecked == true;
        _policy.MediumThreshold = medium;
        _policy.StrongThreshold = strong;
        _policy.VeryStrongThreshold = veryStrong;
        return true;
    }

    private void ExportCheckedButton_Click(object sender, RoutedEventArgs e) =>
        ExportCsv(_checked, "checked", ValDelimiterCombo, ValMaskCheck);

    // ================= Список утечек =================

    private async void LoadLeaksButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Список утёкших паролей",
            Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != true) return;

        _leaksCts = new CancellationTokenSource();
        LoadLeaksButton.IsEnabled = false;
        CancelLeaksButton.IsEnabled = true;
        LeaksProgress.Value = 0;
        LeaksProgress.Visibility = Visibility.Visible;
        SetStatus($"Загрузка {Path.GetFileName(dialog.FileName)}…");

        var progress = new Progress<double>(p => LeaksProgress.Value = p);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var leaks = await DictionaryLoader.LoadFileAsync(dialog.FileName, progress, _leaksCts.Token);
            _leakedRule.Leaks = leaks;

            LeaksInfo.Text = leaks.Count == 0
                ? $"Утечки: {leaks.Source} — файл пуст, проверка утечек пропускается."
                : $"Утечки: {leaks.Source} — {leaks.Count:N0} записей"
                  + (leaks.SkippedLines > 0 ? $", отброшено строк-мусора: {leaks.SkippedLines:N0}." : ".");
            SetStatus($"Список утечек загружен за {stopwatch.ElapsedMilliseconds} мс.");
            ValidateCurrent(record: false);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Загрузка списка утечек отменена. Прежний список не изменён.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus("Не удалось загрузить список утечек.", isError: true);
            MessageBox.Show(this, $"Не удалось прочитать файл:\n{ex.Message}", "Ошибка загрузки",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _leaksCts.Dispose();
            _leaksCts = null;
            LoadLeaksButton.IsEnabled = true;
            CancelLeaksButton.IsEnabled = false;
            LeaksProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelLeaksButton_Click(object sender, RoutedEventArgs e) => _leaksCts?.Cancel();

    // ================= Общее =================

    /// <summary>Экспорт с предупреждением об открытом виде паролей.</summary>
    private void ExportCsv(IReadOnlyCollection<CsvRecord> records, string filePrefix, ComboBox delimiterCombo, CheckBox maskCheck)
    {
        if (records.Count == 0)
        {
            SetStatus("Нечего экспортировать: список пуст.", isError: true);
            return;
        }

        bool mask = maskCheck.IsChecked == true;
        if (!mask)
        {
            var answer = MessageBox.Show(this,
                "Пароли будут сохранены в CSV в открытом виде: любой, кто откроет файл, их увидит.\n\n" +
                "Чтобы сохранить только маски (abc***xyz), отметьте «Маскировать».\n\nПродолжить?",
                "Пароли в открытом виде", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.OK) return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Экспорт в CSV",
            Filter = "CSV (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };
        if (dialog.ShowDialog(this) != true) return;

        char delimiter = delimiterCombo.SelectedIndex == 1 ? ';' : ',';
        try
        {
            CsvExporter.ExportToFile(dialog.FileName, records, delimiter, mask);
            SetStatus($"Экспортировано записей: {records.Count} → {Path.GetFileName(dialog.FileName)}" + (mask ? " (маски)." : "."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus("Экспорт не выполнен. Данные в приложении не потеряны.", isError: true);
            MessageBox.Show(this, $"Не удалось записать файл:\n{ex.Message}", "Ошибка экспорта",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopyToClipboard(string text, string doneMessage)
    {
        try
        {
            Clipboard.SetText(text);
            SetStatus(doneMessage);
        }
        catch (ExternalException)
        {
            // Буфер обмена держит другое приложение
            SetStatus("Буфер обмена занят другим приложением, попробуйте ещё раз.", isError: true);
        }
    }

    private void SetStatus(string text, bool isError = false)
    {
        StatusText.Text = text;
        StatusText.Foreground = isError ? StrengthColors.Fail : (Brush)FindResource("TextBrush");
    }

    private static bool TryParseInt(string text, out int value) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryParseDouble(string text, out double value) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
