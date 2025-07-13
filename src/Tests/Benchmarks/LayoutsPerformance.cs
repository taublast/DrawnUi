using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using DrawnUi.Draw;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ILayout = Microsoft.Maui.ILayout;

namespace SomeBenchmarks
{
    public class CustomBenchmarkConfig : ManualConfig
    {
        public CustomBenchmarkConfig()
        {
            AddJob(Job.Default
                .WithWarmupCount(2)
                .WithIterationCount(10));
            //.WithMinIterationCount(4)
            //.WithMaxIterationCount(10));
        }
    }


    //[MemoryDiagnoser]
    [Config(typeof(CustomBenchmarkConfig))]
    public class LayoutsPerformance
    {

        private List<SkiaControl> drawnUi;
        private List<VisualElement> maui;

        const int Iterations = 16;

        [GlobalSetup]
        public void Setup()
        {

            maui = new();
            drawnUi = new();

            for (int i = 0; i < Iterations; i++)
            {
                maui.Add(new Border());
                drawnUi.Add(new SkiaShape());
            }

            //skiaLayout = new();
            //simpleLayout = new();
            //mauiLayout = new();
        }

        /// <summary>
        /// Not really relevant because not creating handlers for every view
        /// </summary>
        [Benchmark]
        public void CreateMauiStackLayoutWithChildren()
        {
            var layout = new StackLayout();

            for (int i = 0; i < Iterations; i++)
            {
                var view = maui[i];
                layout.Add(view);
                layout.GetLayoutHandlerIndex(view);
            }
        }
         
        [Benchmark]
        public void CreateDrawnUiLayout()
        {
            var layout = new SkiaLayout();

            for (int i = 0; i < Iterations; i++)
            {
                var view = drawnUi[i];
                layout.AddSubView(view);
                layout.GetLayoutHandlerIndexToDo(view);
            }
        }

        [Benchmark]
        public void CreateLayoutAbsoluteWrapperWithChildren()
        {
            var layout = new SkiaLayer();
            
            for (int i = 0; i < Iterations; i++)
            {
                var view = drawnUi[i];
                layout.AddSubView(view);
                layout.GetLayoutHandlerIndexToDo(view);
            }
        }

        [Benchmark]
        public void CreateLayoutAbsoluteWithChildren()
        {
            var layout = new SkiaLayout()
            {
                Type = LayoutType.Absolute
            };

            for (int i = 0; i < Iterations; i++)
            {
                var view = drawnUi[i];
                layout.AddSubView(view);
                layout.GetLayoutHandlerIndexToDo(view);
            }
        }

        [Benchmark]
        public void CreateLayoutWrapWithChildren()
        {
            var layout = new SkiaLayout()
            {
                Type = LayoutType.Wrap
            };

            for (int i = 0; i < Iterations; i++)
            {
                var view = drawnUi[i];
                layout.AddSubView(view);
                layout.GetLayoutHandlerIndexToDo(view);
            }
        }

        [Benchmark]
        public void CreateLayoutColumnWithChildren()
        {
            var layout = new SkiaLayout()
            {
                Type = LayoutType.Column
            };

            for (int i = 0; i < Iterations; i++)
            {
                var view = drawnUi[i];
                layout.AddSubView(view);
                layout.GetLayoutHandlerIndexToDo(view);
            }
        }

        [Benchmark]
        public void CreateLayoutRowWithChildren()
        {
            var layout = new SkiaLayout()
            {
                Type = LayoutType.Row
            };

            for (int i = 0; i < Iterations; i++)
            {
                var view = drawnUi[i];
                layout.AddSubView(view);
                layout.GetLayoutHandlerIndexToDo(view);
            }
        }

        //[Benchmark]
        //public void CreateSimpleLayout()
        //{
        //    for (int i = 0; i < Iterations; i++)
        //    {
        //        var view = new ContentLayout();
        //    }
        //}

        //[Benchmark]
        //public void CreateDrawnUiLayout()
        //{
        //    for (int i = 0; i < Iterations; i++)
        //    {
        //        var view = new SkiaLayout();
        //    }
        //}

        //[Benchmark]
        //public void CreateDrawnUiLayoutStack()
        //{
        //    for (int i = 0; i < Iterations; i++)
        //    {
        //        var view = new SkiaLayout()
        //        {
        //            Type = LayoutType.Stack
        //        };
        //    }
        //}


    }

    internal static class LayoutExtensions
    {
        public static IOrderedEnumerable<IView> OrderByZIndex(this ILayout layout) => layout.OrderBy(v => v.ZIndex);

        public static int GetLayoutHandlerIndex(this ILayout layout, IView view)
        {
            var count = layout.Count;
            switch (count)
            {
            case 0:
            return -1;
            case 1:
            return view == layout[0] ? 0 : -1;
            default:
            var found = false;
            var zIndex = view.ZIndex;
            var lowerViews = 0;

            for (int i = 0; i < count; i++)
            {
                var child = layout[i];
                var childZIndex = child.ZIndex;

                if (child == view)
                {
                    found = true;
                }

                if (childZIndex < zIndex || !found && childZIndex == zIndex)
                {
                    ++lowerViews;
                }
            }

            return found ? lowerViews : -1;
            }
        }

        //public static IOrderedEnumerable<IView> OrderByZIndex(this ISkiaLayout layout) => layout.OrderBy(v => v.ZIndex);

        //public static int GetLayoutHandlerIndex(this ISkiaLayout layout, ISkiaControl view)
        //{
        //    var count = layout.Count;
        //    switch (count)
        //    {
        //    case 0:
        //    return -1;
        //    case 1:
        //    return view == layout[0] ? 0 : -1;
        //    default:
        //    var found = false;
        //    var zIndex = view.ZIndex;
        //    var lowerViews = 0;

        //    for (int i = 0; i < count; i++)
        //    {
        //        var child = layout[i];
        //        var childZIndex = child.ZIndex;

        //        if (child == view)
        //        {
        //            found = true;
        //        }

        //        if (childZIndex < zIndex || !found && childZIndex == zIndex)
        //        {
        //            ++lowerViews;
        //        }
        //    }

        //    return found ? lowerViews : -1;
        //    }
        //}

        //public static int GetLayoutHandlerIndexFast(this SkiaLayout layout, ISkiaControl view)
        //{
        //    var views = layout.GetOrderedSubviews();
        //    foreach (var child in layout.ChildrenFactory.GetViewsIterator())
        //    {
        //        if (child == view)
        //        {
        //            return child.ZIndex;
        //        }
        //    }
        //    return -1;
        //}

        public static int GetLayoutHandlerIndexSmart(this SkiaLayout layout, ISkiaControl view)
        {
            var views = layout.ChildrenFactory.GetViewsIterator();
            //var asSpans = CollectionsMarshal.AsSpan(views);
            foreach (var child in views)
            {
                if (child == view)
                {
                    return child.ZIndex;
                }
            }
            return -1;
        }

        public static int GetLayoutHandlerIndexToDo(this SkiaControl layout, ISkiaControl view)
        {
            var views = layout.Views;
            var asSpans = CollectionsMarshal.AsSpan(views);
            foreach (var child in asSpans)
            {
                if (child == view)
                {
                    return child.ZIndex;
                }
            }
            return -1;
        }

    }
}
