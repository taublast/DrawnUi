using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PreviewTests
{
    public  class CustomTabBar : SkiaControl
    {
        private int _selectedIndex = 0;
        private int _tabCount = 5; // Example number of tabs
        private float _tabWidth;
        private float _height;
        private float _indicatorRadius = 10; // Size of the circle indicator

        public CustomTabBar()
        {
            _tabCount = 4;
        }

        public void SetSelectedIndex(int index)
        {
            _selectedIndex = index;
        }

        protected override void Paint(DrawingContext ctx)
        {
            base.Paint(ctx);

            if (LayoutReady)
            {
                _height = DrawingRect.Height;
                _tabWidth = DrawingRect.Width / _tabCount;
            }

            // Draw tab bar
            DrawTabBar(ctx.Context.Canvas, _tabCount, _selectedIndex, DrawingRect);

            // Now draw circle indicators
            using var paint = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Fill };
            for (int i = 0; i < _tabCount; i++)
            {
                float x = (_tabWidth * i) + (_tabWidth / 2);
                float y = _height / 2;
                ctx.Context.Canvas.DrawCircle(x, y, _indicatorRadius, paint);

                if (i == _selectedIndex)
                {
                    paint.Color = SKColors.Red;
                    ctx.Context.Canvas.DrawCircle(x, y, _indicatorRadius, paint);
                    paint.Color = SKColors.Blue;
                }
            }
        }

        // Keep the same DrawTabBar method, but remove the path creation in Paint
        public void DrawTabBar(SKCanvas canvas, int tabCount, int selectedIndex, SKRect destination)
        {
            float tabWidth = destination.Width / tabCount;
            var path = new SKPath();
            path.MoveTo(destination.Left, destination.Top);

            for (int i = 0; i < tabCount; i++)
            {
                float startX = destination.Left + i * tabWidth;
                float endX = startX + tabWidth;
                float cx = startX + (tabWidth / 2);

                if (i == selectedIndex)
                {
                    // Use a smaller dip if you want a subtle shape
                    float dipDepth = 30;
                    path.QuadTo(cx, destination.Top, cx, destination.Top + dipDepth);
                    path.QuadTo(cx, destination.Top, endX, destination.Top);
                }
                else
                {
                    path.LineTo(endX, destination.Top);
                }
            }

            path.LineTo(destination.Right, destination.Bottom);
            path.LineTo(destination.Left, destination.Bottom);
            path.Close();

            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColors.Gray
            };
            canvas.DrawPath(path, paint);
        }



        public SKPath CreateTabBarPath(int tabCount, int selectedIndex, SKRect dest)
        {
            float tabWidth = dest.Width / tabCount;

            var path = new SKPath();
            path.MoveTo(dest.Left, dest.Top); // Start at top-left

            for (int i = 0; i < tabCount; i++)
            {
                float startX = dest.Left + i * tabWidth;
                float endX = startX + tabWidth;
                float cx = startX + (tabWidth / 2);

                if (i == selectedIndex)
                {
                    // A "U" dip from top -> some depth -> back up
                    float dipDepth = 30; // how deep you want the dip
                    // First curve: from top to a lower point
                    path.QuadTo(cx, dest.Top, cx, dest.Top + dipDepth);
                    // Second curve: from that dip back up
                    path.QuadTo(cx, dest.Top, endX, dest.Top);
                }
                else
                {
                    // Straight line across top
                    path.LineTo(endX, dest.Top);
                }
            }

            // Close off the bottom
            path.LineTo(dest.Right, dest.Bottom);
            path.LineTo(dest.Left, dest.Bottom);
            path.Close();

            return path;
        }

 
    }
}
