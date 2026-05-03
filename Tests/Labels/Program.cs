using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using DrawnUi.Draw;
using SkiaSharp;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).RunAll();

// Thin wrappers that expose protected measurement methods for direct benchmarking
class BenchLabel : SkiaLabel
{
    public new (float Width, LineGlyph[] Glyphs) MeasureLineGlyphs(SKPaint p, string t, bool shaping, float scale)
        => base.MeasureLineGlyphs(p, t, shaping, scale);
    public new (float Width, LineGlyph[] Glyphs) MeasureLineGlyphsProbe(SKPaint p, string t, bool shaping, float scale)
        => base.MeasureLineGlyphsProbe(p, t, shaping, scale);
}

class BenchRichLabel : SkiaRichLabel
{
    public new (float Width, LineGlyph[] Glyphs) MeasureLineGlyphs(SKPaint p, string t, bool shaping, float scale)
        => base.MeasureLineGlyphs(p, t, shaping, scale);
    public new (float Width, LineGlyph[] Glyphs) MeasureLineGlyphsProbe(SKPaint p, string t, bool shaping, float scale)
        => base.MeasureLineGlyphsProbe(p, t, shaping, scale);
}

// ─────────────────────────────────────────────────────────────────────────────
// Synthetic benchmarks — generated words, all unique → worst-case for caching.
// Probe = what word-wrap loop uses. Committed = what rendering uses.
// ─────────────────────────────────────────────────────────────────────────────

[MemoryDiagnoser]
[SimpleJob]
public class SkiaLabelMeasureBenchmark
{
    private BenchLabel _label = null!;
    private SKPaint _paint = null!;
    private string[] _accumulatedLines = null!;

    [Params(20, 50, 100)]
    public int WordCount;

    [GlobalSetup]
    public void Setup()
    {
        _label = new BenchLabel();
        _paint = new SKPaint { Typeface = SKTypeface.Default, TextSize = 16f };

        var words = Enumerable.Range(0, WordCount).Select(GenerateWord).ToArray();
        _accumulatedLines = new string[WordCount];
        for (int i = 0; i < WordCount; i++)
            _accumulatedLines[i] = string.Join(" ", words, 0, i + 1);
    }

    [GlobalCleanup]
    public void Cleanup() => _paint.Dispose();

    [Benchmark(Baseline = true, Description = "MeasureLineGlyphs (committed)")]
    public float Committed()
    {
        float total = 0;
        foreach (var line in _accumulatedLines)
            total += _label.MeasureLineGlyphs(_paint, line, false, 1f).Width;
        return total;
    }

    [Benchmark(Description = "MeasureLineGlyphsProbe (probe/fast)")]
    public float Probe()
    {
        float total = 0;
        foreach (var line in _accumulatedLines)
            total += _label.MeasureLineGlyphsProbe(_paint, line, false, 1f).Width;
        return total;
    }

    private static readonly string[] _syllables =
        ["the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog",
         "pack", "my", "box", "with", "five", "dozen", "liquor", "jugs"];

    private static string GenerateWord(int index) =>
        _syllables[index % _syllables.Length] + (index / _syllables.Length > 0 ? (index / _syllables.Length).ToString() : "");
}

[MemoryDiagnoser]
[SimpleJob]
public class SkiaRichLabelMeasureBenchmark
{
    private BenchRichLabel _label = null!;
    private SKPaint _paint = null!;
    private string[] _accumulatedLines = null!;

    [Params(20, 50, 100)]
    public int WordCount;

    [GlobalSetup]
    public void Setup()
    {
        _label = new BenchRichLabel();

        // Add a dummy span so Spans.Count > 0 → complex measurement path
        var dummySpan = new TextSpan { Text = "x", TypeFace = SKTypeface.Default };
        _label.Spans.Add(dummySpan);

        _paint = new SKPaint { Typeface = SKTypeface.Default, TextSize = 16f };

        var words = Enumerable.Range(0, WordCount).Select(GenerateWord).ToArray();
        _accumulatedLines = new string[WordCount];
        for (int i = 0; i < WordCount; i++)
            _accumulatedLines[i] = string.Join(" ", words, 0, i + 1);
    }

    [GlobalCleanup]
    public void Cleanup() => _paint.Dispose();

    [Benchmark(Baseline = true, Description = "MeasureLineGlyphs (committed)")]
    public float Committed()
    {
        float total = 0;
        foreach (var line in _accumulatedLines)
            total += _label.MeasureLineGlyphs(_paint, line, false, 1f).Width;
        return total;
    }

    [Benchmark(Description = "MeasureLineGlyphsProbe (probe/fast)")]
    public float Probe()
    {
        float total = 0;
        foreach (var line in _accumulatedLines)
            total += _label.MeasureLineGlyphsProbe(_paint, line, false, 1f).Width;
        return total;
    }

    private static readonly string[] _syllables =
        ["the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog",
         "pack", "my", "box", "with", "five", "dozen", "liquor", "jugs"];

