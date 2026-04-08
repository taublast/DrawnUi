using DrawnUi.Draw;
using SkiaSharp;
using System.Collections.Generic;
using Xunit;

namespace UnitTests;

public class SkiaGradientTests
{
    [Fact]
    public void CompareTo_UsesVisualStateOnly()
    {
        var first = CreateGradient();
        var second = CreateGradient();

        first.Light = 0.25;
        first.Light = 0.75;

        Assert.Equal(0, first.CompareTo(second));
        Assert.True(first.Equals(second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void CompareTo_IgnoresAngleWhenPointsMatch()
    {
        var angled = CreateGradient();
        angled.Angle = 45;

        var manual = CreateGradient();
        var points = SkiaGradient.LinearGradientAngleToPoints(45);
        manual.StartXRatio = (float)points.X1;
        manual.StartYRatio = (float)points.Y1;
        manual.EndXRatio = (float)points.X2;
        manual.EndYRatio = (float)points.Y2;

        Assert.Equal(0, angled.CompareTo(manual));
        Assert.True(angled == manual);
    }

    [Fact]
    public void CompareTo_DetectsVisualDifference()
    {
        var first = CreateGradient();
        var second = CreateGradient();
        second.Colors = new List<Color> { Colors.Red, Colors.Blue };

        Assert.NotEqual(0, first.CompareTo(second));
        Assert.False(first.Equals(second));
        Assert.True(first != second);
    }

    [Fact]
    public void CompareTo_ObjectOfDifferentType_ReturnsZeroButNotEqual()
    {
        var gradient = CreateGradient();

        Assert.Equal(0, gradient.CompareTo(new object()));
        Assert.False(gradient.Equals(new object()));
    }

    private static SkiaGradient CreateGradient()
    {
        return new SkiaGradient
        {
            Type = GradientType.Linear,
            BlendMode = SKBlendMode.SrcOver,
            TileMode = SKShaderTileMode.Clamp,
            Light = 0.75,
            Opacity = 0.8f,
            StartXRatio = 0.1f,
            StartYRatio = 0.2f,
            EndXRatio = 0.9f,
            EndYRatio = 0.8f,
            Colors = new List<Color> { Colors.Red, Colors.Green },
            ColorPositions = new List<double> { 0.0, 1.0 }
        };
    }
}