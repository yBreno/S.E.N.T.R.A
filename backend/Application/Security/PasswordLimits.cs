using System.Text;

namespace Application.Security;

/// <summary>BCrypt considera no máximo 72 bytes; rejeitar evita senhas diferentes equivalentes.</summary>
public static class PasswordLimits
{
    public const int MaximumUtf8Bytes = 72;
    public static bool Accepts(string? value) => value is not null && Encoding.UTF8.GetByteCount(value) <= MaximumUtf8Bytes;
}
