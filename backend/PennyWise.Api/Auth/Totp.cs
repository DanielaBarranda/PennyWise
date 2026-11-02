using System.Security.Cryptography;
using System.Text;

namespace PennyWise.Api.Auth;

// Time-based one-time passwords (RFC 6238), the 6-digit codes that authenticator
// apps such as Google Authenticator and Microsoft Authenticator show. A code is
// HMAC-SHA1 of the current 30-second time step, keyed with a secret that only
// the server and the user's phone know.
public static class Totp
{
    public const int Digits = 6;
    private const int StepSeconds = 30;

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(20); // 160 bits, as RFC 4226 recommends

    public static long StepAt(DateTimeOffset time) => time.ToUnixTimeSeconds() / StepSeconds;

    // Accepts the code for the current step or one step either side, because
    // phone and server clocks are rarely exactly in sync. Returns the step that
    // matched, so the caller can refuse the same code a second time.
    public static long? Verify(byte[] secret, string code, DateTimeOffset now)
    {
        if (code.Length != Digits || !code.All(char.IsAsciiDigit)) return null;
        var current = StepAt(now);
        for (var step = current - 1; step <= current + 1; step++)
        {
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Code(secret, step)), Encoding.ASCII.GetBytes(code)))
            {
                return step;
            }
        }
        return null;
    }

    public static string Code(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var hash = HMACSHA1.HashData(secret, counter);
        var offset = hash[^1] & 0x0F;
        var value = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    // The link an authenticator app reads from the QR code.
    public static string SetupUri(byte[] secret, string email) =>
        $"otpauth://totp/{Uri.EscapeDataString("PennyWise:" + email)}?secret={Base32(secret)}&issuer=PennyWise&digits={Digits}&period={StepSeconds}";

    // RFC 4648 Base32 without padding: the form authenticator apps expect when
    // the secret is typed in by hand.
    public static string Base32(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                result.Append(alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0) result.Append(alphabet[(buffer << (5 - bits)) & 31]);
        return result.ToString();
    }
}
