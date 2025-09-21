using AppoMobi.Specials;
using DrawnUi.Draw;
using SkiaSharp;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;
using static UnitTests.RenderingTests;
using SkiaLayout = DrawnUi.Draw.SkiaLayout;

namespace UnitTests
{
    public class SkiaLayoutTests : DrawnTestsBase
    {
        [Fact]
        public void ItemsSourceNotSet()
        {
            var layout = new SkiaLayout
            {
                Type = LayoutType.Column
            };

            ObservableCollection<int> itemsSource = null;
            layout.ItemsSource = itemsSource;
            layout.ItemTemplate = new DataTemplate(() => new SkiaControl());

            layout.CommitInvalidations();
            layout.Measure(100, 100, 1);

            Assert.True(LayoutStructureCorrespondsToItemsSource(itemsSource, layout));
        }

        [Fact]
        public void ItemsSourceEmpty()
        {
            var layout = new SkiaLayout
            {
                Type = LayoutType.Column
            };

            var itemsSource = new ObservableCollection<int>();
            layout.ItemsSource = itemsSource;
            layout.ItemTemplate = new DataTemplate(() => new SkiaControl());

            layout.CommitInvalidations();
            layout.Measure(100, 100, 1);

            Assert.True(LayoutStructureCorrespondsToItemsSource(itemsSource, layout));
        }

        [Fact]
        public void ItemsSourceNotEmpty()
        {
            var layout = new SkiaLayout
            {
                Type = LayoutType.Column
            };

            var itemsSource = new ObservableCollection<int>() { 1, 2, 3, 4, 5 };
            layout.ItemsSource = itemsSource;
            layout.ItemTemplate = new DataTemplate(() => new SkiaControl());

            layout.CommitInvalidations();
            layout.Measure(100, 100, 1);

            Assert.True(LayoutStructureCorrespondsToItemsSource(itemsSource, layout));
        }

        [Fact]
        public async Task NoLeaksDisposedLayoutWithChildren()
        {
            var layout = CreateSampleLayoutWIthChildren();
            var image = layout.FindView<SkiaImage>("Image");

            var layoutRef = new WeakReference(layout);
            var childRef = new WeakReference(image);

            var destination = new SKRect(0, 0, 100, 100);
            layout.CommitInvalidations();
            layout.Measure(destination.Width, destination.Height, 1);
            var picture = RenderWithOperationsContext(destination, (ctx) =>
            {
                layout.Render(ctx);
            });

            layout.Dispose();

            Assert.True(layout.IsDisposed);
            Assert.True(image.IsDisposing);

            await Task.Delay(10000);

            Assert.True(image.IsDisposed, "child failed to dispose in due time");

            image = null;
            layout = null;

            // First GC
            await Task.Yield();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.False(layoutRef.IsAlive, "layout should not be alive!");

            // Second GC
            //await Task.Yield();
            await Task.Delay(1500);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.False(childRef.IsAlive, "child should not be alive!");
        }

        [Fact]
        public void AbsoluteTypeRespectZIndex()
        {
            var layout = CreateAbsoluteLayoutSampleWIthChildren();

            var destination = new SKRect(0, 0, 100, float.PositiveInfinity);
            layout.CommitInvalidations();
            layout.Measure(destination.Width, destination.Height, 1);

            //prepare DrawingRect
            layout.Arrange(new SKRect(0, 0, layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height),
                layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height, 1);

            var picture = RenderWithOperationsContext(destination, (ctx) =>
            {
                layout.Render(ctx.WithDestination(layout.DrawingRect));
            });

            var cache = layout.RenderObject;
            var pixels = cache.Image.PeekPixels();
            var color = pixels.GetPixelColor(0, 0);

            Assert.Equal(color, SKColors.Red);
        }

