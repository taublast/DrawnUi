using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DrawnUi.Draw;
using DrawnUi.Views;
using Microsoft.AspNetCore.Components;
using SkiaSharp;

namespace Microsoft.Maui.ApplicationModel
{
    public static class MainThread
    {
        public static bool IsMainThread => true;

        public static void BeginInvokeOnMainThread(Action action)
        {
            action?.Invoke();
        }

        public static Task InvokeOnMainThreadAsync(Action action)
        {
            action?.Invoke();
            return Task.CompletedTask;
        }

        public static async Task InvokeOnMainThreadAsync(Func<Task> action)
        {
            if (action != null)
            {
                await action();
            }
        }
    }
}

namespace Microsoft.Maui
{
    public interface IElementHandler
    {
        void DisconnectHandler();
    }

    public interface IView
    {
        object Handler { get; }
    }
}

namespace Microsoft.Maui.Devices
{
    public enum DisplayRotation
    {
        Rotation0,
        Rotation90,
        Rotation180,
        Rotation270
    }

    public static class DevicePlatform
    {
        public const string Unknown = nameof(Unknown);
        public const string Android = nameof(Android);
        public const string iOS = nameof(iOS);
        public const string UWP = nameof(UWP);
        public const string WinUI = nameof(WinUI);
    }

    public static class DeviceInfo
    {
        public static string Platform { get; set; } = DevicePlatform.Unknown;
    }

    public readonly struct DisplayInfo
    {
        public DisplayInfo(double density, DisplayRotation rotation = DisplayRotation.Rotation0)
        {
            Density = density;
            Rotation = rotation;
        }

        public double Density { get; }

        public DisplayRotation Rotation { get; }
    }

    public sealed class DisplayInfoChangedEventArgs : EventArgs
    {
        public DisplayInfoChangedEventArgs(DisplayInfo displayInfo)
        {
            DisplayInfo = displayInfo;
        }

        public DisplayInfo DisplayInfo { get; }
    }

    public sealed class DeviceDisplay
    {
        private DeviceDisplay()
        {
        }

        public static DeviceDisplay Current { get; } = new();

        public static event EventHandler<DisplayInfoChangedEventArgs> MainDisplayInfoChanged;

        public DisplayInfo MainDisplayInfo { get; set; } = new(1.0);

        public static void RaiseMainDisplayInfoChanged(DisplayInfo displayInfo)
        {
            MainDisplayInfoChanged?.Invoke(Current, new DisplayInfoChangedEventArgs(displayInfo));
        }
    }
}

namespace Microsoft.Maui
{
    public readonly struct CornerRadius : IEquatable<CornerRadius>
    {
        public CornerRadius(double uniformRadius)
            : this(uniformRadius, uniformRadius, uniformRadius, uniformRadius)
        {
        }

        public CornerRadius(double topLeft, double topRight, double bottomLeft, double bottomRight)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomLeft = bottomLeft;
            BottomRight = bottomRight;
        }

        public double TopLeft { get; }

        public double TopRight { get; }

        public double BottomLeft { get; }

        public double BottomRight { get; }

        public bool Equals(CornerRadius other)
        {
            return TopLeft.Equals(other.TopLeft)
                && TopRight.Equals(other.TopRight)
                && BottomLeft.Equals(other.BottomLeft)
                && BottomRight.Equals(other.BottomRight);
        }

        public override bool Equals(object obj)
        {
            return obj is CornerRadius other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TopLeft, TopRight, BottomLeft, BottomRight);
        }

        public static implicit operator CornerRadius(double uniformRadius)
        {
            return new CornerRadius(uniformRadius);
        }

        public static bool operator ==(CornerRadius left, CornerRadius right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CornerRadius left, CornerRadius right)
        {
            return !left.Equals(right);
        }
    }
}

namespace Microsoft.Maui.Graphics
{
    public class Color : IEquatable<Color>
    {
        public Color(float red, float green, float blue, float alpha = 1f)
        {
            Red = Clamp(red);
            Green = Clamp(green);
            Blue = Clamp(blue);
            Alpha = Clamp(alpha);
        }

        public Color(double red, double green, double blue, double alpha = 1.0)
            : this((float)red, (float)green, (float)blue, (float)alpha)
        {
        }

        public float Red { get; }

        public float Green { get; }

