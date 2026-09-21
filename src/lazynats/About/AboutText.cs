using System.Reflection;

namespace lazynats.About;

// The embedded About.txt already has its `{product}`/`{version}`/`{author}`/`{description}`
// tokens resolved - the Nuke `GenerateAbout` target (`.nuke/build/Program.cs`) renders them from
// About.template.txt at build time. Load() is therefore a plain resource read, no substitution.
internal static class AboutText
{
    private const string ResourceName = "lazynats.About.About.txt";

    public static string Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
