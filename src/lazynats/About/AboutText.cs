using System.Reflection;

namespace lazynats.About;

// Loads the embedded About.txt and substitutes its `{token}` placeholders via plain
// string.Replace - no templating engine, per design.md's "AOT-trivial mechanism" decision. One
// method per known token rather than a dictionary-driven loop, so adding a new token later is a
// one-line addition here, not a new abstraction.
internal static class AboutText
{
    private const string ResourceName = "lazynats.About.About.txt";

    public static string Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        return RenderAboutText(text);
    }

    private static string RenderAboutText(string text) =>
        text
            .Replace("{product}", ResolveProduct())
            .Replace("{version}", ResolveVersion())
            .Replace("{author}", ResolveAuthor())
            .Replace("{description}", ResolveDescription());

    // AssemblyInformationalVersionAttribute carries GitVersion's full semantic version in release
    // builds (see platform-release-builds); a local dotnet build/run with no version stamped
    // falls back to AssemblyName's own default (e.g. "1.0.0.0").
    private static string ResolveVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(informational)) return informational;

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    // AssemblyProductAttribute/AssemblyDescriptionAttribute/AssemblyCompanyAttribute all come
    // from PublicAssembly.props' Product/Description/Company MSBuild properties (Company holds
    // Authors' value - see that file's own comment on why), generated into this assembly at
    // build time by the SDK's default GenerateAssemblyInfo - not hand-maintained constants here.
    private static string ResolveProduct() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>()?.Product
        ?? Assembly.GetExecutingAssembly().GetName().Name
        ?? "lazynats";

    private static string ResolveAuthor() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "Unknown";

    private static string ResolveDescription() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;
}