        public float Blue { get; }

        public float Alpha { get; }

        public float A => Alpha;

        public SKColor ToSKColor()
        {
            return new SKColor(ToByte(Red), ToByte(Green), ToByte(Blue), ToByte(Alpha));
        }

        public Color WithAlpha(double alpha)
        {
            return new Color(Red, Green, Blue, (float)alpha);
        }

        public Color WithAlpha(float alpha)
        {
            return new Color(Red, Green, Blue, alpha);
        }

        public Color WithAlpha(byte alpha)
        {
            return new Color(Red, Green, Blue, alpha / 255f);
        }

        public static Color FromRgb(byte red, byte green, byte blue)
        {
            return new Color(red / 255f, green / 255f, blue / 255f, 1f);
        }

        public static Color FromHsla(float hue, float saturation, float lightness, float alpha = 1f)
        {
            var sk = SKColor.FromHsl(hue * 360f, saturation * 100f, lightness * 100f).WithAlpha(ToByte(alpha));
            return FromSKColor(sk);
        }

        public static Color FromSKColor(SKColor color)
        {
            return new Color(color.Red / 255f, color.Green / 255f, color.Blue / 255f, color.Alpha / 255f);
        }

        public bool Equals(Color other)
        {
            return other is not null
                && Red.Equals(other.Red)
                && Green.Equals(other.Green)
                && Blue.Equals(other.Blue)
                && Alpha.Equals(other.Alpha);
        }

        public override bool Equals(object obj)
        {
            return obj is Color other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Red, Green, Blue, Alpha);
        }

        public static implicit operator SKColor(Color color)
        {
            return color?.ToSKColor() ?? SKColors.Transparent;
        }

        public static implicit operator Color(SKColor color)
        {
            return FromSKColor(color);
        }

        private static float Clamp(float value)
        {
            return Math.Clamp(value, 0f, 1f);
        }

        private static byte ToByte(float value)
        {
            return (byte)Math.Clamp((int)Math.Round(value * 255f), 0, 255);
        }
    }

    public static class Colors
    {
        public static Color Transparent { get; } = Color.FromSKColor(SKColors.Transparent);

        public static Color White { get; } = Color.FromSKColor(SKColors.White);

        public static Color Black { get; } = Color.FromSKColor(SKColors.Black);

        public static Color Red { get; } = Color.FromSKColor(SKColors.Red);

        public static Color Green { get; } = Color.FromSKColor(SKColors.Green);

        public static Color GreenYellow { get; } = Color.FromSKColor(SKColors.GreenYellow);

        public static Color Gray { get; } = Color.FromSKColor(SKColors.Gray);

        public static Color Orange { get; } = Color.FromSKColor(SKColors.Orange);
    }

    public struct PointF
    {
        public PointF(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; set; }

        public float Y { get; set; }
    }

    public struct Rect
    {
        public Rect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double Left => X;

        public double Top => Y;

        public double Right => X + Width;

        public double Bottom => Y + Height;
    }
}

