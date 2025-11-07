using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace SkiaMarkdown.Avalonia.Controls;

/// <summary>
/// Automation peer exposing text content for assistive technologies.
/// </summary>
public sealed class MarkdownViewAutomationPeer : ScrollViewerAutomationPeer
{
    public MarkdownViewAutomationPeer(MarkdownView owner)
        : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;
}
