using System.Text;

namespace Application.Security;

public static class PasswordLimits
{
    public const int MaximumUtf8Bytes = 72;
    public static bool Accepts(string? value) => value is not null && Encoding.UTF8.GetByteCount(value) <= MaximumUtf8Bytes;
}
