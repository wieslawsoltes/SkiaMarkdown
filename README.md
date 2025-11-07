# SkiaMarkdown

[![CI](https://img.shields.io/github/actions/workflow/status/wieslawsoltes/SkiaMarkdown/ci.yml?branch=main&label=CI)](https://github.com/wieslawsoltes/SkiaMarkdown/actions/workflows/ci.yml)
[![Pack](https://img.shields.io/github/actions/workflow/status/wieslawsoltes/SkiaMarkdown/pack.yml?branch=main&label=Pack)](https://github.com/wieslawsoltes/SkiaMarkdown/actions/workflows/pack.yml)
[![Release](https://img.shields.io/github/actions/workflow/status/wieslawsoltes/SkiaMarkdown/release.yml?branch=main&label=Release)](https://github.com/wieslawsoltes/SkiaMarkdown/actions/workflows/release.yml)

SkiaMarkdown is a GitHub Flavored Markdown (GFM) pipeline that parses Markdown into a strongly typed syntax tree, renders via SkiaSharp, and ships an Avalonia control for high-fidelity previews across desktop, mobile, and web targets. The repository contains the core parser, GPU renderer, HTML bridge, Avalonia integrations, samples, benchmarks, and compatibility suites needed to ship a production-ready Markdown experience.

## Why SkiaMarkdown?
- **Spec first:** Tracks the [GFM spec](https://github.github.com/gfm/) plus the CommonMark reference tests that live in `tests/Data` for compliance coverage.
- **GPU-accelerated rendering:** Uses SkiaSharp (with HarfBuzz text shaping) to hit sub-frame rendering budgets on discrete and integrated GPUs.
- **Avalonia-ready UI:** Bundles a reusable `MarkdownView` control plus a playground sample wired to AvaloniaEdit for live previews.
- **Extensible pipeline:** The Core library exposes buffers, tokenizer, syntax tree, and rendering stages that can be swapped, extended, or profiled.
- **Built for performance work:** Benchmarks, analyzers, trim-ready builds, and incremental parsing hooks make it easier to optimize.

## Repository layout
| Path | Description |
| --- | --- |
| `src/Core` | Core parser, tokenizer, buffers, dependency injection helpers, and pipeline primitives. |
| `src/Syntax` | Source-generated syntax descriptors, kinds, and AST helpers shared across projects. |
| `src/Rendering` | SkiaSharp-based renderer with GPU-friendly drawing primitives. |
| `src/Html` | Markdown-to-HTML utilities plus spec-compliance helpers for HTML snapshots. |
| `src/Avalonia` | Avalonia control implementations and platform plumbing for embedding previews. |
| `samples/` | `SkiaMarkdown.Samples` console host plus the `AvaloniaPlayground` interactive UI demo. |
| `tests/` | xUnit suites for Core and HTML layers, including embedded GFM/CommonMark fixtures. |
| `benchmarks/` | BenchmarkDotNet harness for parser and renderer profiling. |
| `docs/` | Roadmaps, compatibility toggles, spec notes, and architectural plans. |

## Getting started
### Prerequisites
- .NET SDK 9.0.301 or newer (the repo pins this in `global.json`).
- Windows, macOS, or Linux build host with Skia dependencies handled by NuGet (no native setup required).

### Restore, build, and test
```bash
dotnet restore SkiaMarkdown.sln
dotnet build SkiaMarkdown.sln --configuration Release
dotnet test SkiaMarkdown.sln --configuration Release --collect:"XPlat Code Coverage"
```
Use `dotnet format --verify-no-changes` locally to keep StyleCop/Roslynator guidance green before sending a PR.

### Run the Avalonia playground
```bash
dotnet run --project samples/SkiaMarkdown.AvaloniaPlayground --configuration Debug
```
The playground wires an AvaloniaEdit surface to the SkiaMarkdown pipeline so changes render in real time.

### Execute benchmarks
```bash
dotnet run --project benchmarks/SkiaMarkdown.Benchmarks --configuration Release --framework net9.0
```
BenchmarkDotNet captures parser and renderer perf counters; use `--filter` to narrow scenarios.

## CI/CD pipelines
The repository includes three GitHub Actions workflows located in `.github/workflows/`:
- `ci.yml` builds and tests the solution on Linux, Windows, and macOS for `push`/`pull_request` events.
- `pack.yml` runs `dotnet pack` and uploads NuGet artifacts for main and on-demand runs.
- `release.yml` is triggered by semantic version tags (`v*.*.*`) to build, pack, and attach NuGet packages to a GitHub Release.

Badges at the top of this README will surface the latest workflow state once the pipelines run at least once in GitHub.

## Packaging locally
```bash
DOTNET_NOLOGO=1 dotnet pack SkiaMarkdown.sln --configuration Release --output artifacts/packages --property:ContinuousIntegrationBuild=true
```
Packages are emitted under `artifacts/packages` and mirror what the pack/release workflows publish.

## Documentation & roadmap
- `docs/skia-markdown-roadmap.md` tracks the multi-phase product plan.
- `docs/compatibility-toggles.md` documents parser switches for spec compliance.
- `docs/html-generator-plan.md` and `docs/spec.txt` capture implementation notes.

## Contributing
Issues and PRs are welcome. Please run the CI steps locally, keep analyzers happy, and reference the roadmap when proposing larger features. Benchmarks and compatibility fixtures live under `tests/Data` if you need to add new coverage.

## License
SkiaMarkdown is released under the [MIT License](LICENSE).
