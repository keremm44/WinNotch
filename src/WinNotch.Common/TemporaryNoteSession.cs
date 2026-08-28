namespace WinNotch.Common;

/// <summary>In-memory scratch note. No file IO; content disappears with the process.</summary>
public sealed class TemporaryNoteSession
{
    public const int MaxLength = 10_000;

    public string Text { get; private set; } = string.Empty;
    public bool HasContent => Text.Length > 0;

    public void Update(string? text)
    {
        string value = text ?? string.Empty;
        Text = value.Length <= MaxLength ? value : value[..MaxLength];
    }

    public void Clear() => Text = string.Empty;
}