    private static string GenerateWord(int index) =>
        _syllables[index % _syllables.Length] + (index / _syllables.Length > 0 ? (index / _syllables.Length).ToString() : "");
}

// ─────────────────────────────────────────────────────────────────────────────
// Realistic markdown benchmarks — headings, bullets, code blocks, emojis.
// ─────────────────────────────────────────────────────────────────────────────

[MemoryDiagnoser]
[SimpleJob]
public class SkiaLabelMarkdownBenchmark
{
    private BenchLabel _label = null!;
    private SKPaint _paint = null!;

    private static readonly string[] _doc =
    [
        "# 🚀 Getting Started with SkiaSharp",
        "",
        "## Overview",
        "",
        "SkiaSharp is a cross-platform 2D graphics API for .NET platforms based on Google's Skia library.",
        "It is the graphics engine behind Google Chrome, Flutter, Android, and many other products.",
        "The library exposes the full power of Skia through a clean and idiomatic .NET API.",
        "",
        "## Installation",
        "",
        "Add the NuGet packages to your project:",
        "",
        "```bash",
        "dotnet add package SkiaSharp",
        "dotnet add package SkiaSharp.HarfBuzz",
        "dotnet add package SkiaSharp.Views.Maui.Controls",
        "```",
        "",
        "## Drawing Text",
        "",
        "To draw text on a canvas use `SKCanvas.DrawText` with an `SKPaint` configured for text rendering.",
        "The font size, typeface, color, and text alignment are all set on the paint object before drawing.",
        "Always dispose the paint and canvas objects after use to avoid memory leaks and GC pressure.",
        "",
        "```csharp",
        "using var paint = new SKPaint { TextSize = 16f, Color = SKColors.Black, IsAntialias = true };",
        "paint.Typeface = SKTypeface.FromFamilyName(\"Arial\", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);",
        "canvas.DrawText(\"Hello, world! 🌍\", x, y, paint);",
        "```",
        "",
        "## Word Wrap and Measurement",
        "",
        "Word wrapping works by measuring candidate strings: \"word1\", \"word1 word2\", \"word1 word2 word3\".",
        "The same words appear over and over across lines so caching per-word measurements is very effective.",
        "Common English words like the, and, is, a, to, of, in, that, it, with, for, on, are repeat constantly.",
        "In a typical document the top 20 words by frequency account for roughly 25% of all tokens.",
        "",
        "## Performance Tips 🏎️",
        "",
        "- Reuse `SKPaint` objects — allocation and GC pressure is significant in hot render loops.",
        "- Cache glyph measurements — measuring the same word on every frame is pure waste.",
        "- Use `SKBitmap` for off-screen rendering and blit with `DrawBitmap` to avoid overdraw.",
        "- Avoid `string.Format` and LINQ in tight render paths — prefer `Span<char>` and direct loops.",
        "- Use `SKTypeface.FromFile` only once per font file and cache the result for the app lifetime.",
        "- Pre-warm the typeface cache at startup so the first frame does not stall on font loading.",
        "",
        "## Emoji and Bidirectional Text 😀🎉🔥",
        "",
        "Emoji require HarfBuzz shaping to render correctly. Enable shaping when text contains emoji codepoints.",
        "The shaping path is slower but necessary for correct cluster rendering of sequences like 🏳️‍🌈 and 👨‍👩‍👧‍👦.",
        "Right-to-left scripts such as Arabic and Hebrew also require shaping and bidi reordering.",
        "",
        "## Links and Further Reading",
        "",
        "See the official Microsoft docs at https://learn.microsoft.com/dotnet/api/skiasharp for the full API.",
        "The SkiaSharp source is on GitHub at https://github.com/mono/SkiaSharp — PRs are welcome.",
        "Join the discussion on GitHub Issues if you find a bug or have a feature request for the library.",
        "",
        "## Summary",
        "",
        "SkiaSharp is fast, flexible, and runs on iOS, Android, macOS, Windows, WebAssembly, and Linux.",
        "With per-word measurement caching the render pipeline handles long rich-text documents efficiently.",
        "The cache hit rate on real English prose is significantly higher than on artificially unique words.",
    ];

    [GlobalSetup]
    public void Setup()
    {
        _label = new BenchLabel();
        _paint = new SKPaint { Typeface = SKTypeface.Default, TextSize = 16f };
    }

    [GlobalCleanup]
    public void Cleanup() => _paint.Dispose();

    [Benchmark(Baseline = true, Description = "MeasureLineGlyphs/Markdown (committed)")]
    public float Committed()
    {
        float total = 0;
        foreach (var line in _doc)
            total += _label.MeasureLineGlyphs(_paint, line, false, 1f).Width;
        return total;
    }

    [Benchmark(Description = "MeasureLineGlyphsProbe/Markdown (probe/fast)")]
    public float Probe()
    {
        float total = 0;
        foreach (var line in _doc)
            total += _label.MeasureLineGlyphsProbe(_paint, line, false, 1f).Width;
        return total;
    }
}

