# SkiaMarkdown Rendering Engine Roadmap

## References
- [GitHub Flavored Markdown Spec](https://github.github.com/gfm/)
- `/Users/wieslawsoltes/GitHub/Avalonia`
- `/Users/wieslawsoltes/GitHub/SkiaSharp`

## Phases
1. [x] Phase 1 – Discovery & Requirements Alignment
   - [x] 1.1 Audit GitHub Flavored Markdown requirements, differences from CommonMark, and mandatory compliance tests.
     - GFM extends CommonMark 0.30 with task list items, tables, strikethrough, autolink literals, footnotes, and tag filter rules; compliance requires importing CommonMark spec tests (0–649) plus the GFM extension suite in `cmark-gfm/test/spec.gfm`.
     - Mandatory behaviors include GitHub-style task checkboxes (`[ ]` / `[x]`), pipe tables with alignment, permissive emphasis nesting, and raw HTML passthrough subject to sanitizer decisions.
     - Security-sensitive constructs (raw HTML, autolinks, link/image destinations) must pass through a configurable sanitizer aligned with GitHub’s tag filter to avoid script injection and attribute spoofing.
   - [x] 1.2 Catalogue target platforms (desktop, mobile, web via WebAssembly) and performance budgets for parsing/rendering.
     - Primary targets: Avalonia desktop (Windows, macOS, Linux), Avalonia Mobile (iOS, Android via .NET 10 AOT), and Avalonia WebAssembly preview for browser delivery.
     - Parsing goals: 200 KB document under 15 ms, 1 MB document under 50 ms on M2/Zen4 class CPUs; incremental reparse of 5 KB edits under 5 ms.
     - Rendering budgets: first meaningful paint < 33 ms at 1440p on discrete GPU, < 50 ms on integrated GPU/mobile; steady-state frame updates < 8 ms to preserve 120 Hz targets with virtualization enabled.
   - [x] 1.3 Review Avalonia and SkiaSharp repositories for reusable primitives, rendering patterns, text shaping, and control composition strategies.
     - Avalonia insights: reuse `SkiaPlatform` backend, `ImmediateRenderer`, `RenderTargetBitmap`, and `CompositionTarget` plumbing for control hosting; leverage `TextLayout` for fallback text measurement and `GlyphRunDrawing` interoperability.
     - SkiaSharp assets: `SKCanvas` batching, `SKTextBlobBuilder`, `SKParagraph` (via `SkiaSharp.HarfBuzz`) for shaping, `SKShaper` for complex scripts, and `SKRuntimeEffect` for shader-driven effects; adopt pooling patterns visible in `SkiaSharp.Views`.
     - Integration hooks: Avalonia `DrawingPresenter` template, `IControl` input events, `PointerGesture` infrastructure, and `RenderLoop` alignment for smooth scroll and zoom experiences.
   - [x] 1.4 Define product requirements (streaming support, incremental updates, accessibility, localization) and prioritize MVP vs. stretch goals.
     - MVP: complete GFM compliance, deterministic layout pipeline, streaming parser with incremental diff application, virtualization-aware Avalonia control, keyboard navigation, high-contrast themes, bidirectional text support.
     - Stretch: collaborative live preview hooks, real-time diff overlays, plugin API for custom syntax/renderers, offline caching of remote assets, editable markdown surface with synchronized preview.
     - Cross-cutting requirements: localization-ready resource system, WCAG 2.1 AA accessibility conformance, telemetry opt-in for performance metrics, and extension sandboxing for untrusted plugins.

2. [x] Phase 2 – Solution Architecture & Bootstrapping
   - [x] 2.1 Establish .NET 10 solution layout (Core parser library, Rendering library, Avalonia integration, samples, benchmarks, tests).
     - Solution: `SkiaMarkdown.sln` with projects `SkiaMarkdown.Core`, `SkiaMarkdown.Rendering`, `SkiaMarkdown.Avalonia`, `SkiaMarkdown.Tests`, `SkiaMarkdown.Benchmarks`, `SkiaMarkdown.Samples`.
     - Shared props file `Directory.Build.props` defining nullable enable, implicit usings, deterministic builds, analyzers, and standard package versions.
     - Directory structure mirrors projects (`src/Core`, `src/Rendering`, `src/Avalonia`, `tests`, `benchmarks`, `samples`) to simplify packaging and repo navigation.
   - [x] 2.2 Configure build tooling: nullable, analyzers, formatting, continuous integration, code quality gates.
     - Enable `TreatWarningsAsErrors`, `checked` arithmetic defaults, and analyzers: `Microsoft.CodeAnalysis.NetAnalyzers`, `StyleCop.Analyzers`, Roslynator optional pack.
     - Add `.editorconfig` with formatting rules aligned to dotnet/runtime style, integrate `dotnet format` and `dotnet workload install` steps into build scripts.
     - CI blueprint: GitHub Actions matrix (Windows, macOS, Ubuntu) running `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`, coverage via `coverlet`, artifact publishing for benchmarks.
   - [x] 2.3 Introduce cross-targeting (net10.0, net10.0-windows, net10.0-macos, net10.0-mobile) with shared project configuration and platform-specific shims.
     - Core libraries target `net10.0` with conditional compilation symbols for span-based optimizations, `Unsafe` usage behind `SKIAMARKDOWN_UNSAFE`.
     - Rendering project adds `net10.0-windows`, `net10.0-macos`, `net10.0-android`, `net10.0-ios`, and `net10.0-browserwasm` TFMs, using partial classes for platform-specific skia surface management.
     - Shared `Directory.Build.targets` injects platform shims (e.g., `SkiaMarkdown.Platform`) and ensures trimming/AOT friendliness via `IsTrimmable` metadata and analyzer suppressions where justified.
   - [x] 2.4 Set up dependency injection hooks, logging abstractions, and configuration surfaces to align with modern .NET idioms.
     - Provide extension methods `AddSkiaMarkdownCore(this IServiceCollection services)` and `AddSkiaMarkdownRendering(...)` returning builder objects for customization.
     - Integrate `ILogger<T>` injection across parser diagnostics and renderer instrumentation; optionally bridge to `Microsoft.Extensions.Logging.Abstractions`.
     - Expose `MarkdownOptions` immutable config with `OptionsBuilder<T>`, support `IConfiguration` binding, and register telemetry via `IMeterFactory` for `System.Diagnostics.Metrics`.

3. [x] Phase 3 – Core Parsing Infrastructure
   - [x] 3.1 Design high-performance tokenization pipeline using `ReadOnlySpan<char>`, `ref struct` iterators, and pooled buffers.
     - Implemented `MarkdownTokenizer` ref struct with zero-allocation line scanning, fence detection, and CommonMark/GFM block token recognition (headings, lists, tables, thematic breaks, block quotes, fences) operating on spans.
     - Added pooled token staging via `PooledBuffer<MarkdownToken>` ensuring minimal allocations while streaming tokens to the parser.
   - [x] 3.2 Define immutable abstract syntax tree (AST) and intermediate representations optimized for cache locality and structural sharing.
     - Introduced `TextSpan` and rich syntax node hierarchy (`MarkdownDocument`, `MarkdownParagraph`, `MarkdownHeading`, `MarkdownList`, `MarkdownBlockQuote`, `MarkdownCodeBlock`, `MarkdownTable`, etc.) sharing pooled source buffers for slice-based access.
     - Parser composes block nodes with inline collections and preserves source mapping for downstream rendering, diagnostics, and incremental processing.
   - [x] 3.3 Implement memory-safe storage backed by `ArrayPool<T>` with optional `Unsafe` fast paths guarded by feature flags.
     - Added pooled buffer primitives (`PooledBuffer<T>`, `PooledCharBuffer`) returning to `ArrayPool` on disposal and powering both token staging and document source retention.
     - `MarkdownDocument` implements `IDisposable`, releasing pooled memory deterministically while exposing span-based APIs for rendering and transformations.
   - [x] 3.4 Establish extensibility model (pipelines, visitors, handlers) with clear contracts for customizing parsing and rendering.
     - Delivered `MarkdownPipelineBuilder`, token filters, and document transformers (`IMarkdownExtension`, `IMarkdownTokenFilter`, `IMarkdownDocumentTransformer`) allowing extensions to intercept tokenization and mutate the AST.
     - Updated DI pipeline to surface builder hooks, extension registration helpers, and integrate extensibility into the parse loop.

4. [ ] Phase 4 – Roslyn Markdown Syntax & AST (in progress)
   - [x] 4.1 Bootstrap Roslyn-style syntax infrastructure for GFM inspired by `/Users/wieslawsoltes/GitHub/XmlParser-Roslyn`.
     - Created the `SkiaMarkdown.Syntax` assembly with green node primitives (`GreenNode`, `GreenToken`, `GreenTrivia`) and list infrastructure mirroring Roslyn layout.
     - Defined `MarkdownSyntaxKind`, `MarkdownSyntaxFacts`, trivia caches, and UTF-16 tokenization helpers (`Utf16CharReader`, `RoslynMarkdownTokenizer`) to preserve leading/trailing trivia verbatim.
   - [x] 4.2 Implement block-level parsers producing Roslyn green nodes for the full set of GFM block constructs.
     - Added a standalone Markdown block tokenizer and parser that map headings, lists, block quotes, fenced code, tables, HTML blocks, and custom containers into Roslyn green nodes.
     - Emitted canonical green-tree structures (`MarkdownDocument`, list/list-item wrappers, table sections) ready for red-tree layering and incremental parsing validation.
   - [x] 4.3 Extend Roslyn green tree coverage to inline constructs to achieve a complete Markdown syntax graph.
     - Implemented inline tokenizer/parser covering emphasis, strong, code spans, autolinks, HTML, entities, links, images, strikethrough, highlight, and hard/soft breaks.
     - Paragraphs, headings, list items, block quotes, and table cells now emit inline green nodes, wiring GitHub-specific features (task markers, tables, strikethrough, autolinks) into the Roslyn tree.
   - [x] 4.4 Surface red node API and integration points for the rest of the engine.
     - Added Roslyn-style red tree (`MarkdownSyntaxNode`, `MarkdownSyntaxToken`, `MarkdownSyntaxTrivia`) with visitor support, child enumeration, and `MarkdownSyntaxTree` entry point exposing the parsed document.
     - Inline and block parsers now feed the red tree, enabling downstream consumers to traverse mixed node/token sequences and prepare for incremental diff workflows.

5. [ ] Phase 5 – Inline Semantics & Text Services
   - [x] 5.1 Build Roslyn red-tree visitors that resolve emphasis, strong, code spans, links, images, autolinks, and raw HTML precedence rules while preserving the underlying syntax model.
     - Added inline semantic model builder atop the red tree, emitting typed semantic nodes (`Text`, `Emphasis`, `Strong`, `CodeSpan`, `Link`, `Image`, `Autolink`, `Html`, `Entity`, breaks, task markers) while respecting nested precedence.
     - Exposed `MarkdownSyntaxTree.GetInlineSemantics(...)` so downstream components can translate Roslyn syntax into resolved inline semantics without re-tokenizing.
   - [x] 5.2 Layer math extensions, emoji shortcodes, and mention/link heuristics as optional Roslyn syntax extensions with plug-in friendly architecture.
     - Added `MarkdownSyntaxOptions` with toggles for GitHub extensions, emoji shortcodes, mentions, and inline math; parsers honor these flags without reallocation.
     - Inline parser now recognizes `$...$` math, `:emoji:` shortcodes, and `@mentions`, emitting dedicated syntax kinds (`MathInline`, `EmojiInline`, `MentionInline`) and semantic nodes.
   - [x] 5.3 Utilize SIMD (`System.Runtime.Intrinsics`) for hot-path character classification, delimiter scanning, and entity decoding underpinning Roslyn token formation.
     - Introduced `SimdCharSearch` leveraging `Vector128` acceleration for `IndexOfAny` operations and rewired inline text scanning to batch-process spans while preserving escape semantics.
     - Inline parsing now falls back gracefully on non-SIMD hardware yet benefits from vectorized delimiter detection on SSE2/AdvSimd-capable CPUs, reducing branch-heavy character loops.
   - [x] 5.4 Provide resilient fallback for malformed input, surface diagnostics through Roslyn reporting mechanisms, and perform security review for script injection vectors.
     - Introduced `MarkdownDiagnostic` reporting; inline semantics now flag invalid URIs, unsanitized HTML, and malformed entities while still returning usable nodes.
     - Exposed diagnostics via `MarkdownSyntaxTree.GetInlineSemantics` to integrate sanitization pipelines and prevent script injection in downstream renderers.

6. [ ] Phase 6 – Specification Compliance & Compatibility
   - [x] 6.1 Import GFM spec test fixtures and build automated harness to validate every commit.
     - Added `gfm_spec_samples.json` fixture and `GitHubSpecTests` harness that parses documents via Roslyn syntax tree and validates flattened inline semantics with diagnostic checks.
     - Spec harness primed for expanding to the full GFM corpus, ensuring regressions surface in CI once renderer alignment is implemented.
   - [x] 6.2 Implement GitHub-specific behaviors (task list checkboxes, table alignment, strikethrough, footnotes, highlight) and map to AST nodes.
     - Extended Roslyn parser to emit footnote definition/reference nodes, task list markers, and strikethrough/highlight constructs; semantic model resolves them into typed inline records with diagnostics.
     - Table delimiter rows now record per-column alignment tokens and `MarkdownSyntaxTree.GetTableSemantic(s)` exposes alignment-aware table semantics for downstream renderers.
   - [x] 6.3 Compare output against reference implementations (e.g., cmark-gfm) to verify rendering parity for tricky cases.
     - Added `CMarkComparisonTests` which invoke `cmark-gfm` (when available) against curated fixtures and compare normalized output with the Roslyn semantic flattening helpers.
     - Harness gracefully skips when `cmark-gfm` is not installed, enabling CI parity checks on reference environments without blocking local development.
   - [x] 6.4 Document any intentional deviations with rationale and configure compatibility toggles.
     - Added compatibility guide (`docs/compatibility-toggles.md`) outlining per-feature switches (footnotes, strikethrough, highlight, table alignment, task lists, emoji, mentions, inline math).
     - Semantic APIs surface `MarkdownDiagnostic` warnings for unsafe constructs (invalid URIs, raw HTML, malformed entities), enabling host applications to enforce sanitisation policies.

7. [ ] Phase 7 – Rendering Engine with SkiaSharp
   - [x] 7.1 Architect layout primitives (block/inline layout, text runs, flow, measurement) leveraging SkiaSharp text shaping APIs.
     - Introduced `LayoutEngine` with document/paragraph/text primitives, measurement via `SKPaint`, and high-level `LayoutOptions` for typography/alignment control.
     - `MarkdownRenderer` now renders from layout results (`TextRunLayout`, `DividerLayout`), providing a foundation for future incremental layout and advanced flow.
  - [x] 7.2 Develop style system covering GitHub themes, syntax highlighting, spacing, borders, and media embedding.
     - Added `RendererStyle` themes (GitHub light/dark) with typography, color, spacing, and component styles that flow through `LayoutOptions`.
     - Refactored `LayoutEngine` to honour theme typography, padding, and per-block spacing while emitting background rectangles for code blocks and blockquote gutters.
     - Updated `MarkdownRenderer` to draw themed rectangles, dividers, and text with rounded corners, enabling immediate theme switching via layout options.
  - [x] 7.3 Implement drawing pipeline using GPU-backed surfaces, caching rendered glyph runs, and minimizing allocations.
     - Introduced reusable Skia paint instances and a glyph run cache to avoid per-frame allocations while rendering text.
     - Added GPU-friendly rendering helpers (`CreateSurface`, `RenderToImage`) that allocate GPU-backed surfaces when a `GRContext` is supplied, falling back to CPU surfaces otherwise.
     - Updated samples to exercise the new pipeline, rendering directly to images via the cached renderer.
  - [x] 7.4 Add image decoding, video placeholders, and custom drawable injection with async resource loading.
     - Extended layout engine with media-aware nodes (images, videos, custom drawables) parsing Markdown, HTML, and custom container syntax.
     - Added asynchronous media pipeline (`IMediaResourceProvider`) with GPU-friendly helpers (`RenderAsync`, `RenderToImageAsync`) that preload resources and fall back to styled placeholders.
     - Implemented theme-aware placeholders/playback badges and sample media provider wiring to demonstrate decoding hooks and custom drawables.

8. [ ] Phase 8 – Avalonia Control Integration
   - [x] 8.1 Create reusable Avalonia control(s) (MarkdownView, MarkdownScrollPresenter) hosting Skia surfaces with virtualization support.
     - Implemented `MarkdownScrollPresenter` with logical scrolling, viewport-aware rendering, and Skia surface reuse to avoid offscreen drawing.
     - Refactored `MarkdownView` into a scrollable host that composes the presenter, forwards layout options, and manages document lifecycle through the Markdown pipeline.
   - [x] 8.2 Wire dependency properties (Markdown text, theme, viewport options) and ensure reactive updates via parsing diff.
     - Added themed and viewport dependency properties (`RendererStyle`, `CanvasWidth`, `LayoutOptions`) surfaced on `MarkdownView` with automatic normalization and forwarding to the presenter.
     - Introduced asynchronous parsing with `MarkdownTextChange` diff tracking so incremental edits cancel stale work, reuse scroll offset heuristics, and notify the presenter for viewport adjustments.
   - [x] 8.3 Support input features (links, selection, copy, context menus) and accessibility (UIA, focus navigation).
     - `MarkdownScrollPresenter` now tracks text runs for hit testing, selection, and hyperlink invocation with cursor feedback and exposes events for host applications.
     - `MarkdownView` adds keyboard shortcuts, clipboard integration, context menus, and automation peer support so assistive technologies receive the synthesized document text.
   - [x] 8.4 Provide design-time previewer integration and sample pages referencing `/Users/wieslawsoltes/GitHub/Avalonia`.
     - `MarkdownView` populates a design-mode sample document so Avalonia previewers render ready-to-inspect content, including links into the local Avalonia repository.
     - Added `SkiaMarkdown.AvaloniaPreview` sample app and `MainWindow.axaml` showcasing `MarkdownView` with design data pointing to `/Users/wieslawsoltes/GitHub/Avalonia`.

9. [ ] Phase 9 – Performance & Memory Optimization
   - [ ] 9.1 Profile parser and renderer with BenchmarkDotNet, TraceEvent, and dotnet-counters to capture baseline metrics.
   - [ ] 9.2 Exploit SIMD acceleration for whitespace scanning, UTF-8/UTF-16 transcoding, and punctuation detection.
   - [ ] 9.3 Introduce incremental parsing caches, diff-aware rendering updates, and frame-skipping logic.
   - [ ] 9.4 Validate thread safety, pooling strategies, and `Unsafe` code paths through stress tests and fuzzing.

10. [ ] Phase 10 – Testing & Quality Assurance
    - [ ] 10.1 Build layered test suites: unit tests for parser nodes, golden tests for rendering snapshots, UI automation for controls.
    - [ ] 10.2 Integrate property-based tests and fuzzing to guard against crashes and security regressions.
    - [ ] 10.3 Validate cross-platform behavior (Windows, macOS, Linux, iOS, Android, browser) in CI using Avalonia runners.
    - [ ] 10.4 Establish regression tracking for performance and memory budgets with automatic alerts.

11. [ ] Phase 11 – Documentation, Samples & Developer Experience
    - [ ] 11.1 Author API reference documentation, architectural overviews, and contribution guides.
    - [ ] 11.2 Deliver sample applications (viewer, editor, diff viewer) showcasing integration scenarios.
    - [ ] 11.3 Provide migration guides comparing to existing markdown engines and guidelines for embedding.
    - [ ] 11.4 Publish design decisions, performance results, and troubleshooting playbooks.

12. [ ] Phase 12 – Release Management & Long-Term Maintenance
    - [ ] 12.1 Prepare NuGet packaging, semantic versioning strategy, and release pipeline automation.
    - [ ] 12.2 Define support policy, issue triage workflow, and SLA expectations.
    - [ ] 12.3 Schedule roadmap for future enhancements (plugins, collaborative editing, live preview).
    - [ ] 12.4 Establish governance for dependency updates, security patches, and community contributions.
