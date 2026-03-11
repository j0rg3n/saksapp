using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;

namespace SaksAppWeb.Services;

public class LinuxFontResolver : IFontResolver
{
    public string DefaultFontName => "LiberationSans";

    private static readonly Dictionary<string, Dictionary<(bool Bold, bool Italic), string>> FontMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LiberationSans"] = new()
        {
            { (false, false), "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf" },
            { (true, false), "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf" },
            { (false, true), "/usr/share/fonts/truetype/liberation/LiberationSans-Italic.ttf" },
            { (true, true), "/usr/share/fonts/truetype/liberation/LiberationSans-BoldItalic.ttf" },
        },
        ["LiberationSerif"] = new()
        {
            { (false, false), "/usr/share/fonts/truetype/liberation/LiberationSerif-Regular.ttf" },
            { (true, false), "/usr/share/fonts/truetype/liberation/LiberationSerif-Bold.ttf" },
            { (false, true), "/usr/share/fonts/truetype/liberation/LiberationSerif-Italic.ttf" },
            { (true, true), "/usr/share/fonts/truetype/liberation/LiberationSerif-BoldItalic.ttf" },
        },
        ["LiberationMono"] = new()
        {
            { (false, false), "/usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf" },
            { (true, false), "/usr/share/fonts/truetype/liberation/LiberationMono-Bold.ttf" },
            { (false, true), "/usr/share/fonts/truetype/liberation/LiberationMono-Italic.ttf" },
            { (true, true), "/usr/share/fonts/truetype/liberation/LiberationMono-BoldItalic.ttf" },
        },
    };

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (!FontMap.TryGetValue(familyName, out var variants))
            return null;

        var key = (isBold, isItalic);
        if (!variants.TryGetValue(key, out var fontPath))
            return null;

        return new FontResolverInfo(fontPath);
    }

    public byte[] GetFont(string faceName)
    {
        return File.ReadAllBytes(faceName);
    }
}

public static class FontResolverBootstrap
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        GlobalFontSettings.FontResolver = new LinuxFontResolver();
        _initialized = true;
    }
}
