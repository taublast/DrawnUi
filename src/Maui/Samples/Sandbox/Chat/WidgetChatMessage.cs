using System.ComponentModel;
using DrawnUi.Extensions;
using Sandbox;

namespace AppoMobi.Forms.Controls
{

    public class WidgetChatMessage : WidgetListCell
    {
        //public override void Draw(SKCanvas canvas, SKImageInfo info, SKRect destination, double scale = 1.0, SkiaAnchor anchor = SkiaAnchor.TopLeft)
        //{
        //    if (IsRootView(ParentControl, info, destination))
        //        canvas.Clear(ClearColors.ToSKColor());

        //    if (IsSelected)
        //    {
        //        Opacity = 0.7;
        //    }
        //    else
        //    {
        //        Opacity = 1.0;
        //    }

        //    base.Draw(canvas, info, destination, scale);
        //}


        public SkiaShape FrameNewDate { get; set; }

        public SkiaLabel LabelFirstDate { get; set; }

        public SkiaLayout MainHorizontalStack { get; set; }

        public SkiaLayout MessageStack { get; set; }

        //public SkiaLayout StackBubble { get; set; }

        public SkiaImage Banner { get; set; }

        public SkiaLabel LabelMessage { get; set; }

        public SkiaLabel LabelTime { get; set; }

        public SkiaSvg IconAttachment { get; set; }

        public SkiaSvg BubbleArrowIncoming { get; set; }

        public SkiaSvg BubbleArrowOutcoming { get; set; }

        public SkiaSvg IconWasSent { get; set; }

        public SkiaSvg IconWasDelivered { get; set; }

        private ChatMessage _oldContext;

        public override void OnDisposing()
        {
            if (_oldContext != null)
                _oldContext.PropertyChanged -= OnContextPropertyChanged;

            Banner.Error -= OnImageError;

            DetachGestures();

            base.OnDisposing();
        }

        private void OnImageError(object sender, EventArgs e)
        {
            //todo AvatarDefault.IsVisible = true;
            Update();
        }

        private void OnContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {

            if (e.PropertyName == "Id")//Message sent
            {
                SetContentFull(BindingContext as ChatMessage);
                return;
            }

            if (e.PropertyName == "Read"
                || e.PropertyName == "Sent"
                || e.PropertyName == "Delivered")
            {
                UpdateStatus(BindingContext as ChatMessage);
                Update();
            }
            else
            if (e.PropertyName == "Notify")
            {
                var item = BindingContext as ChatMessage;
                if (item != null)
                {
                    IsNew = item.Notify;
                }
                else
                {
                    IsNew = false;
                }
                UpdateContainer(BindingContext as ChatMessage);
                Update();
            }
        }

        private void OnFirstTimeMeasured(object sender, ScaledSize scaledSize)
        {
            var label = sender as SkiaLabel;

            FrameNewDate.WidthRequest = label.MeasuredSize.Units.Width + label.Margin.Left + label.Margin.Right;
        }

        //this is more smooth when invoked here comparing to actions inside OnBindingContextChanged
        void OnTextSizeChanged(object sender, ScaledSize scaledSize)
        {
            var label = sender as SkiaLabel;
            var requestContainerWidth = label.MeasuredSize.Units.Width + label.Margin.Left + label.Margin.Right + 8;

            if (Template == ChatMetaType.File)
            {
                requestContainerWidth += 16 + 8 + 4;
            }

            if (MainFrame.Destination.Width != requestContainerWidth)
            {
                MainFrame.WidthRequest = requestContainerWidth;
            }

            var addHeight = 0;
            if (ShowDate)
            {
                addHeight = 24 + 16;
            }

            //set forms view dynamic height...
            if (Template == ChatMetaType.Image || Template == ChatMetaType.Video || Template == ChatMetaType.Article)
            {
                HeightRequest = addHeight + label.MeasuredSize.Units.Height + label.Margin.Top + label.Margin.Bottom + Banner.HeightRequest + 10;
            }
            else
            {
                HeightRequest = addHeight + label.MeasuredSize.Units.Height + label.Margin.Top + label.Margin.Bottom + 10;
            }
        }