        [Fact]
        public void ColumnTypeRespectZIndex()
        {
            var layout = new SkiaLayout
            {
                Type = LayoutType.Column,
                BackgroundColor = Colors.Black,
                Spacing = 0,
                UseCache = SkiaCacheType.Image,
                Children = new List<SkiaControl>()
                {
                    new SkiaShape()
                    {
                        ZIndex = 0,
                        Tag = "Green",
                        BackgroundColor = Colors.Green,
                        HeightRequest=100,
                        LockRatio=1,
                    },
                    new SkiaShape()
                    {
                        AddMarginTop=-100,
                        ZIndex = 1,
                        Tag = "Red",
                        BackgroundColor = Colors.Red,
                        HeightRequest=100,
                        LockRatio=1,
                    },
                    new SkiaShape()
                    {
                        //AddMarginTop=-200,
                        Tag = "Blue",
                        BackgroundColor = Colors.Blue,
                        HeightRequest=100,
                        LockRatio=-1,
                    },
                }
            };

            var destination = new SKRect(0, 0, 100, float.PositiveInfinity);
            layout.Measure(destination.Width, destination.Height, 1);

            //prepare DrawingRect
            layout.Arrange(new SKRect(0, 0, layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height),
                layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height, 1);

            var picture = RenderWithOperationsContext(destination, (ctx) =>
            {
                layout.Render(ctx.WithDestination(layout.DrawingRect));
            });

            var cache = layout.RenderObject;
            var pixels = cache.Image.PeekPixels();
            var color = pixels.GetPixelColor(0, 0);
            Assert.Equal(color, SKColors.Red);
        }

        [Fact]
        public void AbsoluteTypePaddingOk()
        {
            var layout = new SkiaLayout
            {
                Padding = new Thickness(16),
                Type = LayoutType.Absolute,
                WidthRequest = 100,
                Spacing = 0,
                UseCache = SkiaCacheType.Image,
                Children = new List<SkiaControl>()
                {
                    new SkiaShape()
                    {
                        ZIndex = 0,
                        Tag = "Green",
                        BackgroundColor = Colors.Green,
                        HeightRequest=100,
                        LockRatio=1,
                    },
                    new SkiaShape()
                    {
                        AddMarginTop=-100,
                        ZIndex = 1,
                        Tag = "Red",
                        BackgroundColor = Colors.Red,
                        HeightRequest=100,
                        LockRatio=1,
                    },
                    new SkiaShape()
                    {
                        //AddMarginTop=-200,
                        Tag = "Blue",
                        BackgroundColor = Colors.Blue,
                        HeightRequest=100,
                        LockRatio=-1,
                    },
                }
            };

            var destination = new SKRect(0, 0, 150, float.PositiveInfinity);
            layout.Measure(destination.Width, destination.Height, 1);

            //prepare DrawingRect
            layout.Arrange(new SKRect(0, 0, layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height),
                layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height, 1);

            var picture = RenderWithOperationsContext(destination, (ctx) =>
            {
                layout.Render(ctx.WithDestination(layout.DrawingRect));
            });

            var image = layout.RenderObject.Image;

            Assert.Equal(layout.DrawingRect.Width, 100);
        }

        [Fact]
        public void ColumnTypePaddingOk()
        {
            var layout = new SkiaLayout
            {
                Padding = new Thickness(16),
                Type = LayoutType.Column,
                WidthRequest = 100,
                Spacing = 0,
                UseCache = SkiaCacheType.Image,
                Children = new List<SkiaControl>()
                {
                    new SkiaShape()
                    {
                        ZIndex = 0,
                        Tag = "Green",
                        BackgroundColor = Colors.Green,
                        HeightRequest=100,
                        LockRatio=1,
                    },
                    new SkiaShape()
                    {
                        AddMarginTop=-100,
                        ZIndex = 1,
                        Tag = "Red",
                        BackgroundColor = Colors.Red,
                        HeightRequest=100,
                        LockRatio=1,
                    },
                    new SkiaShape()
                    {
                        //AddMarginTop=-200,
                        Tag = "Blue",
                        BackgroundColor = Colors.Blue,
                        HeightRequest=100,
                        LockRatio=-1,
                    },
                }
            };

            var destination = new SKRect(0, 0, 150, float.PositiveInfinity);
            layout.Measure(destination.Width, destination.Height, 1);

            //prepare DrawingRect
            layout.Arrange(new SKRect(0, 0, layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height),
                layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height, 1);

            var picture = RenderWithOperationsContext(destination, (ctx) =>
            {
                layout.Render(ctx.WithDestination(layout.DrawingRect));
            });

            var image = layout.RenderObject.Image;

            Assert.Equal(layout.DrawingRect.Width, 100);
        }

