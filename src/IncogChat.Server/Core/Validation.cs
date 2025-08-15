namespace IncogChat.Server.Core;

using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

public static class Validation
{
    private static readonly Regex NameRx = new(@"^[A-Za-z0-9 _-]{1,32}$", RegexOptions.Compiled);

    public static string ValidateName(string name)
    {
        name = (name ?? "").Trim();
        if (!NameRx.IsMatch(name)) throw new ArgumentException("Display name invalid.");
        return name;
    }

    public static string ValidateMessage(string text)
    {
        text = (text ?? "").Trim();
        if (text.Length is < 1 or > 500) throw new ArgumentException("Message length invalid.");
        return text;
    }

    public static string HtmlEncode(string text) => HtmlEncoder.Default.Encode(text);
}
