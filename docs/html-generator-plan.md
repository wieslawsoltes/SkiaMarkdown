# GFM Markdown → HTML Generator Plan

This plan describes the work required to add a reference GitHub-Flavoured Markdown
HTML generator built on top of SkiaMarkdown's Roslyn-style AST (`SkiaMarkdown.Syntax.Red`).
The generator must follow the GFM v0.29 specification (`docs/spec.txt`) and ship with
granular unit tests that prove compliance with each block/inline rule.

## Goals

- [x] Provide a library surface that accepts Markdown (as text or `MarkdownSyntaxTree`)
      and emits HTML that matches the spec.
- [x] Leverage the existing Roslyn AST so that block/inline handling is deterministic
      and can be reasoned about per node kind.
- [ ] Cover every spec section (blocks, inlines, extensions) with targeted tests,
      using examples lifted from the spec to guarantee parity.
- [x] Ensure the generator is decoupled from rendering so it can serve as a reference
      implementation or baseline for regression testing the engine.

## High-Level Tasks

- [x] **Library scaffolding**
  - [x] Create a new project (e.g. `SkiaMarkdown.Html`) with a public API:
        `MarkdownHtmlGenerator.Generate(string markdown, MarkdownHtmlOptions? options = null)`.
  - [x] Provide overloads that accept `MarkdownSyntaxTree` or `MarkdownSyntaxNode`
        to re-use parsed trees.
  - [x] Define options for HTML sanitation toggles (e.g., allow raw HTML, soft break style).

- [x] **AST visitor infrastructure**
  - [x] Implement a `MarkdownHtmlWriter` that walks the Roslyn AST via a visitor pattern.
  - [x] Handle block-level nodes first (headings, thematic breaks, block quotes, lists, tables, etc.).
  - [x] Handle inline nodes/tokens (emphasis, code spans, links, images, autolinks).
  - [x] Manage state stacks for lists, tight/loose detection, and table alignment.
  - [x] Integrate escaping helpers for text and attribute contexts (per spec).

- [ ] **Spec-driven behavior**
  - [ ] For each section in `docs/spec.txt`, enumerate acceptance criteria and map them
        to generator behaviors (e.g., Section 4.2 ATX headings → `<hN>` with trimmed text).
        - `tests/SkiaMarkdown.Html.Tests/Data/spec_html_cases.json` now captures representative inputs/outputs across every spec chapter (paragraphs, headings, thematic breaks, quotes, lists, code, tables, HTML, inline emphasis, links, images, autolinks, entities, breaks, task lists, footnotes, etc.); continue expanding this matrix with the remaining examples plus narrative mapping.
  - [x] Track special cases: task list items, strikethrough, tables, disallowed raw HTML,
        soft vs. hard line breaks, etc.
  - [ ] Document any spec deviations (should be none) inside the generator README.

- [ ] **Unit test suite**
  - [x] Add a dedicated test project (e.g., `SkiaMarkdown.Html.Tests`).
  - [ ] Organize tests mirroring spec chapters (Blocks, Containers, Inlines).
  - [ ] For each spec example, add tests asserting generated HTML equals the spec output.
  - [ ] Include regression tests for tricky combinations (nested lists, mixed HTML, tables).
  - [ ] Provide fuzz-style tests comparing the generator to known-good outputs if available.

- [ ] **Documentation**
  - [x] Extend `docs/html-generator-plan.md` (this file) with status as work progresses.
  - [ ] Add user-facing docs describing how to consume the generator and referencing
        sections of the spec it covers.
  - [ ] Briefly explain how the Roslyn AST is used to faithfully reproduce GFM semantics.

- [ ] **Integration hooks**
  - [ ] Optionally expose the HTML generator via the playground sample so users can preview
        Markdown → HTML results (toggle-able).
  - [ ] Provide a CLI or minimal command for batch conversion in future work.

## Execution Outline

- [x] **Phase 1 – Setup**
  - [x] Create project skeleton, options, and stub generator API.
- [x] **Phase 2 – Blocks**
  - [x] Implement headings, paragraphs, thematic breaks, block quotes, and lists.
  - [x] Emit fenced code blocks and HTML blocks.
  - [x] Render GFM tables with alignment metadata.
  - [x] Handle setext headings and indented code block emission.
  - [x] Handle remaining block constructs (link reference definitions, custom containers, disallowed raw HTML).
    - Custom container blocks now map to structured `<div>` wrappers with `data-*` metadata and recursively rendered content, link definitions remain non-rendering, and the GFM tag filter replaces disallowed raw HTML tags even when passthrough is enabled.
- [x] **Phase 3 – Containers**
  - [x] Implement block quotes, list constructs, and task list semantics.
- [x] **Phase 4 – Inlines**
  - [x] Emit emphasis/strong/strikethrough/code spans with escaping.
- [x] Implement link, image, and autolink rendering with attribute handling.
- [x] Support Markdown task-list checkboxes in list items.
- [x] Decode entity/numeric references and honor raw HTML toggles.
- [x] Render mentions and emoji shortcodes (with optional resolver hook).
- [x] Handle remaining inline constructs (math, footnotes, etc.).
    - Dollar-delimited math emits `<span class="math math-inline">` nodes (preserving the literal via `data-math`), highlight markers become `<mark>`, and footnote references/definitions generate `<sup>` backlinks plus a `<section class="footnotes">` list appended to the document.
- [x] **Phase 5 – Extensions**
  - [x] Add tables, strikethrough, task lists, and disallowed-raw-HTML behaviors.
- [ ] **Phase 6 – Tests _(in progress)_**
  - [x] Build per-section test suites and automation.
    - Introduced `tests/SkiaMarkdown.Html.Tests/Data/spec_html_cases.json` plus `SpecDrivenHtmlTests` to replay representative GFM sections (ATX/setext headings, code blocks, tables, task lists, footnotes) against the HTML generator; expand this corpus as additional sections are formalized.
- [ ] **Phase 7 – Docs + Integration**
  - [ ] Document API usage and integrate optional preview/CLI hooks.

The above phases can be developed incrementally; each should land with accompanying tests
before moving to the next to keep regressions detectable.