        [Fact]
        public void ColumnTypeMarginOk()
        {
            var layout = new SkiaLayout
            {
                BackgroundColor = Colors.Black,
                Type = LayoutType.Column,
                Margin = new Thickness(0),
                VerticalOptions = LayoutOptions.Fill,
                Spacing = 0,
                UseCache = SkiaCacheType.Image,
                Children = new List<SkiaControl>()
                {
                    new SkiaLabel()
                    {
                        BackgroundColor = Colors.Red,
                        WidthRequest = 100,
                        Tag="Label",
                        Text="Tests",
                    },
                }
            };

            var label = layout.FindViewByTag("Label");

            var destination = new SKRect(0, 0, 150, 150);
            layout.Measure(destination.Width, destination.Height, 1);

            //prepare DrawingRect
            layout.Arrange(new SKRect(0, 0, layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height),
                layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height, 1);

            var picture = RenderWithOperationsContext(destination, (ctx) =>
            {
                layout.Render(ctx.WithDestination(layout.DrawingRect));
            });

            var image = layout.RenderObject.Image;

            Assert.Equal(layout.DrawingRect.Width, 100);

            Assert.True(label.DrawingRect.Height > 0);

        }

        /*
        [Fact]
        public void ChildNotDrawnWhenOutOfBounds()
        {
            var layout = CreateSampleLayoutWIthChildren();
            var image = layout.FindView<SkiaImage>("Image");
            var label = layout.FindView<SkiaLabel>("Label");

            image.AddMarginTop = 110;

            var destination = new SKRect(0, 0, 100, 100);
            layout.Measure(destination.Width, destination.Height, 1);
            var picture = RenderWithOperationsContext(destination, (ctx) =>
            {
                layout.Render(ctx, destination, 1);
            });

            Assert.True(label.WasDrawn);
            Assert.False(image.WasDrawn);
        }
        */

        /// <summary>
        /// Check the generated structure to correspond to itemssource
        /// </summary>
        /// <param name="itemsSource"></param>
        /// <param name="layout"></param>
        /// <returns></returns>
        static bool LayoutStructureCorrespondsToItemsSource(IList itemsSource, SkiaLayout layout)
        {
            if (!layout.UsesRenderingTree)
            {
                throw new Exception("Incompatible layout, not using rendering tree");
            }

            if (itemsSource == null || itemsSource.Count == 0)
            {
                return layout.LatestStackStructure == null || layout.LatestStackStructure.GetChildren().Count() == 0;
            }

            if (layout.LatestStackStructure.GetChildren().Count() != itemsSource.Count())
                return false;


            var index = 0;
            foreach (var cell in layout.LatestStackStructure.GetChildren())
            {
                if (cell.ControlIndex != index)
                    return false;

                var item = itemsSource[cell.ControlIndex];

                index++;
            }

            return itemsSource.Count == index;
        }

        static SkiaLayout CreateSampleLayoutWIthChildren()
        {
            return new SkiaLayout
            {
                Children = new List<SkiaControl>()
                {
                    new SkiaLabel()
                    {
                        Tag="Label",
                        Text="Tests"
                    },
                    new SkiaShape()
                    {
                        Tag="Shape",
                        ZIndex = 1,
                        WidthRequest = 20,
                        LockRatio = 1
                    },
                    new SkiaImage()
                    {
                        Tag="Image",
                        WidthRequest = 50,
                        LockRatio = 1
                    },
                    new SkiaLabelFps()
                }
            };
        }

