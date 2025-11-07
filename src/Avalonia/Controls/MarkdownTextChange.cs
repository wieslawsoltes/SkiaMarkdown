using System;

namespace SkiaMarkdown.Avalonia.Controls;

/// <summary>
/// Represents a contiguous text change between two Markdown buffers.
/// </summary>
public readonly struct MarkdownTextChange
{
    public static MarkdownTextChange None { get; } = default;

    public MarkdownTextChange(int start, int oldLength, int newLength)
    {
        Start = start;
        OldLength = oldLength;
        NewLength = newLength;
    }

    /// <summary>
    /// Gets the starting offset of the change in the new buffer.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Gets the number of characters removed by the change.
    /// </summary>
    public int OldLength { get; }

    /// <summary>
    /// Gets the number of characters inserted by the change.
    /// </summary>
    public int NewLength { get; }

    /// <summary>
    /// Gets the net change in length (<see cref="NewLength"/> - <see cref="OldLength"/>).
    /// </summary>
    public int LengthDelta => NewLength - OldLength;

    /// <summary>
    /// Gets a value indicating whether the change actually modifies the buffer.
    /// </summary>
    public bool HasChanges => OldLength != 0 || NewLength != 0;

    public static MarkdownTextChange FromBuffers(ReadOnlySpan<char> oldText, ReadOnlySpan<char> newText)
    {
        if (oldText.SequenceEqual(newText))
        {
            return None;
        }

        var prefix = 0;
        var maxPrefix = Math.Min(oldText.Length, newText.Length);
        while (prefix < maxPrefix && oldText[prefix] == newText[prefix])
        {
            prefix++;
        }

        var oldSuffix = oldText.Length - prefix;
        var newSuffix = newText.Length - prefix;
        var suffix = 0;
        var maxSuffix = Math.Min(oldSuffix, newSuffix);

        while (suffix < maxSuffix &&
               oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix])
        {
            suffix++;
        }

        var oldLength = oldText.Length - prefix - suffix;
        var newLength = newText.Length - prefix - suffix;

        if (oldLength < 0)
        {
            oldLength = 0;
        }

        if (newLength < 0)
        {
            newLength = 0;
        }

        return new MarkdownTextChange(prefix, oldLength, newLength);
    }
}
