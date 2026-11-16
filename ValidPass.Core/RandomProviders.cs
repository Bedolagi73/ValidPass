using System.Security.Cryptography;

namespace ValidPass.Core;

/// <summary>Источник случайных чисел. Интерфейс — чтобы в тестах подставить предсказуемый.</summary>
public interface IRandomProvider
{
    /// <summary>Равномерное целое в [0, maxExclusive).</summary>
    int Next(int maxExclusive);
}

/// <summary>
/// Криптографический ГСЧ. RandomNumberGenerator.GetInt32 отбрасывает «лишние» значения
/// вместо взятия остатка, поэтому смещения от % нет.
/// </summary>
public sealed class CryptoRandomProvider : IRandomProvider
{
    public static CryptoRandomProvider Instance { get; } = new();

    public int Next(int maxExclusive) => RandomNumberGenerator.GetInt32(maxExclusive);
}
