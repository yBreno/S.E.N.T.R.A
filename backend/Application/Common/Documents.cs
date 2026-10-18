using System.Text.RegularExpressions;
namespace Application.Common;

/// <summary>Utilitarios de normalizacao/validacao de documentos brasileiros.</summary>
public static partial class Documents
{
    public static string Digits(string? s) => new((s ?? string.Empty).Where(char.IsDigit).ToArray());

    public static bool IsValidCpf(string? value)
    {
        var cpf = Digits(value);
        if (cpf.Length != 11 || cpf.Distinct().Count() == 1) return false;
        return cpf[9] - '0' == CheckDigit(cpf, 9) && cpf[10] - '0' == CheckDigit(cpf, 10);

        static int CheckDigit(string c, int len)
        {
            var sum = 0;
            for (var i = 0; i < len; i++) sum += (c[i] - '0') * (len + 1 - i);
            var r = sum * 10 % 11;
            return r == 10 ? 0 : r;
        }
    }

    public static bool IsValidCnpj(string? value)
    {
        var c = Digits(value);
        if (c.Length != 14 || c.Distinct().Count() == 1) return false;
        int[] w1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] w2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        return c[12] - '0' == Digit(c, w1) && c[13] - '0' == Digit(c, w2);

        static int Digit(string c, int[] w)
        {
            var sum = 0;
            for (var i = 0; i < w.Length; i++) sum += (c[i] - '0') * w[i];
            var r = sum % 11;
            return r < 2 ? 0 : 11 - r;
        }
    }

    public static string NormalizePlate(string? plate) =>
        (plate ?? string.Empty).Replace("-", "").Replace(" ", "").ToUpperInvariant();

    /// <summary>Placa antiga (ABC1234) ou Mercosul (ABC1D23).</summary>
    public static bool IsValidLicensePlate(string? plate) => PlateRegex().IsMatch(NormalizePlate(plate));

    public static bool IsValidZipCode(string? zip) => Digits(zip).Length == 8;

    /// <summary>LGPD: ***.456.789-** (nunca devolve o CPF completo).</summary>
    public static string MaskCpf(string? cpf)
    {
        var d = Digits(cpf);
        return d.Length != 11 ? "***.***.***-**" : $"***.{d.Substring(3, 3)}.{d.Substring(6, 3)}-**";
    }

    public static bool IsValidLatitude(double v) => v is >= -90 and <= 90;
    public static bool IsValidLongitude(double v) => v is >= -180 and <= 180;

    [GeneratedRegex("^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$")]
    private static partial Regex PlateRegex();
}
