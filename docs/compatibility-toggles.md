## Compatibility Toggles

`MarkdownSyntaxOptions` exposes switches that let downstream hosts align behaviour with CommonMark or GitHub Flavored Markdown (GFM) expectations.

| Option | Default | Description |
|--------|---------|-------------|
| `EnableGitHubExtensions` | `true` | Enables GFM-specific block detection (tables, task lists, autolinks). |
| `EnableFootnotes` | `true` | Parses `[^label]:` block definitions and `[^label]` inline references into dedicated syntax/semantic nodes. When disabled, footnote markers render as literal text. |
| `EnableStrikethrough` | `true` | Recognises `~~text~~` delimiters; when disabled the delimiters remain literal characters. |
| `EnableHighlight` | `true` | Recognises `==highlight==` markers; disable for CommonMark-style compatibility. |
| `EnableTableAlignment` | `true` | Records per-column alignment metadata in table delimiter rows; disable to treat delimiters verbatim. |
| `EnableInlineMath` | `true` | Parses `$inline math$` spans; disable to treat dollar signs as text. |
| `EnableEmojiShortcodes` | `true` | Recognises `:emoji:` shortcodes. |
| `EnableMentions` | `true` | Recognises GitHub-style `@mention` identifiers. |
| `EnableTaskLists` | `true` | Emits task list markers (`[ ]`, `[x]`) in inline semantics. |

### Diagnostics Surface

`MarkdownSyntaxTree.GetInlineSemantics(...)` and `GetTableSemantic(...)` return diagnostics describing potentially unsafe constructs:

| Diagnostic ID | Severity | Description |
|---------------|----------|-------------|
| `MD001` | Warning | Link destination is not a valid URI. |
| `MD002` | Warning | Image source is not a valid URI. |
| `MD003` | Warning | Raw HTML detected – consumers should apply their own sanitiser. |
| `MD004` | Warning | HTML entity is missing a terminating `;`. |

Hosts can filter or escalate these diagnostics to enforce stricter compatibility or security rules.
