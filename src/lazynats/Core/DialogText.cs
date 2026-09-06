namespace lazynats.Core;

internal static class DialogText
{
    /// <summary>
    /// Pads text with 1 space around it. This is just a utility so it can be used to some strings coming from data,
    /// like "message.Subject.Pad()", for titles it would be easier to just add it in a string itself " Live Feed ".
    /// No need to use "sophisticated" padding logic.
    /// </summary>
    /// <param name="text">Text to pad.</param>
    /// <returns>Padded text.</returns>
    public static string Pad(this string text) => $" {text} ";
}
