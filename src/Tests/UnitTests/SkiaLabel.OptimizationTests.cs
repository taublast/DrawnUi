using System;
using System.Diagnostics;
using DrawnUi.Draw;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Tests
{
    /// <summary>
    /// Tests to validate that the SkiaLabel optimizations work correctly
    /// and don't break existing functionality
    /// </summary>
    public class SkiaLabelOptimizationTests
    {
        [Fact]
        public void MeasurePartialTextWidth_WithOptimizations_ProducesSameResults()
        {
            // Arrange
            var label = new SkiaLabel();
            using var paint = new SKPaint
            {
                TextSize = 16,
                Typeface = SKTypeface.Default
            };
            
            var testTexts = new[]
            {
                "Hello World",
                "Test123",
                "A",
                "This is a longer text to test",
                "🙂 Emoji test",
                ""
            };

            // Act & Assert
            foreach (var text in testTexts)
            {
                var span = text.AsSpan();
                
                // The optimized method should produce the same result as direct measurement
                var optimizedResult = label.MeasurePartialTextWidthOptimized(paint, span, false, 1.0f);
                var directResult = paint.MeasureText(text);
                
                // Allow small floating point differences
                Assert.True(Math.Abs(optimizedResult - directResult) < 0.1f, 
                    $"Optimized measurement differs from direct measurement for text '{text}'. " +
                    $"Optimized: {optimizedResult}, Direct: {directResult}");
            }
        }

        [Fact]
        public void ObjectPools_GetAndReturn_WorkCorrectly()
        {
            // Arrange & Act
            var list1 = SkiaLabel.ObjectPools.GetLineGlyphList();
            var list2 = SkiaLabel.ObjectPools.GetLineGlyphList();
            var sb1 = SkiaLabel.ObjectPools.GetStringBuilder();
            var sb2 = SkiaLabel.ObjectPools.GetStringBuilder();

            // Assert - should get different instances
            Assert.NotSame(list1, list2);
            Assert.NotSame(sb1, sb2);
            
            // Verify they're clean
            Assert.Empty(list1);
            Assert.Empty(list2);
            Assert.Equal(0, sb1.Length);
            Assert.Equal(0, sb2.Length);

            // Add some data
            list1.Add(new LineGlyph { Width = 10 });
            sb1.Append("test");

            // Return to pool
            SkiaLabel.ObjectPools.ReturnLineGlyphList(list1);
            SkiaLabel.ObjectPools.ReturnStringBuilder(sb1);

            // Get again - should be clean
            var list3 = SkiaLabel.ObjectPools.GetLineGlyphList();
            var sb3 = SkiaLabel.ObjectPools.GetStringBuilder();

            Assert.Empty(list3);
            Assert.Equal(0, sb3.Length);

            // Cleanup
            SkiaLabel.ObjectPools.ReturnLineGlyphList(list2);
            SkiaLabel.ObjectPools.ReturnLineGlyphList(list3);
            SkiaLabel.ObjectPools.ReturnStringBuilder(sb2);
            SkiaLabel.ObjectPools.ReturnStringBuilder(sb3);
        }

        [Fact]
        public void SpanMeasurement_MeasureTextWidthWithAdvanceSpan_MatchesStringVersion()
        {
            // Arrange
            using var paint = new SKPaint
            {
                TextSize = 16,
                Typeface = SKTypeface.Default
            };

            var testTexts = new[]
            {
                "Hello",
                "World",
                "123",
                "Test with spaces",
                "🙂"
            };

            // Act & Assert
            foreach (var text in testTexts)
            {
                var spanResult = SkiaLabel.SpanMeasurement.MeasureTextWidthWithAdvanceSpan(paint, text.AsSpan());
                var stringResult = paint.MeasureText(text);

                Assert.Equal(stringResult, spanResult, 3); // Allow small floating point differences
            }
        }

        [Fact]
        public void PooledStringBuilder_AutomaticReturn_WorksCorrectly()
        {
            // Arrange
            var initialPoolSize = SkiaLabel.ObjectPools.GetPoolSizes().StringBuilders;

            // Act
            using (var pooled = SkiaLabel.PooledStringBuilder.Get())
            {
                pooled.StringBuilder.Append("test");
                Assert.Equal("test", pooled.StringBuilder.ToString());
            } // StringBuilder should be returned to pool here

            // Assert
            var finalPoolSize = SkiaLabel.ObjectPools.GetPoolSizes().StringBuilders;
            Assert.True(finalPoolSize >= initialPoolSize, "StringBuilder should have been returned to pool");
        }

        [Fact]
        public void LastNonSpaceIndexOptimized_MatchesOriginal()
        {
            // Arrange
            var testTexts = new[]
            {
                "Hello World",
                "Test   ",
                "   Leading",
                "NoSpaces",
                "   ",
                "",
                "A"
            };

            // Act & Assert
            foreach (var text in testTexts)
            {
                var optimizedResult = SkiaLabel.LastNonSpaceIndexOptimized(text.AsSpan());
                var originalResult = SkiaLabel.LastNonSpaceIndex(text);

                Assert.Equal(originalResult, optimizedResult);
            }
        }

        [Fact]
        public void TextMeasurement_WithPooling_ProducesConsistentResults()
        {
            // Arrange
            var label = new SkiaLabel
            {
                Text = "Test text for measurement consistency",
                FontSize = 16
            };

            // Act - Measure multiple times to test pooling consistency
            var results = new float[10];
            for (int i = 0; i < results.Length; i++)
            {
                var measured = label.Measure(300, 100, 1.0f);
                results[i] = measured.Pixels.Width;
            }

            // Assert - All measurements should be identical
            for (int i = 1; i < results.Length; i++)
            {
                Assert.Equal(results[0], results[i], 3);
            }
        }
    }
}
