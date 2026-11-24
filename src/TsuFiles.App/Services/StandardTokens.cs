// TsuFiles: token giao diện chuẩn của hệ sinh thái (tsudev-standards v5, DESIGN_SYSTEM mục 1.2.1 và 7). Hai tệp JSON của
// bản sao chỉ-đọc `.standards/tokens/` được nhúng vào app lúc build (TsuFiles.App.csproj): lớp nền chung
// `design-tokens.json` rồi lớp bản sắc `products/files.json` gộp đè vai trò nhấn. Đổi màu = đổi bộ quy ước rồi sync,
// KHÔNG chép mã màu vào mã nguồn.

using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Avalonia.Media;

namespace TsuFiles.App.Services;

internal static class StandardTokens
{
    /// <summary><c>data-product</c> của TsuFiles trong bộ quy ước.</summary>
    public const string ProductId = "files";

    internal const string BaseResource = "TsuFiles.Standards.design-tokens.json";
    internal const string ProductResource = "TsuFiles.Standards.product.json";

    private static readonly Lazy<Tokens> Loaded = new(Load);

    /// <summary>Màu của một vai trò (vd. <c>bg-base</c>, <c>primary</c>) ở chế độ Sáng/Tối, đã gộp bản sắc sản phẩm.</summary>
    public static Color Color(string role, bool isDark)
    {
        var table = isDark ? Loaded.Value.Dark : Loaded.Value.Light;
        if (!table.TryGetValue(role, out var value))
            throw new KeyNotFoundException($"Token màu '{role}' không có trong .standards/tokens.");
        return Parse(value);
    }

    /// <summary>Chuỗi gốc trong JSON (để so khớp trong test).</summary>
    public static string Raw(string role, bool isDark) => (isDark ? Loaded.Value.Dark : Loaded.Value.Light)[role];

    /// <summary>Bán kính bo góc (px) của một cấp: <c>sm</c>, <c>md</c>, <c>lg</c>, <c>control</c>; bản sắc ghi đè nền.</summary>
    public static double Radius(string size) => Loaded.Value.Radius[size];

    /// <summary>Font tiêu đề (h1-h3, số liệu lớn) của sản phẩm, vd. "Lexend".</summary>
    public static string DisplayFont => Loaded.Value.DisplayFont;

    /// <summary>Hex (#RGB, #RRGGBB, #AARRGGBB) hoặc <c>rgba(r, g, b, a)</c> như trong tokens.</summary>
    internal static Color Parse(string value)
    {
        var v = value.Trim();
        if (v.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) && v.EndsWith(')'))
        {
            var parts = v[5..^1].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length == 4)
            {
                var a = double.Parse(parts[3], CultureInfo.InvariantCulture);
                return Avalonia.Media.Color.FromArgb(
                    (byte)Math.Round(Math.Clamp(a, 0, 1) * 255),
                    byte.Parse(parts[0], CultureInfo.InvariantCulture),
                    byte.Parse(parts[1], CultureInfo.InvariantCulture),
                    byte.Parse(parts[2], CultureInfo.InvariantCulture));
            }
        }
        return Avalonia.Media.Color.Parse(v);
    }

    private sealed record Tokens(
        Dictionary<string, string> Light,
        Dictionary<string, string> Dark,
        Dictionary<string, double> Radius,
        string DisplayFont);

    private static Tokens Load()
    {
        using var baseDoc = Read(BaseResource);
        using var productDoc = Read(ProductResource);
        var product = productDoc.RootElement;
        if (product.GetProperty("id").GetString() != ProductId)
            throw new InvalidDataException($"Tệp bản sắc nhúng không phải '{ProductId}'.");

        var light = Strings(baseDoc.RootElement.GetProperty("color").GetProperty("light"));
        var dark = Strings(baseDoc.RootElement.GetProperty("color").GetProperty("dark"));
        var colors = product.GetProperty("colors");
        foreach (var (k, v) in Strings(colors.GetProperty("light"))) light[k] = v;
        foreach (var (k, v) in Strings(colors.GetProperty("dark"))) dark[k] = v;

        var radius = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var p in baseDoc.RootElement.GetProperty("radius").EnumerateObject())
            radius[p.Name] = Px(p.Value.GetString()!);
        // Bộ bo góc theo `shape` (DESIGN_SYSTEM 1.2.3, bảng sm/md/lg/control) - cùng bảng với products.css.
        var shape = product.GetProperty("shape").GetString();
        (double Sm, double Md, double Lg, double Control)? preset = shape switch
        {
            "sac" => (2, 3, 4, 3),
            "chuan" => (4, 6, 8, 6),
            "mem" => (6, 10, 14, 10),
            "tron" => (8, 14, 20, 999),
            _ => null,
        };
        if (preset is { } r)
        {
            radius["sm"] = r.Sm;
            radius["md"] = r.Md;
            radius["lg"] = r.Lg;
            radius["control"] = r.Control;
        }

        return new Tokens(light, dark, radius, product.GetProperty("fontDisplay").GetString()!);
    }

    private static JsonDocument Read(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Thiếu tài nguyên nhúng {name} (xem TsuFiles.App.csproj).");
        return JsonDocument.Parse(stream);
    }

    private static Dictionary<string, string> Strings(JsonElement obj)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in obj.EnumerateObject())
            if (p.Value.ValueKind == JsonValueKind.String)
                d[p.Name] = p.Value.GetString()!;
        return d;
    }

    private static double Px(string value) =>
        double.Parse(value.Replace("px", "", StringComparison.Ordinal), CultureInfo.InvariantCulture);
}
