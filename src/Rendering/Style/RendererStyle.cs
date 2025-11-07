using System.Collections.Generic;
using SkiaSharp;

namespace SkiaMarkdown.Rendering.Style;

public sealed record RendererStyle(
    TextStyle Body,
    IReadOnlyDictionary<int, TextStyle> Headings,
    TextStyle InlineCode,
    CodeBlockStyle CodeBlock,
    BlockquoteStyle Blockquote,
    DividerStyle Divider,
    ListStyle List,
    MediaStyle Media,
    SKColor BackgroundColor,
    float ParagraphSpacing,
    float HeadingSpacing,
    float BlockquoteSpacing,
    float HorizontalPadding)
{
    private static readonly SKTypeface BodyTypeface = SKTypeface.FromFamilyName("Segoe UI");
    private static readonly SKTypeface BodyBoldTypeface = SKTypeface.FromFamilyName("Segoe UI Semibold") ?? BodyTypeface;
    private static readonly SKTypeface MonoTypeface = SKTypeface.FromFamilyName("Consolas");

    public static RendererStyle GitHubLight { get; } = CreateGitHubLight();

    public static RendererStyle GitHubDark { get; } = CreateGitHubDark();

    public TextStyle GetHeadingStyle(int level) =>
        Headings.TryGetValue(level, out var style) ? style : Body;

    private static RendererStyle CreateGitHubLight()
    {
        var body = new TextStyle(BodyTypeface, 18f, SKColor.Parse("#24292f"));
        var headings = new Dictionary<int, TextStyle>
        {
            [1] = new TextStyle(BodyBoldTypeface, 32f, body.Color),
            [2] = new TextStyle(BodyBoldTypeface, 28f, body.Color),
            [3] = new TextStyle(BodyBoldTypeface, 24f, body.Color),
            [4] = new TextStyle(BodyBoldTypeface, 22f, body.Color),
            [5] = new TextStyle(BodyBoldTypeface, 20f, body.Color),
            [6] = new TextStyle(BodyBoldTypeface, 18f, body.Color)
        };

        var inlineCode = new TextStyle(MonoTypeface, 18f, SKColor.Parse("#d73a49"));
        var codeBlock = new CodeBlockStyle(
            new TextStyle(MonoTypeface, 16f, body.Color),
            SKColor.Parse("#f6f8fa"),
            SKColor.Parse("#eaecef"),
            Padding: 12f,
            CornerRadius: 6f,
            Spacing: 16f);

        var blockquote = new BlockquoteStyle(
            new TextStyle(BodyTypeface, 18f, SKColor.Parse("#57606a")),
            SKColor.Parse("#d0d7de"),
            BarWidth: 4f,
            Indent: 20f,
            Padding: 12f,
            Spacing: 16f);

        var divider = new DividerStyle(SKColor.Parse("#d0d7de"), 1f, Spacing: 24f);
        var list = new ListStyle(new TextStyle(BodyTypeface, 18f, body.Color), Indent: 28f, MarkerSpacing: 12f, ItemSpacing: 6f, Spacing: 16f);
        var media = new MediaStyle(
            MaxImageWidth: 760f,
            DefaultImageHeight: 360f,
            DefaultVideoHeight: 400f,
            PlaceholderBackground: SKColor.Parse("#f6f8fa"),
            PlaceholderBorder: SKColor.Parse("#d0d7de"),
            PlaceholderText: new TextStyle(BodyTypeface, 16f, SKColor.Parse("#57606a")),
            CornerRadius: 8f,
            Spacing: 24f);

        return new RendererStyle(
            Body: body,
            Headings: headings,
            InlineCode: inlineCode,
            CodeBlock: codeBlock,
            Blockquote: blockquote,
            Divider: divider,
            List: list,
            Media: media,
            BackgroundColor: SKColors.White,
            ParagraphSpacing: 16f,
            HeadingSpacing: 20f,
            BlockquoteSpacing: 12f,
            HorizontalPadding: 48f);
    }

    private static RendererStyle CreateGitHubDark()
    {
        var bodyColor = SKColor.Parse("#c9d1d9");
        var body = new TextStyle(BodyTypeface, 18f, bodyColor);
        var headings = new Dictionary<int, TextStyle>
        {
            [1] = new TextStyle(BodyBoldTypeface, 32f, bodyColor),
            [2] = new TextStyle(BodyBoldTypeface, 28f, bodyColor),
            [3] = new TextStyle(BodyBoldTypeface, 24f, bodyColor),
            [4] = new TextStyle(BodyBoldTypeface, 22f, bodyColor),
            [5] = new TextStyle(BodyBoldTypeface, 20f, bodyColor),
            [6] = new TextStyle(BodyBoldTypeface, 18f, bodyColor)
        };

        var inlineCode = new TextStyle(MonoTypeface, 18f, SKColor.Parse("#ffa657"));
        var codeBlock = new CodeBlockStyle(
            new TextStyle(MonoTypeface, 16f, bodyColor),
            SKColor.Parse("#161b22"),
            SKColor.Parse("#30363d"),
            Padding: 12f,
            CornerRadius: 6f,
            Spacing: 16f);

        var blockquote = new BlockquoteStyle(
            new TextStyle(BodyTypeface, 18f, SKColor.Parse("#8b949e")),
            SKColor.Parse("#3b434b"),
            BarWidth: 4f,
            Indent: 20f,
            Padding: 12f,
            Spacing: 16f);

        var divider = new DividerStyle(SKColor.Parse("#30363d"), 1f, Spacing: 24f);
        var list = new ListStyle(new TextStyle(BodyTypeface, 18f, bodyColor), Indent: 28f, MarkerSpacing: 12f, ItemSpacing: 6f, Spacing: 16f);
        var media = new MediaStyle(
            MaxImageWidth: 760f,
            DefaultImageHeight: 360f,
            DefaultVideoHeight: 400f,
            PlaceholderBackground: SKColor.Parse("#161b22"),
            PlaceholderBorder: SKColor.Parse("#30363d"),
            PlaceholderText: new TextStyle(BodyTypeface, 16f, SKColor.Parse("#8b949e")),
            CornerRadius: 8f,
            Spacing: 24f);

        return new RendererStyle(
            Body: body,
            Headings: headings,
            InlineCode: inlineCode,
            CodeBlock: codeBlock,
            Blockquote: blockquote,
            Divider: divider,
            List: list,
            Media: media,
            BackgroundColor: SKColor.Parse("#0d1117"),
            ParagraphSpacing: 16f,
            HeadingSpacing: 20f,
            BlockquoteSpacing: 12f,
            HorizontalPadding: 48f);
    }
}
