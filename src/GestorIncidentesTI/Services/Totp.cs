using System.Security.Cryptography;

namespace GestorIncidentesTI.Services;

/// <summary>
/// Códigos TOTP (RFC 6238: HMAC-SHA1, 6 dígitos, 30 segundos) para comprobar una clave que
/// todavía no es la de la cuenta (la app nueva de "Perdí mi autenticador"). Acepta el paso
/// anterior y el siguiente, como Identity.
/// </summary>
public static class Totp
{
    private const int Paso = 30;

    public static string Generar(string claveBase32, DateTimeOffset momento) =>
        Codigo(Base32(claveBase32), momento.ToUnixTimeSeconds() / Paso);

    public static bool Verificar(string claveBase32, string? codigo, DateTimeOffset momento)
    {
        if (codigo is not { Length: 6 } || !codigo.All(char.IsAsciiDigit)) return false;
        var clave = Base32(claveBase32);
        var actual = momento.ToUnixTimeSeconds() / Paso;
        for (var desfase = -1; desfase <= 1; desfase++)
        {
            if (CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(Codigo(clave, actual + desfase)),
                    System.Text.Encoding.ASCII.GetBytes(codigo)))
                return true;
        }
        return false;
    }

    private static string Codigo(byte[] clave, long contador)
    {
        var mensaje = BitConverter.GetBytes(contador);
        if (BitConverter.IsLittleEndian) Array.Reverse(mensaje);
        var hash = HMACSHA1.HashData(clave, mensaje);
        var inicio = hash[^1] & 0x0F;
        var valor = ((hash[inicio] & 0x7F) << 24) | (hash[inicio + 1] << 16) | (hash[inicio + 2] << 8) | hash[inicio + 3];
        return (valor % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Base32(string texto)
    {
        const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var limpio = texto.Replace(" ", "").TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(limpio.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in limpio)
        {
            var valor = Alfabeto.IndexOf(c);
            if (valor < 0) throw new FormatException("Clave Base32 no válida.");
            buffer = (buffer << 5) | valor;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(buffer >> bits));
                buffer &= (1 << bits) - 1;
            }
        }
        return bytes.ToArray();
    }
}
