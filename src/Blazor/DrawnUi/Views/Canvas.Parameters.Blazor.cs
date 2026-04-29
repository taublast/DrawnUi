using Microsoft.AspNetCore.Components;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using System.Globalization;

namespace DrawnUi.Views;

public partial class Canvas
{
    [Parameter]
    public new string BackgroundColor { get; set; } = "#00000000";

    [Parameter]
    public new string Margin { get; set; } = string.Empty;

    private string BackgroundColorCss => string.IsNullOrWhiteSpace(BackgroundColor)
        ? "transparent"
        : BackgroundColor;

    private Color ParsedBackgroundColor => string.IsNullOrWhiteSpace(BackgroundColor)
        ? null
        : Color.FromSKColor(SKColor.Parse(BackgroundColor));

    private Thickness ParsedMargin => ParseThickness(Margin);

    private static Thickness ParseThickness(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Thickness.Zero;

        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var numbers = parts
            .Select(part => double.Parse(part, CultureInfo.InvariantCulture))
            .ToArray();

        return numbers.Length switch
        {
            1 => new Thickness(numbers[0]),
            2 => new Thickness(numbers[0], numbers[1]),
            4 => new Thickness(numbers[0], numbers[1], numbers[2], numbers[3]),
            _ => throw new FormatException($"Unsupported thickness value '{value}'.")
        };
    }
}