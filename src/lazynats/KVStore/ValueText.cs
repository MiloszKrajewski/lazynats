using System.Text;
using System.Text.Unicode;

namespace lazynats.KVStore;

// The printable-text guard behind Edit Key (see openspec/changes/add-kv-key-crud/design.md's
// "Printable-text guard" decision): a value is editable as text only if it's valid UTF-8 and
// every decoded Rune is either not a control character or one of the three whitespace controls a
// text value can legitimately contain. This is a heuristic, not a format guarantee - see that
// design doc's "Risks" section for why a false positive (binary that happens to decode cleanly)
// is harmless here.
internal static class ValueText
{
    public static bool TryDecode(byte[] value, out string text)
    {
        if (!Utf8.IsValid(value)) {
            text = string.Empty;
            return false;
        }

        var decoded = Encoding.UTF8.GetString(value);
        foreach (var rune in decoded.EnumerateRunes()) {
            if (Rune.IsControl(rune) && rune.Value is not ('\t' or '\n' or '\r')) {
                text = string.Empty;
                return false;
            }
        }

        text = decoded;
        return true;
    }
}