        static SkiaLayout CreateAbsoluteLayoutSampleWIthChildren()
        {
            return new SkiaLayout
            {
                BackgroundColor = Colors.Black,
                UseCache = SkiaCacheType.Image,
                Children = new List<SkiaControl>()
                {
                    new SkiaLabel()
                    {
                        Tag="Label",
                        Text="Tests",
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        ZIndex = 2,
                    },
                    new SkiaShape()
                    {
                        Tag="Shape",
                        ZIndex = 1,
                        BackgroundColor = Colors.Red,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill
                    },
                    new SkiaShape()
                    {
                        BackgroundColor = Colors.Yellow,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill
                    },
                }
            };
        }

        [Fact]
        public void MeasureVisibleWithSplitAndDynamicColumns()
        {
            var layout = new SkiaLayout
            {
                Type = LayoutType.Column,
                Split = 2,
                DynamicColumns = true,
                MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
                UseCache = SkiaCacheType.Image,
                WidthRequest = 200,
                HeightRequest = 300
            };

            // Create 5 items - should result in 3 rows: [2 items], [2 items], [1 item]
            var itemsSource = new ObservableCollection<int>() { 1, 2, 3, 4, 5 };
            layout.ItemsSource = itemsSource;
            layout.ItemTemplate = new DataTemplate(() => new SkiaShape()
            {
                BackgroundColor = Colors.Blue,
                HeightRequest = 50,
                WidthRequest = 90
            });

            layout.CommitInvalidations();
            layout.Measure(200, 300, 1);

            // Verify structure corresponds to items source
            Assert.True(LayoutStructureCorrespondsToItemsSource(itemsSource, layout));

            // Verify the structure has correct row/column distribution
            var structure = layout.LatestStackStructure;
            Assert.NotNull(structure);

            // Should have 3 rows
            Assert.Equal(3, structure.MaxRows);

            // First row should have 2 columns
            Assert.Equal(2, structure.GetColumnCountForRow(0));

            // Second row should have 2 columns
            Assert.Equal(2, structure.GetColumnCountForRow(1));

            // Third row should have 1 column (DynamicColumns = true)
            Assert.Equal(1, structure.GetColumnCountForRow(2));

            // Verify items are positioned correctly
            var cells = structure.GetChildren().OrderBy(c => c.ControlIndex).ToList();
            Assert.Equal(5, cells.Count);

            // Check row/column assignments
            Assert.Equal(0, cells[0].Row); // Item 1: Row 0, Col 0
            Assert.Equal(0, cells[0].Column);

            Assert.Equal(0, cells[1].Row); // Item 2: Row 0, Col 1
            Assert.Equal(1, cells[1].Column);

            Assert.Equal(1, cells[2].Row); // Item 3: Row 1, Col 0
            Assert.Equal(0, cells[2].Column);

            Assert.Equal(1, cells[3].Row); // Item 4: Row 1, Col 1
            Assert.Equal(1, cells[3].Column);

            Assert.Equal(2, cells[4].Row); // Item 5: Row 2, Col 0 (last row with 1 item)
            Assert.Equal(0, cells[4].Column);
        }

