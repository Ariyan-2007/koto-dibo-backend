using System.Security.Cryptography;

namespace KotoDibo.Common.Helpers;

// Short, human-typeable codes for the household invite flow (read aloud, texted, retyped from a
// photo of a QR). Crockford-ish alphabet with 0/O/1/I/L removed so a misread character can't
// silently resolve to a different valid code. 32 symbols divides 256 evenly, so byte % 32 introduces
// no modulo bias.
public static class InviteCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int DefaultLength = 8;

    public static string Generate(int length = DefaultLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }
}