namespace Microsoft.Maui.Controls
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ContentPropertyAttribute : Attribute
    {
        public ContentPropertyAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public class Element : BindableObject
    {
        public Element Parent { get; internal set; }
    }

    public interface IContentView
    {
        Microsoft.Maui.IView PresentedContent { get; }
    }

    public class View : Element, Microsoft.Maui.IView
    {
        public object Handler { get; set; }

        public event EventHandler SizeChanged;

        public IList<object> Effects { get; } = new List<object>();

        public IList<object> Behaviors { get; } = new List<object>();

        public virtual bool IsVisible { get; set; } = true;

        public virtual bool InputTransparent { get; set; }

        public virtual bool IsClippedToBounds { get; set; }

        public virtual double WidthRequest { get; set; } = -1;

        public virtual double HeightRequest { get; set; } = -1;

        public virtual double Width { get; set; } = -1;

        public virtual double Height { get; set; } = -1;

        public virtual double TranslationX { get; set; }

        public virtual double TranslationY { get; set; }

        public virtual double X { get; set; }

        public virtual double Y { get; set; }

        public virtual int ZIndex { get; set; }

        public virtual double Opacity { get; set; } = 1.0;

        public virtual double Rotation { get; set; }

        public virtual double RotationX { get; set; }

        public virtual double RotationY { get; set; }

        public virtual double AnchorX { get; set; }

        public virtual double AnchorY { get; set; }

        public virtual double ScaleX { get; set; } = 1.0;

        public virtual double ScaleY { get; set; } = 1.0;

        public virtual double MaximumWidthRequest { get; set; } = -1;

        public virtual double MinimumWidthRequest { get; set; } = -1;

        public virtual double MaximumHeightRequest { get; set; } = -1;

        public virtual double MinimumHeightRequest { get; set; } = -1;

        public virtual Shadow Shadow { get; set; }

        public virtual Geometry Clip { get; set; }

        public virtual Brush Background { get; set; }

        public virtual Style Style { get; set; }

        public virtual Thickness Padding { get; set; }

        public virtual Thickness Margin { get; set; }

        public virtual LayoutOptions HorizontalOptions { get; set; } = LayoutOptions.Start;

        public virtual LayoutOptions VerticalOptions { get; set; } = LayoutOptions.Start;

        public virtual Microsoft.Maui.Graphics.Color BackgroundColor { get; set; } = Microsoft.Maui.Graphics.Colors.Transparent;

        public virtual void Update()
        {
        }

        protected void RaiseSizeChanged()
        {
            SizeChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual SizeRequest OnMeasure(double widthConstraint, double heightConstraint)
        {
            return new SizeRequest(new Size(widthConstraint, heightConstraint), new Size(0, 0));
        }

        protected virtual void OnParentSet()
        {
        }
    }

    public class Layout : View
    {
        public IList<Microsoft.Maui.IView> Children { get; } = new List<Microsoft.Maui.IView>();
    }

    public class ContentView : View, IContentView
    {
        [Parameter]
        public object Content { get; set; }

        public Microsoft.Maui.IView PresentedContent => Content as Microsoft.Maui.IView;
    }

    public class ContentPage : ContentView
    {
    }

    public class DataTemplate
    {
        public DataTemplate()
        {
        }

        public DataTemplate(Func<object> createContent)
        {
            CreateContent = createContent;
        }

        public Func<object> CreateContent { get; set; }
    }

    public abstract class DataTemplateSelector : DataTemplate
    {
        public virtual DataTemplate SelectTemplate(object item, BindableObject container)
        {
            return null;
        }
    }

    public readonly struct SizeRequest
    {
        public SizeRequest(Size request, Size minimum)
        {
            Request = request;
            Minimum = minimum;
        }

        public Size Request { get; }

        public Size Minimum { get; }

        public double Width => Request.Width;

        public double Height => Request.Height;
    }

    public sealed class VisualTreeChangeEventArgs : EventArgs
    {
        public Element Parent { get; init; }

        public Element Child { get; init; }

        public int ChildIndex { get; init; }
    }

    public class Style
    {
    }

    public class Brush
    {
    }

    public class Shadow
    {
        public Brush Brush { get; set; }

        public SkiaShadow FromPlatform()
        {
            return null;
        }
    }

    public class Geometry
    {
        public void FromPlatform(SKPath path, SKRect destination, float renderingScale)
        {
        }
    }

    public class GradientBrush : Brush
    {
        public IList<GradientStop> GradientStops { get; } = new List<GradientStop>();
    }

    public class SolidColorBrush : Brush
    {
        public SolidColorBrush()
        {
        }

        public SolidColorBrush(Microsoft.Maui.Graphics.Color color)
        {
            Color = color;
        }

        public Microsoft.Maui.Graphics.Color Color { get; set; }
    }

    public class GradientStop
    {
        public GradientStop()
        {
        }

        public GradientStop(Microsoft.Maui.Graphics.Color color, float offset)
        {
            Color = color;
            Offset = offset;
        }

        public Microsoft.Maui.Graphics.Color Color { get; set; }

        public float Offset { get; set; }
    }

    public class LinearGradientBrush : GradientBrush
    {
        public Microsoft.Maui.Graphics.PointF StartPoint { get; set; }

        public Microsoft.Maui.Graphics.PointF EndPoint { get; set; }
    }

    public class RadialGradientBrush : GradientBrush
    {
        public Microsoft.Maui.Graphics.PointF Center { get; set; }
    }

    public enum GridUnitType
    {
        Absolute,
        Auto,
        Star
    }

    public readonly struct GridLength
    {
        public GridLength(double value, GridUnitType gridUnitType = GridUnitType.Absolute)
        {
            Value = value;
            GridUnitType = gridUnitType;
        }

        public double Value { get; }

        public GridUnitType GridUnitType { get; }

        public bool IsAbsolute => GridUnitType == GridUnitType.Absolute;

        public bool IsAuto => GridUnitType == GridUnitType.Auto;

        public bool IsStar => GridUnitType == GridUnitType.Star;

        public static GridLength Auto => new(1, GridUnitType.Auto);

        public static GridLength Star => new(1, GridUnitType.Star);
    }

    public interface IGridColumnDefinition
    {
        GridLength Width { get; }
    }

    public interface IGridRowDefinition
    {
        GridLength Height { get; }
    }

    public class ColumnDefinition : IGridColumnDefinition
    {
        public GridLength Width { get; set; } = GridLength.Star;
    }

    public class RowDefinition : IGridRowDefinition
    {
        public GridLength Height { get; set; } = GridLength.Star;
    }

    public class Easing
    {
        private readonly Func<double, double> _ease;

        public Easing(Func<double, double> ease)
        {
            _ease = ease ?? throw new ArgumentNullException(nameof(ease));
        }

        public double Ease(double value)
        {
            return _ease(value);
        }

        public static Easing Linear { get; } = new(value => value);

        public static Easing CubicIn { get; } = new(value => value * value * value);
    }
}

namespace AppoMobi.Maui.Gestures
{
    public enum TouchActionType
    {
        Entered,
        Pressed,
        Pressing,
        Moved,
        Rotated,
        Released,
        Exited,
        Cancelled,
        PanStarted,
        PanChanged,
        PanEnded,
        Wheel,
        Pointer
    }

    public enum TouchActionResult
    {
        Touch,
        Down,
        Up,
        Tapped,
        LongPressing,
        Panning,
        Wheel,
        Pointer
    }

    public enum PointerDeviceType
    {
        Touch,
        Mouse,
        Pen
    }

    public enum MouseButton
    {
        Left,
        Right,
        Middle,
        XButton1,
        XButton2,
        XButton3,
        XButton4,
        XButton5,
        XButton6,
        XButton7,
        XButton8,
        XButton9,
        Extended
    }

    [Flags]
    public enum MouseButtons
    {
        None = 0,
        Left = 1,
        Right = 2,
        Middle = 4,
        XButton1 = 8,
        XButton2 = 16,
        XButton3 = 32,
        XButton4 = 64,
        XButton5 = 128,
        XButton6 = 256,
        XButton7 = 512,
        XButton8 = 1024,
        XButton9 = 2048
    }

    public enum MouseButtonState
    {
        Pressed,
        Released
    }

    public enum TouchHandlingStyle
    {
        Default,
        Lock,
        Manual,
        Disabled
    }

    public interface IGestureListener
    {
        public void OnGestureEvent(
            TouchActionType type,
            TouchActionEventArgs args,
            TouchActionResult action);

        public bool InputTransparent { get; }
    }

    public delegate void TouchActionEventHandler(object sender, TouchActionEventArgs args);

    public static class PointFExtensions
    {
        public static PointF Add(this PointF lhs, PointF rhs)
        {
            return new PointF(lhs.X + rhs.X, lhs.Y + rhs.Y);
        }

        public static PointF Subtract(this PointF lhs, PointF rhs)
        {
            return new PointF(lhs.X - rhs.X, lhs.Y - rhs.Y);
        }
    }

    public class TouchActionEventArgs : EventArgs
    {
        public float DeltaTimeMs { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        public static void FillDistanceInfo(TouchActionEventArgs current, TouchActionEventArgs previous)
        {
            if (previous == null)
            {
                current.Distance = new DistanceInfo();
                return;
            }

            current.StartingLocation = previous.StartingLocation;
            current.IsInContact = previous.IsInContact;
            current.DeltaTimeMs = (float)(current.Timestamp - previous.Timestamp).TotalMilliseconds;

            var distance = new DistanceInfo
            {
                Start = previous.Location,
                End = current.Location,
                Delta = current.Location.Subtract(previous.Location)
            };

            if (current.Type == TouchActionType.Released || current.Type == TouchActionType.Cancelled ||
                current.Type == TouchActionType.Exited)
            {
                distance = new DistanceInfo
                {
                    Start = previous.Location,
                    End = previous.Location,
                    Delta = new PointF(0, 0)
                };
            }

            distance.Total = previous.Distance.Total.Add(distance.Delta);
            current.Distance = distance;
            current.Distance.Velocity = GetVelocity(current, previous);
            distance.TotalVelocity = previous.Distance.TotalVelocity.Add(current.Distance.Velocity);
        }

        public static PointF GetVelocity(TouchActionEventArgs current, TouchActionEventArgs previous)
        {
            const float velocityEpsilon = 0.001f;
            var velocity = new PointF(0, 0);

            if (previous != null)
            {
                PointF deltaDistance;
                float deltaSeconds;

                if (current.Distance.Delta.X == 0 && current.Distance.Delta.Y == 0 &&
                    (current.Type == TouchActionType.Released || current.Type == TouchActionType.Cancelled ||
                     current.Type == TouchActionType.Exited))
                {
                    var prevDeltaSecondsX = Math.Abs(previous.Distance.Velocity.X) > velocityEpsilon
                        ? previous.Distance.Delta.X / previous.Distance.Velocity.X
                        : 0;
                    var prevDeltaSecondsY = Math.Abs(previous.Distance.Velocity.Y) > velocityEpsilon
                        ? previous.Distance.Delta.Y / previous.Distance.Velocity.Y
                        : 0;
                    var prevDeltaSeconds = !float.IsNaN(prevDeltaSecondsX) && !float.IsInfinity(prevDeltaSecondsX)
                        ? prevDeltaSecondsX
                        : prevDeltaSecondsY;

                    deltaDistance = new PointF(previous.Distance.Delta.X, previous.Distance.Delta.Y);
                    deltaSeconds = (float)((current.Timestamp - previous.Timestamp).TotalSeconds + prevDeltaSeconds);
                    if (deltaSeconds > 0)
                    {
                        velocity = new PointF(deltaDistance.X / deltaSeconds, deltaDistance.Y / deltaSeconds);
                    }
                }
                else
                {
                    deltaDistance = new PointF(current.Distance.Delta.X, current.Distance.Delta.Y);
                    deltaSeconds = (float)(current.Timestamp - previous.Timestamp).TotalSeconds;
                    if (deltaSeconds > 0)
                    {
                        velocity = new PointF(deltaDistance.X / deltaSeconds, deltaDistance.Y / deltaSeconds);
                    }
                }

                if (float.IsNaN(velocity.X) || float.IsInfinity(velocity.X))
                    velocity.X = 0;
                if (float.IsNaN(velocity.Y) || float.IsInfinity(velocity.Y))
                    velocity.Y = 0;
            }

            return velocity;
        }

        public TouchActionEventArgs(long id, TouchActionType type, PointF location, object elementBindingContext = null)
        {
            Id = id;
            Type = type;
            Location = location;
            Context = elementBindingContext;
            Distance = new DistanceInfo();
        }

        public TouchActionEventArgs()
        {
            Distance = new DistanceInfo();
        }

        public long Id { get; private set; }

        public bool PreventDefault { get; set; }

        public TouchActionType Type { get; private set; }

        public PointF Location { get; set; }

        public PointF StartingLocation { get; set; }

        public bool IsInContact { get; set; }

        public bool IsInsideView { get; set; }

        public object Context { get; set; }

        public bool Handled { get; set; }

        public int NumberOfTouches { get; set; }

        public DrawnUi.Draw.TouchEffect.WheelEventArgs Wheel { get; set; }

        public DrawnUi.Draw.TouchEffect.PointerData Pointer { get; set; }

        public DistanceInfo Distance { get; set; }

        public ManipulationInfo Manipulation { get; set; }

        public record ManipulationInfo(
            PointF Center,
            PointF PreviousCenter,
            double Scale,
            double Rotation,
            double ScaleTotal,
            double RotationTotal,
            int TouchesCount);

        public record DistanceInfo
        {
            public PointF Delta { get; set; }

            public PointF Total { get; set; }

            public PointF TotalVelocity { get; set; }

            public PointF Velocity { get; set; }

            public PointF Start { get; set; }

            public PointF End { get; set; }
        }
    }
}
