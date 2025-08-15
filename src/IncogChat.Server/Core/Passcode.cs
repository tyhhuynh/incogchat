namespace IncogChat.Server.Core;

using System.Security.Cryptography;
using System.Text.RegularExpressions;

public static class Passcode
{
    private static readonly Regex EightDigits = new(@"^\d{8}$", RegexOptions.Compiled);
    private static readonly Regex Acceptable = new(@"^\d{8}$|^\d{4}-\d{4}$", RegexOptions.Compiled);

    public static string Normalize(string input)
    {
        if (!Acceptable.IsMatch(input)) throw new ArgumentException("Invalid passcode format.");
        var digits = input.Replace("-", "");
        if (!EightDigits.IsMatch(digits)) throw new ArgumentException("Passcode must be exactly 8 digits.");
        return digits;
    }

    public static string Generate()
        => RandomNumberGenerator.GetInt32(0, 100_000_000).ToString("D8");

    public static string GenerateUnique(RoomRegistry reg)
    {
        for (var i = 0; i < 5; i++)
        {
            var code = Generate();
            if (!reg.Exists(code)) return code;
        }
        while (true)
        {
            var code = Generate();
            if (!reg.Exists(code)) return code;
        }
    }
}