        public WidgetChatMessage()
        {

            MainHorizontalStack = new SkiaLayout()
            {
                Tag = "MainHorizontalStack",
                Type = LayoutType.Row,
                Padding = new Thickness(8, 0, 8, 0),
                Spacing = 0,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.End
            }.WithParent(this);

            FrameNewDate = new SkiaShape()
            {
                Tag = "FrameNewDate",
                Type = ShapeType.Rectangle,
                CornerRadius = 8,
                HeightRequest = 24,
                WidthRequest = 100,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(8, 8, 8, 8),
            }.WithParent(this);

            LabelFirstDate = new SkiaLabel()
            {
                TranslationY = -4,
                Margin = new Thickness(10, 0, 10, 0),
                LineBreakMode = LineBreakMode.NoWrap,
                MaxLines = 1,
                FontAttributes = FontAttributes.Bold,
                Text = $"Time",
                FontSize = 10,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                TextColor = App.Current.Resources.Get<Color>("ColorPrimary")
            }.Adapt((c) =>
            {
                c.Measured += OnFirstTimeMeasured;
            }).WithParent(FrameNewDate);



            BubbleArrowIncoming = new SkiaSvg()
            {
                Tag = "arrowL",
                TranslationY = 5,
                TintColor = Colors.White,
                SvgString = App.Current.Resources.Get<string>("SvgChatFromLeft"),
                VerticalOptions = LayoutOptions.Start,
                HorizontalOptions = LayoutOptions.Start,
                HeightRequest = 8,
                WidthRequest = 8
            }.WithParent(MainHorizontalStack);

            #region BUBBLE

            MainFrame = new SkiaShape()
            {
                Tag = "MainFrame",
                Margin = new Thickness(0, 0, 0, 8),
                CornerRadius = 8,
                BackgroundColor = App.Current.Resources.Get<Color>("ColorPaperSecondary"),
                //           StrokeColor = StaticResources.ColorPaperSecondary,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            }.WithParent(MainHorizontalStack);

            MessageStack = new SkiaLayout()
            {
                Tag = "MessageStack",
                IsClippedToBounds = true,
                Type = LayoutType.Row,
                Spacing = 4,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.End
            }.WithParent(MainFrame);

            Banner = new SkiaImage()
            {
                IsVisible = false,
                Tag = "Banner",
                Background = App.Current.Resources.Get<Color>("ColorPrimaryLight"),
                //Margin = new Thickness(0, 0, 0, 0),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Start,
                HeightRequest = 200,
            }.WithParent(MessageStack).Adapt((c) =>
            {
                c.Error += OnImageError;
            });

            MessageStack.BreakLine();

            IconAttachment = new SkiaSvg()
            {
                Tag = "Attachment",
                Margin = new Thickness(8, 0, 0, 0),
                SvgString = App.Current.Resources.Get<string>("SvgAttachment"),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start,
                TintColor = App.Current.Resources.Get<Color>("ColorAccentLight"),
                HeightRequest = 16,
                WidthRequest = 16
            }.WithParent(MessageStack);

            LabelMessage = new SkiaLabel()
            {
                Tag = "Message",
                //TintColor = Colors.FloralWhite,
                LineBreakMode = LineBreakMode.WordWrap,
                MaxLines = -1,
                Text = $"Message",
                FontSize = 12,
                HorizontalOptions = LayoutOptions.Fill,
                Margin = new Thickness(8, 8, 8, 8),
                VerticalOptions = LayoutOptions.Center,
                TextColor = App.Current.Resources.Get<Color>("ColorText"),
            }.WithParent(MessageStack).Adapt((c) =>
            {
                c.Measured += OnTextSizeChanged;
            });

            LabelTime = new SkiaLabel()
            {
                Tag = "LabelTime",
                LineBreakMode = LineBreakMode.NoWrap,
                MaxLines = 1,
                Text = $"Time",
                FontSize = 9,
                HorizontalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 2, 2),
                VerticalOptions = LayoutOptions.End,
                //TextColor = StaticResources.ColorPrimaryLight,
            }.WithParent(MainFrame);

            IconWasSent = new SkiaSvg()
            {
                Tag = "WasDelivered",
                Margin = new Thickness(0, 0, 7, 3),
                TintColor = App.Current.Resources.Get<Color>("ColorPrimaryLight"),
                SvgString = App.Current.Resources.Get<string>("SvgCheck"),
                VerticalOptions = LayoutOptions.End,
                HorizontalOptions = LayoutOptions.End,
                HeightRequest = 11,
                WidthRequest = 11
            }.WithParent(MainFrame);

            IconWasDelivered = new SkiaSvg()
            {
                Tag = "WasSeen",
                //TranslationY = 5,
                Margin = new Thickness(0, 0, 3, 3),
                TintColor = App.Current.Resources.Get<Color>("ColorPrimaryLight"),
                SvgString = App.Current.Resources.Get<string>("SvgCheck"),
                VerticalOptions = LayoutOptions.End,
                HorizontalOptions = LayoutOptions.End,
                HeightRequest = 11,
                WidthRequest = 11
            }.WithParent(MainFrame);

            #endregion

            BubbleArrowOutcoming = new SkiaSvg()
            {
                Tag = "arrowR",
                TranslationY = 5,
                SvgString = App.Current.Resources.Get<string>("SvgChatFromRight"),
                VerticalOptions = LayoutOptions.Start,
                HorizontalOptions = LayoutOptions.Start,
                HeightRequest = 8,
                WidthRequest = 8
            }.WithParent(MainHorizontalStack);

            // GESTURES
            AttachGestures();

        }



        public bool ShowDate { get; set; }