        [Fact]
        public void MeasureVisibleWithSplitNoDynamicColumns()
        {
            var layout = new SkiaLayout
            {
                Type = LayoutType.Column,
                Split = 2,
                DynamicColumns = false, // Force all rows to have Split columns
                MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
                UseCache = SkiaCacheType.Image,
                WidthRequest = 200,
                HeightRequest = 300
            };

            // Create 5 items - should result in 3 rows: [2 items], [2 items], [1 item but padded to 2 columns]
            var itemsSource = new ObservableCollection<int>() { 1, 2, 3, 4, 5 };
            layout.ItemsSource = itemsSource;
            layout.ItemTemplate = new DataTemplate(() => new SkiaShape()
            {
                BackgroundColor = Colors.Blue,
                HeightRequest = 50,
                WidthRequest = 90
            });

            layout.CommitInvalidations();
            layout.Measure(200, 300, 1);

            // Verify structure corresponds to items source
            Assert.True(LayoutStructureCorrespondsToItemsSource(itemsSource, layout));

            // Verify the structure has correct row/column distribution
            var structure = layout.LatestStackStructure;
            Assert.NotNull(structure);

            // Should have 3 rows
            Assert.Equal(3, structure.MaxRows);

            // All rows should have Split columns when DynamicColumns = false
            Assert.Equal(2, structure.GetColumnCountForRow(0));
            Assert.Equal(2, structure.GetColumnCountForRow(1));
            Assert.Equal(1, structure.GetColumnCountForRow(2)); // Only 1 item in last row
        }

        [Fact]
        public void MeasureVisibleYPositionConsistentAcrossColumns()
        {
            var layout = new SkiaLayout
            {
                Type = LayoutType.Column,
                Split = 3,
                DynamicColumns = false,
                MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
                UseCache = SkiaCacheType.Image,
                WidthRequest = 300,
                HeightRequest = 400
            };

            // Create 6 items - should result in 2 rows: [3 items], [3 items]
            var itemsSource = new ObservableCollection<int>() { 1, 2, 3, 4, 5, 6 };
            layout.ItemsSource = itemsSource;
            layout.ItemTemplate = new DataTemplate(() => new SkiaShape()
            {
                BackgroundColor = Colors.Blue,
                HeightRequest = 60,
                WidthRequest = 90
            });

            layout.CommitInvalidations();
            layout.Measure(300, 400, 1);

            // Verify structure corresponds to items source
            Assert.True(LayoutStructureCorrespondsToItemsSource(itemsSource, layout));

            // Verify the structure has correct row/column distribution
            var structure = layout.LatestStackStructure;
            Assert.NotNull(structure);

            // Should have 2 rows
            Assert.Equal(2, structure.MaxRows);

            // Both rows should have 3 columns
            Assert.Equal(3, structure.GetColumnCountForRow(0));
            Assert.Equal(3, structure.GetColumnCountForRow(1));

            // Verify Y positions are consistent within each row
            var cells = structure.GetChildren().OrderBy(c => c.ControlIndex).ToList();
            Assert.Equal(6, cells.Count);

            // Row 0: All items should have the same Y position
            var row0Items = cells.Where(c => c.Row == 0).ToList();
            Assert.Equal(3, row0Items.Count);
            var row0Y = row0Items[0].Destination.Top;
            Assert.All(row0Items, item => Assert.Equal(row0Y, item.Destination.Top));

            // Row 1: All items should have the same Y position (but different from row 0)
            var row1Items = cells.Where(c => c.Row == 1).ToList();
            Assert.Equal(3, row1Items.Count);
            var row1Y = row1Items[0].Destination.Top;
            Assert.All(row1Items, item => Assert.Equal(row1Y, item.Destination.Top));

            // Row 1 should be positioned below row 0
            Assert.True(row1Y > row0Y, $"Row 1 Y position ({row1Y}) should be greater than Row 0 Y position ({row0Y})");
        }

