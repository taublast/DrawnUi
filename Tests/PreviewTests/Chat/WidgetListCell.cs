using System.Diagnostics;
using System.Windows.Input;
using AppoMobi.Maui.Gestures;
using DrawnUi.Extensions;
using Newtonsoft.Json;
using PreviewTests;


namespace AppoMobi.Forms.Controls
{
    public enum ChatMetaType
    {
        Default,
        Image,
        Video,
        Article,
        File,
        Separator,
        System
    }

    public class WidgetListCell : SkiaLayout
    {
        public static SkiaImage CreateStandartAvatar(ISkiaControl parent, double scale)
        {
            return new SkiaImage()
            {
                Tag = "avatar",
                BackgroundColor = App.Current.Resources.Get<Color>("ColorPrimaryLight"),
                Margin = new Thickness(2),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                IsClippedToBounds = true,
                Clipping = (path, dest) =>
                {
                    //avatar circle
                    path.AddCircle(dest.Left + dest.Width / 2, dest.Top + dest.Height / 2,
                        dest.Width / 2);
                }
            }.WithParent(parent);
        }

        public static SkiaSvg CreateStandartEmptyAvatar(ISkiaControl parent, double scale)
        {
            return new SkiaSvg()
            {
                Tag = "EmptyAvatar",
                IsVisible = false,
                TranslationY = -2,
                IsClippedToBounds = true,
                SvgString = App.Current.Resources.Get<string>("SvgAvatarEmpty"),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                HeightRequest = 32,
                WidthRequest = 32
            }.WithParent(parent);
        }

        public static bool DisableShadows => false;

        public static readonly BindableProperty CommandTappedProperty = BindableProperty.Create(nameof(CommandTapped), typeof(ICommand), typeof(WidgetListCell),
            null);
        public ICommand CommandTapped
        {
            get { return (ICommand)GetValue(CommandTappedProperty); }
            set { SetValue(CommandTappedProperty, value); }
        }

        public static readonly BindableProperty CommandLongPressingProperty = BindableProperty.Create(nameof(CommandLongPressing), typeof(ICommand), typeof(WidgetListCell),
            null);
        public ICommand CommandLongPressing
        {
            get { return (ICommand)GetValue(CommandLongPressingProperty); }
            set { SetValue(CommandLongPressingProperty, value); }
        }

        public SkiaShape MainFrame { get; set; }

        public bool HasGestures
        {
            get
            {
                return _touchHandler != null;
            }
        }

        protected void DetachGestures()
        {
            if (!HasGestures)
                return;

            _touchHandler.Dispose();
        }


        protected void AttachGestures()
        {
            if (HasGestures)
                return;

            _touchHandler = new TouchEffect
            {
                Capture = true
            };
            _touchHandler.LongPressing += OnLongPressing;
            _touchHandler.Tapped += OnTapped;
            _touchHandler.Down += OnDown;
            _touchHandler.Up += OnUp;
            _touchHandler.TouchAction += OnTouch;
            this.Effects.Add(_touchHandler);
        }

        public bool IsSelected { get; set; }

        public bool CanBeSelected { get; set; } = true;

        public bool CanBeTapped { get; set; } = true;

        public bool IsNew { get; set; }

        private void OnLongPressing(object sender, TouchActionEventArgs args)
        {

            Debug.WriteLine($"[TOUCH] LongPressing!");

            if (CommandLongPressing != null)
            {
                if (CanBeSelected)
                {
                    IsSelected = true;
                    Update();
                }
                Device.StartTimer(TimeSpan.FromMilliseconds(2500), () =>
                {
                    if (CanBeSelected)
                    {
                        IsSelected = false;
                        Update();
                    }
                    return false;
                });
                CommandLongPressing.Execute(BindingContext);

            }

        }

        private bool lockTap;
        private TouchEffect _touchHandler;

        private void OnTapped(object sender, TouchActionEventArgs args)
        {
            if (!CanBeTapped)
                return;

            if (lockTap)
                return;

            Debug.WriteLine($"[TOUCH] Tapped!");

            lockTap = true;
            if (CanBeSelected)
            {
                IsSelected = true;
                Update();
            }

            Device.StartTimer(TimeSpan.FromMilliseconds(50), () =>
            {
                //invoke action
                CommandTapped?.Execute(BindingContext);

                Device.StartTimer(TimeSpan.FromMilliseconds(2500), () =>
                {
                    if (CanBeSelected)
                    {
                        IsSelected = false;
                        Update();
                    }
                    lockTap = false;
                    return false;
                });

                return false;
            });

        }

        private void OnUp(object sender, TouchActionEventArgs args)
        {
            Debug.WriteLine($"[TOUCH] UP");

            //MainFrame.StrokeWidth = 1 / RenderingScale; // 1 precise pixel
            //MainFrame.StrokeColor = StaticResources.DropShadow;
            //Update();
        }

        private void OnDown(object sender, TouchActionEventArgs args)
        {
            Debug.WriteLine($"[TOUCH] DOWN");
        }

        private void OnTouch(object sender, TouchActionEventArgs args)
        {
            Debug.WriteLine($"[TOUCH] {args.Type} {JsonConvert.SerializeObject(args)}");
        }

    }
}