[MemoryDiagnoser]
[SimpleJob]
public class SkiaRichLabelMarkdownBenchmark
{
    private BenchRichLabel _label = null!;
    private SKPaint _paint = null!;

    private static readonly string[] _doc =
    [
        "# 🚀 Getting Started with SkiaSharp",
        "",
        "## Overview",
        "",
        "SkiaSharp is a cross-platform 2D graphics API for .NET platforms based on Google's Skia library.",
        "It is the graphics engine behind Google Chrome, Flutter, Android, and many other products.",
        "The library exposes the full power of Skia through a clean and idiomatic .NET API.",
        "",
        "## Installation",
        "",
        "Add the NuGet packages to your project:",
        "",
        "```bash",
        "dotnet add package SkiaSharp",
        "dotnet add package SkiaSharp.HarfBuzz",
        "dotnet add package SkiaSharp.Views.Maui.Controls",
        "```",
        "",
        "## Drawing Text",
        "",
        "To draw text on a canvas use `SKCanvas.DrawText` with an `SKPaint` configured for text rendering.",
        "The font size, typeface, color, and text alignment are all set on the paint object before drawing.",
        "Always dispose the paint and canvas objects after use to avoid memory leaks and GC pressure.",
        "",
        "```csharp",
        "using var paint = new SKPaint { TextSize = 16f, Color = SKColors.Black, IsAntialias = true };",
        "paint.Typeface = SKTypeface.FromFamilyName(\"Arial\", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);",
        "canvas.DrawText(\"Hello, world! 🌍\", x, y, paint);",
        "```",
        "",
        "## Word Wrap and Measurement",
        "",
        "Word wrapping works by measuring candidate strings: \"word1\", \"word1 word2\", \"word1 word2 word3\".",
        "The same words appear over and over across lines so caching per-word measurements is very effective.",
        "Common English words like the, and, is, a, to, of, in, that, it, with, for, on, are repeat constantly.",
        "In a typical document the top 20 words by frequency account for roughly 25% of all tokens.",
        "",
        "## Performance Tips 🏎️",
        "",
        "- Reuse `SKPaint` objects — allocation and GC pressure is significant in hot render loops.",
        "- Cache glyph measurements — measuring the same word on every frame is pure waste.",
        "- Use `SKBitmap` for off-screen rendering and blit with `DrawBitmap` to avoid overdraw.",
        "- Avoid `string.Format` and LINQ in tight render paths — prefer `Span<char>` and direct loops.",
        "- Use `SKTypeface.FromFile` only once per font file and cache the result for the app lifetime.",
        "- Pre-warm the typeface cache at startup so the first frame does not stall on font loading.",
        "",
        "## Emoji and Bidirectional Text 😀🎉🔥",
        "",
        "Emoji require HarfBuzz shaping to render correctly. Enable shaping when text contains emoji codepoints.",
        "The shaping path is slower but necessary for correct cluster rendering of sequences like 🏳️‍🌈 and 👨‍👩‍👧‍👦.",
        "Right-to-left scripts such as Arabic and Hebrew also require shaping and bidi reordering.",
        "",
        "## Links and Further Reading",
        "",
        "See the official Microsoft docs at https://learn.microsoft.com/dotnet/api/skiasharp for the full API.",
        "The SkiaSharp source is on GitHub at https://github.com/mono/SkiaSharp — PRs are welcome.",
        "Join the discussion on GitHub Issues if you find a bug or have a feature request for the library.",
        "",
        "## Summary",
        "",
        "SkiaSharp is fast, flexible, and runs on iOS, Android, macOS, Windows, WebAssembly, and Linux.",
        "With per-word measurement caching the render pipeline handles long rich-text documents efficiently.",
        "The cache hit rate on real English prose is significantly higher than on artificially unique words.",
    ];

    [GlobalSetup]
    public void Setup()
    {
        _label = new BenchRichLabel();

        var dummySpan = new TextSpan { Text = "x", TypeFace = SKTypeface.Default };
        _label.Spans.Add(dummySpan);

        _paint = new SKPaint { Typeface = SKTypeface.Default, TextSize = 16f };
    }

    [GlobalCleanup]
    public void Cleanup() => _paint.Dispose();

    [Benchmark(Baseline = true, Description = "MeasureLineGlyphs/Markdown (committed)")]
    public float Committed()
    {
        float total = 0;
        foreach (var line in _doc)
            total += _label.MeasureLineGlyphs(_paint, line, false, 1f).Width;
        return total;
    }

    [Benchmark(Description = "MeasureLineGlyphsProbe/Markdown (probe/fast)")]
    public float Probe()
    {
        float total = 0;
        foreach (var line in _doc)
            total += _label.MeasureLineGlyphsProbe(_paint, line, false, 1f).Width;
        return total;
    }
}