        [Fact]
        public void MeasureVisibleBackgroundMeasurementYPositionFix()
        {
            var layout = new SkiaLayout
            {
                Type = LayoutType.Column,
                Split = 2,
                DynamicColumns = false,
                MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
                UseCache = SkiaCacheType.Image,
                WidthRequest = 200,
                HeightRequest = 400,
                Spacing = 10
            };

            // Create 8 items - should result in 4 rows: [2 items], [2 items], [2 items], [2 items]
            var itemsSource = new ObservableCollection<int>() { 1, 2, 3, 4, 5, 6, 7, 8 };
            layout.ItemsSource = itemsSource;
            layout.ItemTemplate = new DataTemplate(() => new SkiaShape()
            {
                BackgroundColor = Colors.Blue,
                HeightRequest = 50,
                WidthRequest = 90
            });

            layout.CommitInvalidations();
            layout.Measure(200, 400, 1);

            // Verify structure corresponds to items source
            Assert.True(LayoutStructureCorrespondsToItemsSource(itemsSource, layout));

            var structure = layout.LatestStackStructure;
            Assert.NotNull(structure);

            // Should have 4 rows with 2 columns each
            Assert.Equal(4, structure.MaxRows);
            for (int row = 0; row < 4; row++)
            {
                Assert.Equal(2, structure.GetColumnCountForRow(row));
            }

            // Verify Y positions are consistent within each row
            var cells = structure.GetChildren().OrderBy(c => c.ControlIndex).ToList();
            Assert.Equal(8, cells.Count);

            // Test each row individually
            for (int row = 0; row < 4; row++)
            {
                var rowItems = cells.Where(c => c.Row == row).ToList();
                Assert.Equal(2, rowItems.Count);

                // Both items in the same row should have identical Y positions
                var firstItemY = rowItems[0].Destination.Top;
                var secondItemY = rowItems[1].Destination.Top;

                Assert.True(Math.Abs(firstItemY - secondItemY) < 0.01f,
                    $"Row {row}: Column 0 Y position ({firstItemY}) should equal Column 1 Y position ({secondItemY})");

                // Verify spacing between rows (except for first row)
                if (row > 0)
                {
                    var previousRowItems = cells.Where(c => c.Row == row - 1).ToList();
                    var previousRowBottom = previousRowItems.Max(c => c.Destination.Bottom);
                    var expectedY = previousRowBottom + 10; // spacing = 10

                    Assert.True(Math.Abs(expectedY - firstItemY) <= 1, // Allow 1 pixel tolerance
                        $"Row {row} Y position ({firstItemY}) should be previous row bottom ({previousRowBottom}) + spacing (10) = {expectedY}");
                }
            }
        }

        [Fact]
        public void MeasureVisibleBackgroundMeasurementWidthCalculationFix()
        {
            // Test that background measurement uses content width (excluding margins) like initial measurement
            var layout = new SkiaLayout
            {
                Type = LayoutType.Column,
                Split = 2,
                Spacing = 10,
                Margin = new Thickness(8, 0, 8, 0), // Add margins to test the fix
                MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
                ItemTemplate = new DataTemplate(() => new SkiaLabel
                {
                    Text = "Test",
                    HorizontalOptions = LayoutOptions.Fill, // Make it fill the available width
                    HeightRequest = 50
                })
            };

            var items = new List<string> { "Item1", "Item2", "Item3", "Item4", "Item5", "Item6" };
            layout.ItemsSource = items;

            // Measure the layout with specific width
            var totalWidth = 400f;
            var measured = layout.Measure(totalWidth, 600, 1.0f);
            Assert.True(measured.Pixels.Width > 0);
            Assert.True(measured.Pixels.Height > 0);

            // Get the structure
            var structure = layout.LatestStackStructure;
            Assert.NotNull(structure);

            // Calculate expected column width using content width (excluding margins)
            var scale = 1.0f;
            var marginsWidth = (layout.Margin.Left + layout.Margin.Right) * scale;
            var contentWidth = totalWidth - marginsWidth;
            var expectedColumnWidth = (contentWidth - (2 - 1) * layout.Spacing * scale) / 2;

            // Verify that all items have the correct width (should match expected column width)
            // Since we set HorizontalOptions.Fill, the items should fill the available column width
            for (int row = 0; row < structure.MaxRows; row++)
            {
                for (int col = 0; col < structure.MaxColumns; col++)
                {
                    var item = structure.Get(col, row);
                    if (item != null)
                    {
                        // The item width should match the calculated column width when using Fill
                        Assert.Equal(expectedColumnWidth, item.Destination.Width, 1); // Allow 1 pixel tolerance for rounding
                    }
                }
            }
        }

    }
}