        void UpdateContainer(ChatMessage item)
        {
            if (item == null)
                return;

            IconWasSent.IsVisible = false;
            IconWasDelivered.IsVisible = false;

            if (ShowDate)
            {
                FrameNewDate.IsVisible = true;
                MainHorizontalStack.Padding = new Thickness(0, 24 + 16, 0, 0);
            }
            else
            {
                FrameNewDate.IsVisible = false;
                MainHorizontalStack.Padding = new Thickness(0, 0, 0, 0);
            }

            if (item.Outgoing)
            {
                LabelTime.Margin = new Thickness(0, 0, 10, 2);

                LabelTime.TextColor = App.Current.Resources.Get<Color>("ColorPrimary");
                MainFrame.Background = App.Current.Resources.Get<Color>("ColorPaperSecondary");

                MainHorizontalStack.HorizontalOptions = LayoutOptions.End;
                BubbleArrowIncoming.IsVisible = false;
                //BubbleArrowOutcoming.IsVisible = true;
                if (item.IsFirst)
                {
                    BubbleArrowOutcoming.IsVisible = true;
                    MainFrame.Margin = new Thickness(0, 0, 0, 8);
                }
                else
                {
                    BubbleArrowOutcoming.IsVisible = false;
                    MainFrame.Margin = new Thickness(0, 0, 8, 8);
                }

            }
            else
            {
                LabelTime.Margin = new Thickness(0, 0, 2, 2);

                LabelTime.TextColor = App.Current.Resources.Get<Color>("ColorPrimaryLight");
                MainFrame.Background = Colors.White;

                MainHorizontalStack.HorizontalOptions = LayoutOptions.Start;
                BubbleArrowOutcoming.IsVisible = false;
                if (item.IsFirst)
                {
                    BubbleArrowIncoming.IsVisible = true;
                    MainFrame.Margin = new Thickness(0, 0, 0, 8);
                }
                else
                {
                    BubbleArrowIncoming.IsVisible = false;
                    MainFrame.Margin = new Thickness(8, 0, 0, 8);
                }
            }

            if (Template == ChatMetaType.File)
            {
                IconAttachment.IsVisible = true;
                LabelMessage.TextColor = App.Current.Resources.Get<Color>("ColorAccentLight");
            }
            else
            {
                IconAttachment.IsVisible = false;
                LabelMessage.TextColor = App.Current.Resources.Get<Color>("ColorText");
            }

            if (Template == ChatMetaType.Image || Template == ChatMetaType.Video)
            {
                Banner.HeightRequest = 200;
                Banner.IsVisible = true;
            }
            else
            if (Template == ChatMetaType.Article)
            {
                Banner.HeightRequest = 80;
                Banner.IsVisible = true;
            }
            else
            {
                Banner.IsVisible = false;
            }

        }

        void UpdateStatus(ChatMessage item)
        {
            if (item == null)
                return;

            if (item.Outgoing)
            {
                if (item.Read)
                {
                    IconWasSent.TintColor = Colors.DeepSkyBlue;
                    IconWasDelivered.TintColor = Colors.DeepSkyBlue;
                    IconWasSent.IsVisible = true;
                    IconWasDelivered.IsVisible = true;
                }
                else
                {
                    IconWasSent.TintColor = App.Current.Resources.Get<Color>("ColorPrimary");
                    IconWasDelivered.TintColor = App.Current.Resources.Get<Color>("ColorPrimary");
                    IconWasSent.IsVisible = item.Sent;
                    IconWasDelivered.IsVisible = item.Delivered;
                }
            }
        }

        void UpdateContent(ChatMessage item)
        {
            if (item == null)
                return;

            if (ShowDate)
                LabelFirstDate.Text = item.WhenDesc;
            else
                LabelFirstDate.Text = "";

            if (item.Outgoing)
            {
                LabelMessage.Text = item.Text + "           ";
            }
            else
            {
                LabelMessage.Text = item.Text + "      ";
            }

            LabelMessage.Measure(Width - 100, Height); //reduce by side padding 

            LabelTime.Text = item.DisplayTime;

            if (Template == ChatMetaType.Image || Template == ChatMetaType.Video)
            {
                Banner.Source = item.ImageMain;
            }
            else
            if (Template == ChatMetaType.Article)
            {
                Banner.Source = item.Metadata.Image;
            }
            else
            {
                Banner.Source = null;
            }
        }

        public ChatMetaType Template { get; protected set; } = ChatMetaType.Default;

        void SetContentFull(ChatMessage item)
        {
            IsNew = item.Notify;
            Template = item.PresentAs;
            ShowDate = item.IsFirstDate;
            CanBeTapped = Template != ChatMetaType.Default;

            //if (_oldContext != null)
            //{
            //    if (_oldContext.Outgoing != item.Outgoing || _oldContext.PresentAs != item.PresentAs)
            //        UpdateBubble(item);
            //}
            //else
            //{
            //    UpdateBubble(item);
            //}
            UpdateContainer(item);
            UpdateContent(item);
            UpdateStatus(item);
            Update();
        }

        protected override void OnBindingContextChanged()
        {
            var item = this.BindingContext as ChatMessage;
            if (item != null && _oldContext != item)
            {
                if (_oldContext != null)
                    _oldContext.PropertyChanged -= OnContextPropertyChanged;

                item.PropertyChanged += OnContextPropertyChanged;
                _oldContext = item;

                SetContentFull(item);
            }

        }




    }

}
