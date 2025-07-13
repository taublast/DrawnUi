<!-- Component: Code-Behind, Category: Programming/Fluent API, Complexity: Intermediate -->
# Code-Behind and Fluent API Patterns

## Scenario
This guide covers real-world code-behind patterns and fluent API usage in DrawnUI applications. It demonstrates how to create controls programmatically, handle events, implement MVVM patterns, and use the powerful fluent extensions for clean, maintainable code. Use these patterns when building complex UIs with C# code or when you need dynamic control creation.

## Complete Working Examples from Real AppoMobi.Mobile Code

### 1. ScreenSearchOnMap - Complex Map Interface with Drawer
```csharp
// Real pattern from ScreenSearchOnMap.cs - Complex UI with map, overlay buttons, and search drawer
public class ScreenSearchOnMap : AppScreen
{
    private AppMap Map;
    private SkiaDrawer Drawer;
    private SkiaButton ButtonPosition;
    private SkiaButton ButtonToggle;
    private SkiaLayout OverlayButtons;

    public ScreenSearchOnMap()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;

        var marginTop = 100;
        var headerSize = Super.Screen.BottomInset + 130 + marginTop;

        SkiaLabel? SearchLabel = null;
        SkiaMauiEntry? SearchEntry = null;
        SkiaSvg BtnToggleDrawerIcon;

        Children = new List<SkiaControl>()
        {
            // MAP with complex event handling and observation
            new AppMap()
            {
                BackgroundColor = AppColors.Background,
                UseCache = SkiaCacheType.Operations,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                ZIndex = -1
            }.Adapt((me) =>
            {
                // Complex map pin setup function
                MapPin SetupClickedPin(bool show)
                {
                    var pin = me.Pins.FirstOrDefault(x => x.Id == "To");
                    var lat = me.LastClicked.Y;
                    var lon = me.LastClicked.X;

                    if (pin == null)
                    {
                        pin = new MapPin()
                        {
                            Id = "To",
                            Icon = me.IconFrom,
                        };
                        pin.Latitude = lat;
                        pin.Longitude = lon;
                        me.Pins.Add(pin);
                    }
                    else
                    {
                        pin.IsVisible = show;
                        pin.Latitude = lat;
                        pin.Longitude = lon;
                    }

                    if (pin.Icon is IMapPinIcon icon)
                    {
                        icon.PinChanged(pin);
                    }

                    me.Update();
                    return pin;
                }

                // Real event handling pattern
                me.ClickedPoint += (s, point) =>
                {
                    SetupClickedPin(true);
                    Model.SelectCoords(new(point.Y, point.X));
                };

                // Real observation pattern with MainThread handling
                me.Observe(Model, (me, prop) =>
                {
                    if (prop.IsEither(nameof(BindingContext), nameof(Model.GeoDataTo)))
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            var pin = SetupClickedPin(Model.GeoDataTo != null);
                            if (Model.GeoDataTo != null)
                            {
                                pin.Label = Model.GeoDataTo.Title;
                                pin.Address = Model.GeoDataTo.Description;
                                if (pin.Latitude == 0 || pin.Longitude == 0)
                                {
                                    pin.Latitude = Model.GeoDataTo.Latitude;
                                    pin.Longitude = Model.GeoDataTo.Longitude;
                                }
                            }
                            me.Update();
                        });
                    }

                    if (prop.IsEither(nameof(BindingContext), nameof(Model.CenterMap)))
                    {
                        if (Model.CenterMap != Point.Zero)
                        {
                            var point = Model.CenterMap;
                            me.AutoCenterTo(point.Y, point.X);
                            me.LastClicked = point;
                        }
                    }
                });
            })
            .Assign(out Map),

            // MAP OVERLAY with floating buttons
            new SkiaLayout()
            {
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                UseCache = SkiaCacheType.Operations,
                Children = new List<SkiaControl>()
                {
                    // Geoposition button with nested structure
                    new SkiaButton()
                    {
                        UseCache = SkiaCacheType.Image,
                        BackgroundColor = AppColors.Background,
                        Margin = 8,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.End,
                        TouchEffectColor = AppColors.PrimaryLight,
                        Children = new List<SkiaControl>()
                        {
                            new SkiaShape()
                            {
                                Tag = "BtnShape",
                                WidthRequest = 44,
                                LockRatio = 1,
                                Type = ShapeType.Circle,
                                Content = new SkiaSvg()
                                {
                                    SvgString = App.Current.Resources.Get<string>("SvgMapArrow"),
                                    HeightRequest = 20,
                                    TintColor = AppColors.ControlPrimary,
                                    LockRatio = 1,
                                    VerticalOptions = LayoutOptions.Center,
                                    HorizontalOptions = LayoutOptions.Center
                                }
                            }
                        },
                        Clicked = async (me, args) => { Model.ApplyUserLocation(); }
                    }.Assign(out ButtonPosition),

                    // Top buttons with status bar handling
                    new SkiaStack()
                    {
                        Spacing = 0,
                        Children = new List<SkiaControl>()
                        {
                            new StatusBarPlaceholder(),
                            new SkiaLayer()
                            {
                                Children = new List<SkiaControl>()
                                {
                                    // Go back button
                                    new SkiaButton()
                                    {
                                        UseCache = SkiaCacheType.Image,
                                        BackgroundColor = AppColors.Background,
                                        Margin = 16,
                                        VerticalOptions = LayoutOptions.Start,
                                        HorizontalOptions = LayoutOptions.Start,
                                        TouchEffectColor = AppColors.PrimaryLight,
                                        Children = new List<SkiaControl>()
                                        {
                                            new SkiaShape()
                                            {
                                                Tag = "BtnShape",
                                                WidthRequest = 44,
                                                LockRatio = 1,
                                                Type = ShapeType.Circle,
                                                Content = new SkiaSvg()
                                                {
                                                    SvgString = App.Current.Resources.Get<string>("SvgGoBack"),
                                                    HeightRequest = 20,
                                                    TintColor = AppColors.ControlPrimary,
                                                    LockRatio = 1,
                                                    VerticalOptions = LayoutOptions.Center,
                                                    HorizontalOptions = LayoutOptions.Center
                                                }
                                            }
                                        },
                                        Clicked = (me, args) => { App.GoBack(); }
                                    },

                                    // Toggle drawer button with observation
                                    new SkiaButton()
                                    {
                                        UseCache = SkiaCacheType.Image,
                                        BackgroundColor = AppColors.Background,
                                        Margin = 16,
                                        VerticalOptions = LayoutOptions.Start,
                                        HorizontalOptions = LayoutOptions.End,
                                        TouchEffectColor = AppColors.PrimaryLight,
                                        Children = new List<SkiaControl>()
                                        {
                                            new SkiaShape()
                                            {
                                                Tag = "BtnShape",
                                                WidthRequest = 44,
                                                LockRatio = 1,
                                                Type = ShapeType.Circle,
                                                Content = new SkiaSvg()
                                                {
                                                    SvgString = App.Current.Resources.Get<string>("SvgSearch"),
                                                    HeightRequest = 20,
                                                    TintColor = AppColors.ControlPrimary,
                                                    LockRatio = 1,
                                                    VerticalOptions = LayoutOptions.Center,
                                                    HorizontalOptions = LayoutOptions.Center
                                                }.Assign(out BtnToggleDrawerIcon)
                                            }
                                        },
                                        Clicked = (me, args) => { Drawer.IsOpen = !Drawer.IsOpen; }
                                    }.Initialize((me) =>
                                    {
                                        // Real observation pattern for icon changes
                                        me.Observe(Drawer, (me, prop) =>
                                        {
                                            if (prop.IsEither(nameof(BindingContext), nameof(SkiaDrawer.IsOpen)))
                                            {
                                                if (Drawer.IsOpen)
                                                {
                                                    BtnToggleDrawerIcon.SvgString =
                                                        App.Current.Resources.Get<string>("SvgSearchMap");
                                                }
                                                else
                                                {
                                                    BtnToggleDrawerIcon.SvgString =
                                                        App.Current.Resources.Get<string>("SvgSearch");
                                                }
                                            }
                                        });
                                    })
                                    .Assign(out ButtonToggle),
                                }
                            }
                        }
                    },
                }
            }.Assign(out OverlayButtons),
        };
    }
}

### 2. ScreenChat - Complex Chat Interface with Virtualization
```csharp
// Real pattern from ScreenChat.cs - Chat interface with scroll, entry, and complex observation
public class ScreenChat : AppScreen
{
    private SkiaScroll MainScroll;
    private ChatCellStack StackCells;
    private ChatEntry MainEntry;
    private SkiaLayout BarInput;

    public ScreenChat()
    {
        Children = new List<SkiaControl>()
        {
            new SkiaLayout()
            {
                Type = LayoutType.Grid,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>()
                {
                    // MAIN SCROLL with virtualization and complex observation
                    new SkiaScroll()
                    {
                        Orientation = ScrollOrientation.Vertical,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        Rotation = 180,
                        ReverseGestures = true,
                        LoadMoreCommand = Model.LoaderItems.CommandLoadMore,
                        Header = new SkiaControl()
                        {
                            HeightRequest = 16,
                            HorizontalOptions = LayoutOptions.Fill,
                        },
                        Footer = new SkiaLayout()
                        {
                            HorizontalOptions = LayoutOptions.Fill,
                            HeightRequest = 50,
                        },
                        Content = new ChatCellStack()
                        {
                            RecyclingTemplate = RecyclingTemplate.Enabled,
                            MeasureItemsStrategy = MeasuringStrategy.MeasureAll,
                            VirtualisationInflated = 40,
                            ItemTemplateType = typeof(ChatMessageCell),
                            ItemsSource = Model.LoaderItems.Items,
                            Margin = new(8, 0),
                        }.Assign(out StackCells)
                    }
                    .Assign(out MainScroll)
                    .WithRow(0)
                    .Observe(Model, (me, prop) =>
                    {
                        if (prop.IsEither(nameof(BindingContext), nameof(Model.ScrollToIndex)))
                        {
                            me.OrderedScroll = Model.ScrollToIndex;
                            Model.ScrollToIndex = -1;
                        }
                    }),

                    // EMPTY VIEW with conditional visibility using ObserveBindingContext
                    new SkiaStack()
                    {
                        IsVisible = false,
                        UseCache = SkiaCacheType.Image,
                        VerticalOptions = LayoutOptions.Center,
                        Spacing = 24,
                        Margin = new(24, 32),
                        Children = new List<SkiaControl>()
                        {
                            new HintCardIcon()
                            {
                                SvgString = App.Current.Resources.Get<string>("SvgDuoOperator")
                            },
                            new SkiaLabel(ResStrings.ChatWelcome)
                            {
                                TextColor = AppColors.Text,
                                FontFamily = AppFonts.SemiBold,
                                HorizontalOptions = LayoutOptions.Fill,
                                HorizontalTextAlignment = DrawTextAlignment.Center,
                            }
                        }
                    }
                    .WithRow(0)
                    .ObserveBindingContext<SkiaLayout, ChatViewModel>((me, vm, prop) =>
                    {
                        bool attached = prop == nameof(BindingContext);
                        if (attached || prop == nameof(vm.IsBusy))
                        {
                            me.IsVisible = !vm.IsBusy && vm.CanSend && Model.LoaderItems.Items.Count == 0;
                        }
                    }),

                    // REPLY ATTACHMENT OVERLAY with grid layout
                    new SkiaLayout()
                    {
                        UseCache = SkiaCacheType.Image,
                        BackgroundColor = AppColors.BackgroundMinor,
                        Padding = 8,
                        RowSpacing = 0,
                        ColumnSpacing = 8,
                        Type = LayoutType.Grid,
                        HorizontalOptions = LayoutOptions.Fill,
                        Children = new List<SkiaControl>()
                        {
                            new SkiaLabel()
                            {
                                Margin = new(0, 0, 12, 0),
                                FontFamily = "FaSolid",
                                HorizontalOptions = LayoutOptions.End,
                                Text = FaPro.Xmark,
                                TextColor = AppColors.TextMinor,
                                VerticalOptions = LayoutOptions.Center,
                            }.SetGrid(2, 0, 1, 2),

                            new SkiaSvg()
                            {
                                HeightRequest = 20,
                                HorizontalOptions = LayoutOptions.Center,
                                SvgString = App.Current.Resources.Get<string>("SvgReply"),
                                TintColor = AppColors.Primary,
                                VerticalOptions = LayoutOptions.Center,
                                LockRatio = 1,
                            }.SetGrid(0, 0, 1, 2),

                            new SkiaLabel()
                            {
                                FontSize = 14,
                                LineBreakMode = LineBreakMode.HeadTruncation,
                                MaxLines = 2,
                                TextColor = AppColors.Primary
                            }
                            .ObserveBindingContext<SkiaLabel, ChatViewModel>((me, vm, prop) =>
                            {
                                bool attached = prop == nameof(BindingContext);
                                if (attached || prop == nameof(vm.SelectedMessageFull))
                                {
                                    me.Text = vm.SelectedMessageFull.PlayerName;
                                }
                            })
                            .SetGrid(1, 0),

                            new SkiaLabel()
                            {
                                FontSize = 14,
                                LineBreakMode = LineBreakMode.HeadTruncation,
                                MaxLines = 2,
                            }
                            .ObserveBindingContext<SkiaLabel, ChatViewModel>((me, vm, prop) =>
                            {
                                bool attached = prop == nameof(BindingContext);
                                if (attached || prop == nameof(vm.SelectedMessageFull))
                                {
                                    me.Text = vm.SelectedMessageFull.Text;
                                }
                            })
                            .SetGrid(1, 1),
                        }
                    }
                    .OnTapped((me) => { Model.CommandCancelReply.Execute(null); })
                    .WithRow(2)
                    .WithRowDefinitions("Auto,Auto")
                    .WithColumnDefinitions("Auto,*,40")
                    .Observe(Model, (me, prop) =>
                    {
                        bool attached = prop == nameof(BindingContext);
                        if (attached || prop == nameof(Model.ReplyOn))
                        {
                            me.IsVisible = Model.ReplyOn;
                            me.Parent?.Invalidate();
                            if (Model.ReplyOn)
                            {
                                MainEntry.IsFocused = true;
                            }
                        }
                    }),

                    // SEND BAR with complex entry and buttons
                    new SkiaStack()
                    {
                        IsParentIndependent = true,
                        BackgroundColor = AppColors.Focus,
                        Spacing = 0,
                        Children = new List<SkiaControl>()
                        {
                            new SkiaShape()
                            {
                                HorizontalOptions = LayoutOptions.Fill,
                                HeightRequest = 1,
                                BackgroundColor = AppColors.Primary
                            },

                            new SkiaLayout()
                            {
                                Padding = new(4, 0, 4, 0),
                                HeightRequest = 66,
                                RowSpacing = 0,
                                Type = LayoutType.Grid,
                                ColumnSpacing = 8,
                                HorizontalOptions = LayoutOptions.Fill,
                                DefaultRowDefinition = new RowDefinition(GridLength.Star),
                                Children = new List<SkiaControl>()
                                {
                                    // Entry container
                                    new SkiaShape()
                                    {
                                        HorizontalOptions = LayoutOptions.Fill,
                                        VerticalOptions = LayoutOptions.Center,
                                        BackgroundColor = AppColors.Background,
                                        Padding = new(8, 2, 0, 2),
                                        CornerRadius = 16,
                                        Content = new ChatEntry()
                                        {
                                            IsParentIndependent = true,
                                            BackgroundColor = Colors.Transparent,
                                            LockFocus = true,
                                            ReturnType = ReturnType.Send,
                                            Placeholder = ResStrings.PlaceholderYourMessage,
                                            HorizontalOptions = LayoutOptions.Fill,
                                            VerticalOptions = LayoutOptions.Start,
                                            Padding = new Thickness(0, 0, 0, 0),
                                            MaxLines = 3,
                                            MinimumHeightRequest = 30,
                                            TextColor = AppColors.Text,
                                            PlaceholderColor = AppColors.Primary,
                                        }
                                        .Assign(out MainEntry)
                                        .ObserveSelf((me, prop) =>
                                        {
                                            if (prop.IsEither(nameof(BindingContext), nameof(me.Text)))
                                            {
                                                Model.Message = me.Text;
                                            }
                                        })
                                        .Observe(Model, (me, prop) =>
                                        {
                                            if (prop.IsEither(nameof(BindingContext), nameof(Model.Message)))
                                            {
                                                me.Text = Model.Message;
                                            }
                                        })
                                    }.WithColumn(1),

                                    // Attach button
                                    new SkiaLayer()
                                    {
                                        VerticalOptions = LayoutOptions.Fill,
                                        HorizontalOptions = LayoutOptions.Fill,
                                        UseCache = SkiaCacheType.Image,
                                        Children = new List<SkiaControl>()
                                        {
                                            new SkiaSvg()
                                            {
                                                UseCache = SkiaCacheType.Operations,
                                                HeightRequest = 20,
                                                LockRatio = 1,
                                                SvgString = App.Current.Resources.Get<string>("SvgAttachment"),
                                                VerticalOptions = LayoutOptions.Center,
                                                HorizontalOptions = LayoutOptions.Center,
                                                TintColor = AppColors.Primary
                                            },
                                        }
                                    }.WithColumn(0)
                                    .OnTapped((me) => { Model.CommandSelectAttachment.Execute(null); }),

                                    // Send button with layered icons
                                    new SkiaLayer()
                                    {
                                        VerticalOptions = LayoutOptions.Fill,
                                        HorizontalOptions = LayoutOptions.Fill,
                                        UseCache = SkiaCacheType.Image,
                                        Children = new List<SkiaControl>()
                                        {
                                            new SkiaSvg()
                                            {
                                                UseCache = SkiaCacheType.Operations,
                                                HeightRequest = 32,
                                                LockRatio = 1,
                                                SvgString = App.Current.Resources.Get<string>("SvgCircle"),
                                                VerticalOptions = LayoutOptions.Center,
                                                HorizontalOptions = LayoutOptions.Center,
                                                TintColor = AppColors.Primary
                                            },
                                            new SkiaSvg()
                                            {
                                                Margin = new(2, 0, 0, 0),
                                                UseCache = SkiaCacheType.Operations,
                                                HeightRequest = 14,
                                                LockRatio = 1,
                                                SvgString = App.Current.Resources.Get<string>("SvgSend"),
                                                VerticalOptions = LayoutOptions.Center,
                                                HorizontalOptions = LayoutOptions.Center,
                                                TintColor = Colors.White
                                            }
                                        }
                                    }.WithColumn(2)
                                    .OnTapped((me) =>
                                    {
                                        Debug.WriteLine("SEND TAPPED");
                                        Model.CommandSubmit.Execute(null);
                                    }),
                                }
                            }
                            .WithColumnDefinitions("32,*,40"),
                        }
                    }
                    .Assign(out BarInput)
                    .WithRow(3),

                    new KeyboardWIthInsetsPlaceholder()
                    {
                        BackgroundColor = AppColors.ControlPrimary
                    }.WithRow(4),
                },
            }
            .WithRowDefinitions("*,Auto,Auto,Auto,Auto"),
        };

        // Real event handling pattern
        MainScroll.PropertyChanged += MainScroll_OnPropertyChanged;
    }

    // Real lifecycle management
    public override void OnDisappearing()
    {
        base.OnDisappearing();
        Model.CommandClearNotifications.Execute(null);
        MainEntry?.Unfocus();
        Model?.UnregisterAsNotificationsProcessor();
        App.Instance.Messager.Unsubscribe(this, AppMessages.Chat);
    }

    public override void OnAppearing()
    {
        base.OnAppearing();
        Tasks.StartDelayed(TimeSpan.FromMilliseconds(50), () =>
        {
            Task.Run(async () =>
            {
                var canUpdate = BindingContext as IUpdateUIState;
                canUpdate?.UpdateState();
            }).ConfigureAwait(false);

            Model?.RegisterAsNotificationsProcessor();
        });

        App.Instance.Messager.Subscribe<string>(this, AppMessages.Chat, async (sender, arg) =>
        {
            // Handle chat messages
        });
    }
}

### 3. ScreenRequestEditor - Complex Form with ObserveTargetProperty
```csharp
// Real pattern from ScreenRequestEditor.cs - Complex form with deep property observation
public class ScreenRequestEditor : AppScreen
{
    public readonly RequestEditorViewModel Model;
    private SkiaScroll MainScroll;

    public ScreenRequestEditor(RequestEditorViewModel vm)
    {
        Model = vm;
        BindingContext = Model;

        Shell.SetPresentationMode(this, PresentationMode.ModalAnimated);

        App.Instance.Messager.Subscribe<string>(this, AppMessages.NavigatedToView, async (sender, arg) =>
        {
            if (arg == $"{this}")
            {
                Model.UpdateState(true);
            }
        });

        CreateContent();
    }

    private SkiaLayout CreateContentLayout()
    {
        SkiaLabel LabelTitle;
        SkiaLabel LabelDropdownCenter;
        SkiaLabel LabelDropdownService;
        EditorFieldGallery Attachments;
        SkiaLabel LabelCargoDesc;
        SkiaLabel LabelAddressNotes;
        CardFrame CardStatus;
        SkiaLabel LabelStatus;
        SkiaLabel LabelPrice;
        SkiaLabel LabelGallery;
        SkiaLayout? LayoutStars = null;
        SkiaLabel LabelReview;

        return new ScreenVerticalStack
        {
            Padding = new Thickness(0, 24, 0, 0),
            HorizontalOptions = LayoutOptions.Fill,
            MinimumHeightRequest = -1,
            Spacing = 0,
            Type = LayoutType.Column,
            UseCache = SkiaCacheType.Operations,
            Children = new List<SkiaControl>
            {
                new StatusBarPlaceholder(),

                // Header with title
                new CardTitle(ResStrings.RequestDetails)
                    .SetFontSize(20)
                    .Assign(out LabelTitle),

                // Center selection dropdown
                new CardTitle(ResStrings.SelectCenter)
                    .SetFontSize(12),

                new InputFrame()
                {
                    BackgroundColor = AppColors.ControlMinor,
                    Content = new SkiaLayer()
                    {
                        VerticalOptions = LayoutOptions.Fill,
                        Children = new List<SkiaControl>()
                        {
                            new SkiaLabel()
                                .Assign(out LabelDropdownCenter)
                                .CenterY(),

                            new SkiaSvg()
                            {
                                UseCache = SkiaCacheType.Operations,
                                TintColor = AppColors.ControlPrimary,
                                HeightRequest = 16,
                                LockRatio = 1,
                                SvgString = App.Current.Resources.Get<string>("SvgDropdown")
                            }
                            .Initialize((me) => { me.IsVisible = Model.CanChange; })
                            .CenterY().EndX()
                        }
                    }
                }.OnTapped((frame) =>
                {
                    if (Model.CanChange)
                    {
                        _ = App.Instance.Singletons.Presentation.Shell.GoToAsync(AppRoutes.SearchCenters.Route);
                    }
                }),

                // Address details with grid layout
                new SkiaLayout()
                {
                    Type = LayoutType.Grid,
                    RowSpacing = 8,
                    ColumnSpacing = 8,
                    Children = new List<SkiaControl>()
                    {
                        // Entrance field with ObserveTargetProperty pattern
                        new InputFrame()
                        {
                            Content = new SkiaLabel().CenterY().FillX()
                                .ObserveTargetProperty<SkiaLabel, RequestEditorViewModel, ServiceRequest, string>(
                                    vm => vm.Item,
                                    item => item.AddressEntrance,
                                    (entry, value) =>
                                    {
                                        entry.Text = value ?? string.Empty;
                                    },
                                    string.Empty)
                        }.SetGrid(0, 1)
                        .OnTapped((me) =>
                        {
                            if (Model.CanChange)
                            {
                                var popup = new ScreenTextEditor(ResStrings.Entrance,
                                    Model.Item.AddressEntrance,
                                    false,
                                    (value) => { Model.Item.AddressEntrance = value; });

                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    _ = App.Instance.Singletons.Presentation.Shell.PushModalAsync(
                                        popup, true, true, true);
                                });
                            }
                        }),

                        // Floor field with same pattern
                        new InputFrame()
                        {
                            Content = new SkiaLabel().CenterY().FillX()
                                .ObserveTargetProperty<SkiaLabel, RequestEditorViewModel, ServiceRequest, string>(
                                    vm => vm.Item,
                                    item => item.AddressFloor,
                                    (entry, value) =>
                                    {
                                        entry.Text = value ?? string.Empty;
                                    },
                                    string.Empty)
                        }.SetGrid(1, 1)
                        .OnTapped((me) =>
                        {
                            if (Model.CanChange)
                            {
                                var popup = new ScreenTextEditor(ResStrings.Floor,
                                    Model.Item.AddressFloor,
                                    false,
                                    (value) => { Model.Item.AddressFloor = value; });

                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    _ = App.Instance.Singletons.Presentation.Shell.PushModalAsync(
                                        popup, true, true, true);
                                });
                            }
                        }),
                    }
                }
                .WithColumnDefinitions("*,*")
                .WithRowDefinitions("Auto"),

                // Cargo description with complex modal handling
                new InputFrame()
                {
                    Content = new SkiaLabel().CenterY().FillX()
                        .ObserveTargetProperty<SkiaLabel, RequestEditorViewModel, ServiceRequest, string>(
                            vm => vm.Item,
                            item => item.Question,
                            (entry, value) =>
                            {
                                entry.Text = value ?? string.Empty;
                            },
                            string.Empty)
                }
                .OnTapped((me) =>
                {
                    if (Model.CanChange)
                    {
                        var popup = new ScreenTextEditor(
                            ResStrings.WhatNeedsToBeShipped,
                            Model.Item.Question,
                            true,
                            (value) =>
                            {
                                Model.Item.Question = value;
                                Model.EditorValidate();
                            });

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            _ = App.Instance.Singletons.Presentation.Shell.PushModalAsync(
                                popup, true, true, true);
                        });
                    }
                }).SetHeight(80),

                // Gallery field
                new CardTitle(ResStrings.UploadPhoto)
                    .SetFontSize(12).Assign(out LabelGallery),

                new EditorFieldGallery()
                {
                    HeightRequest = 80,
                    HorizontalOptions = LayoutOptions.Fill,
                }.Assign(out Attachments),

                // Submit button with complex observation
                new AppSubmitButton(ResStrings.BtnSave)
                {
                    AddMarginTop = 16,
                }
                .ObserveBindingContext<ButtonMedium, RequestEditorViewModel>((me, vm, prop) =>
                {
                    bool attached = prop == nameof(BindingContext);

                    if (attached || prop == nameof(vm.IsBusy))
                    {
                        me.IsVisible = vm.CanChange && !vm.IsBusy;
                    }

                    if (attached || prop == nameof(vm.CommandEditorSubmit))
                    {
                        me.CommandTapped = vm.CommandEditorSubmit;
                    }

                    if (attached || prop == nameof(vm.EditorIsValid))
                    {
                        me.Look = vm.EditorIsValid ? BtnStyle.Default : BtnStyle.Disabled;
                    }
                }),

                // Activity indicator with binding
                new AppActivityIndicator()
                {
                    IsRunning = true,
                }.Adapt((indicator) =>
                {
                    indicator.SetBinding(AppActivityIndicator.IsVisibleProperty, "IsBusy");
                }),

                new BottomTabsPlaceholder()
            }
        }
        .Initialize((layout) =>
        {
            // Real initialization pattern with complex data setup
            layout.ObserveBindingContext<SkiaLayout, RequestEditorViewModel>((me, vm, prop) =>
            {
                bool attached = prop == nameof(BindingContext);

                if (attached || prop == nameof(vm.Item))
                {
                    if (!vm.CanChange)
                    {
                        vm.EditUploads.Options = SelectionGalleryOptions.Readonly;
                        LabelGallery.Text = ResStrings.Attachements;
                    }

                    if (Model.Item.Building == null)
                    {
                        LabelDropdownCenter.Text = ResStrings.Unset;
                    }
                    else
                    {
                        LabelDropdownCenter.Text = Model.Item.Building.DisplayDescription;
                    }

                    if (Model.Item.Service == null)
                    {
                        LabelDropdownService.Text = ResStrings.Unset;
                    }
                    else
                    {
                        LabelDropdownService.Text = Model.Item.Service.Name;
                    }

                    Attachments.UpdateGallery(vm.EditUploads);
                }
            });
        });
    }

    public override void OnWillDisposeWithChildren()
    {
        base.OnWillDisposeWithChildren();
        App.Instance.Messager.Unsubscribe(this, AppMessages.NavigatedToView);
    }
}
```

### Real Service Request Cell Pattern
```csharp
using System.ComponentModel;
using AppoMobi.Common.Enums.UserData;
using AppoMobi.Specials.Localization;

namespace AppoMobi.Mobile.Views;

public class CellServiceRequest : FastCellWithBanner
{
    // Control references declared as fields
    private SkiaLabel labelService;
    private SkiaLabel labelFrom;
    private SkiaLabel labelTo;
    private SkiaLabel LabelStatus;
    private SkiaSvg SvgStatus;
    private SkiaShape FrameStatus;
    private SkiaLabel labelDate;
    private SkiaLabel labelTime;
    private SkiaLabel labelPrice;
    private SkiaLabel labelQuestion;
    private SkiaShape MainFrame;
    private SkiaLayout LayoutReview;

    public CellServiceRequest()
    {
        Tag = "RequestCell";

        var cellsHeight = 200.0;
        var cellsCorners = 12.0;
        var infoFontSize = 14.0;
        var infoIconSize = 12.5;
        var infoInterval = 10.0;

        HorizontalOptions = LayoutOptions.Fill;
        HeightRequest = cellsHeight;
        Padding = new(16, 4);
        UseCache = SkiaCacheType.ImageDoubleBuffered;
        Children = new List<SkiaControl>()
        {
            new AppFrame()
            {
                BackgroundColor = AppColors.BackgroundSecondary,
                StrokeColor = AppColors.ControlSecondary,
                StrokeWidth = 1,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Content = new SkiaLayout()
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Children = new List<SkiaControl>()
                    {
                        // Main content layout
                        new SkiaLayout()
                        {
                            UseCache = SkiaCacheType.Image,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                            Padding = new(16, 16),
                            Children = new List<SkiaControl>()
                            {
                                // Colored header background
                                new SkiaShape()
                                {
                                    HeightRequest = 56,
                                    Margin = -16,
                                    HorizontalOptions = LayoutOptions.Fill,
                                    BackgroundColor = AppColors.ControlMinorOpacity8,
                                },

                                // Service name
                                new SkiaLabel()
                                {
                                    Margin = new Thickness(0, 0, 120, 0),
                                    TextColor = AppColors.Text,
                                    FontSize = 17,
                                    MaxLines = 1,
                                    LineBreakMode = LineBreakMode.TailTruncation,
                                    FontFamily = "FontTextBold"
                                }.Assign(out labelService),

                                // Address FROM
                                new SkiaLabel()
                                {
                                    Margin = new Thickness(0, 53, 120, 0),
                                    TextColor = AppColors.Text,
                                    FontSize = 15,
                                    MaxLines = 1,
                                    LineBreakMode = LineBreakMode.TailTruncation,
                                    FontFamily = AppFonts.SemiBold
                                }.Assign(out labelFrom),

                                // Address TO and cargo details
                                new SkiaStack()
                                {
                                    Spacing = 4,
                                    Margin = new Thickness(0, 74, 0, 0),
                                    Children = new List<SkiaControl>()
                                    {
                                        new SkiaLabel()
                                        {
                                            TextColor = AppColors.ControlPrimary,
                                            FontSize = 15,
                                            MaxLines = 2,
                                            LineBreakMode = LineBreakMode.TailTruncation,
                                            FontFamily = AppFonts.Bold
                                        }.Assign(out labelTo),

                                        new SkiaLabel()
                                        {
                                            TextColor = AppColors.ControlPrimary,
                                            FontSize = 15,
                                            MaxLines = 1,
                                            LineBreakMode = LineBreakMode.TailTruncation,
                                            FontFamily = AppFonts.Normal
                                        }.Assign(out labelQuestion),
                                    }
                                },

                                // Info row with icons (time, date, price)
                                new SkiaLayout()
                                {
                                    VerticalOptions = LayoutOptions.End,
                                    HorizontalOptions = LayoutOptions.Fill,
                                    Type = LayoutType.Row,
                                    Spacing = 3,
                                    Children = new List<SkiaControl>()
                                    {
                                        // Date icon and label
                                        new SkiaSvg()
                                        {
                                            VerticalOptions = LayoutOptions.Center,
                                            HeightRequest = infoIconSize,
                                            LockRatio = 1,
                                            TintColor = AppColors.Text,
                                            SvgString = App.Current.Resources.Get<string>("SvgTimeDate"),
                                        },
                                        new SkiaLabel()
                                        {
                                            VerticalOptions = LayoutOptions.Center,
                                            TextColor = AppColors.Text,
                                            FontSize = infoFontSize,
                                            FontFamily = AppFonts.SemiBold
                                        }.Assign(out labelDate),

                                        // Time icon and label
                                        new SkiaSvg()
                                        {
                                            Margin = new(infoInterval, 0, 0, 0),
                                            VerticalOptions = LayoutOptions.Center,
                                            HeightRequest = infoIconSize,
                                            LockRatio = 1,
                                            TintColor = AppColors.Text,
                                            SvgString = App.Current.Resources.Get<string>("SvgTime"),
                                        },
                                        new SkiaLabel()
                                        {
                                            VerticalOptions = LayoutOptions.Center,
                                            TextColor = AppColors.Text,
                                            FontSize = infoFontSize,
                                            FontFamily = AppFonts.SemiBold
                                        }.Assign(out labelTime),

                                        // Price icon and label
                                        new SkiaSvg()
                                        {
                                            Margin = new(infoInterval, 0, 0, 0),
                                            VerticalOptions = LayoutOptions.Center,
                                            HeightRequest = infoIconSize,
                                            LockRatio = 1,
                                            TintColor = AppColors.Text,
                                            SvgString = App.Current.Resources.Get<string>("SvgMoney"),
                                        },
                                        new SkiaLabel()
                                        {
                                            VerticalOptions = LayoutOptions.Center,
                                            TextColor = AppColors.Text,
                                            FontSize = infoFontSize,
                                            FontFamily = AppFonts.SemiBold
                                        }.Assign(out labelPrice),
                                    }
                                },

                                // Status badge
                                new SkiaShape()
                                {
                                    Tag = "Status",
                                    StrokeWidth = 0,
                                    StrokeColor = AppColors.ControlHoverMinor,
                                    BackgroundColor = AppColors.PrimaryLight,
                                    CornerRadius = 8,
                                    Padding = new(8, 5),
                                    HorizontalOptions = LayoutOptions.End,
                                    VerticalOptions = LayoutOptions.Start,
                                    Content = new SkiaLayout()
                                    {
                                        Type = LayoutType.Row,
                                        Spacing = 5,
                                        Children = new List<SkiaControl>()
                                        {
                                            new SkiaSvg()
                                            {
                                                VerticalOptions = LayoutOptions.Center,
                                                HeightRequest = 15,
                                                LockRatio = 1,
                                                TintColor = AppColors.Text,
                                                SvgString = App.Current.Resources.Get<string>("SvgStatusDeliveryProcessing"),
                                            }.Assign(out SvgStatus),
                                            new SkiaLabel()
                                            {
                                                Margin = new(0, 0, 0, 1),
                                                VerticalOptions = LayoutOptions.Center,
                                                TextColor = AppColors.Text,
                                                FontSize = 12,
                                                FontFamily = "FontTextSemiBold"
                                            }.Assign(out LabelStatus)
                                        }
                                    }
                                }.Assign(out FrameStatus),
                            }
                        },

                        // Review overlay (shown when completed)
                        new SkiaLayout()
                        {
                            IsVisible = false,
                            UseCache = SkiaCacheType.Image,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Start,
                            HeightRequest = 56,
                            Type = LayoutType.Row,
                            Padding = new(16, 0),
                            Background = AppColors.PrimaryLight,
                            Children = new List<SkiaControl>()
                            {
                                new SkiaSvg()
                                {
                                    SvgString = App.Current.Resources.Get<string>("SvgDuoThumb"),
                                    FontAwesomePrimaryColor = AppColors.TextSecondary,
                                    FontAwesomeSecondaryColor = AppColors.ControlPrimary,
                                    HeightRequest = 26,
                                    LockRatio = 1,
                                    VerticalOptions = LayoutOptions.Center,
                                },
                                new SkiaLabel()
                                {
                                    MaxLines = 2,
                                    LineBreakMode = LineBreakMode.TailTruncation,
                                    VerticalOptions = LayoutOptions.Center,
                                    FontSize = 15,
                                    FontFamily = AppFonts.SemiBold,
                                    Text = ResStrings.FinishedTitle
                                }
                            }
                        }.Assign(out LayoutReview),
                    }
                }
            }.Assign(out MainFrame),
        };
    }
    
    // Real data binding pattern from AppoMobi.Mobile
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        var dto = BindingContext as ServiceRequest;
        if (dto != null)
        {
            labelService.Text = $"{dto.Service.Name}";
            SetStatus(dto);
            SetHours(dto);
            SetCost(dto);
            SetTime(dto);
            labelFrom.Text = $"{ResStrings.FromSmall} {dto.Building.DisplayDescription}";
            SetAddressTo(dto);
            SetCargoDetails(dto);
        }
    }

    // Real property change handling pattern
    protected override void ContextPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        base.ContextPropertyChanged(sender, e);

        if (BindingContext is ServiceRequest model)
        {
            if (e.PropertyName == nameof(ServiceRequest.Notify))
            {
                MainFrame.BackgroundColor = model.Notify ?
                    AppColors.BackgroundNotify : AppColors.BackgroundSecondary;
            }
            else if (e.PropertyName == nameof(ServiceRequest.Rating))
            {
                SetStatus(model);
            }
            else if (e.PropertyName == nameof(ServiceRequest.Status))
            {
                SetStatus(model);
            }
            else if (e.PropertyName == nameof(ServiceRequest.Hours))
            {
                SetHours(model);
            }
            else if (e.PropertyName == nameof(ServiceRequest.FixedPrice))
            {
                SetCost(model);
            }
            else if (e.PropertyName.IsEither(nameof(ServiceRequest.Address),
                                           nameof(ServiceRequest.AddressSub)))
            {
                SetAddressTo(model);
            }
        }
    }

    // Helper methods for updating UI based on data
    private void SetStatus(ServiceRequest dto)
    {
        var status = dto.Status.Localize();
        LabelStatus.Text = status;

        if (dto.Status == CustomerRequestStatus.Complete ||
            dto.Status == CustomerRequestStatus.SlaveCanceled ||
            dto.Status == CustomerRequestStatus.Canceled ||
            dto.Status == CustomerRequestStatus.Rejected)
        {
            MainFrame.BackgroundColor = AppColors.BackgroundSecondary;
            FrameStatus.BackgroundColor = AppColors.Background;
            SvgStatus.SvgString = App.Current.Resources.Get<string>("SvgStatusDeliveryComplete");
            LayoutReview.IsVisible = dto.Status == CustomerRequestStatus.Complete && dto.Rating == 0;
        }
        else
        {
            MainFrame.BackgroundColor = AppColors.Background;
            FrameStatus.BackgroundColor = AppColors.PrimaryLight;
            SvgStatus.SvgString = App.Current.Resources.Get<string>("SvgStatusDeliveryProcessing");
            LayoutReview.IsVisible = false;
        }
    }

    private void SetAddressTo(ServiceRequest dto)
    {
        var address = $"{ResStrings.ToSmall} {dto.Address}";
        if (!string.IsNullOrEmpty(dto.AddressSub))
        {
            address += $", {ResStrings.FlatNbShort} {dto.AddressSub}";
        }
        labelTo.Text = address;
    }

    private void SetCost(ServiceRequest dto)
    {
        decimal price = dto.FixedPrice;
        labelPrice.Text = $"{price:0} ₽";
    }

    private void SetTime(ServiceRequest dto)
    {
        labelDate.Text = $"{dto.EditedTime.GetValueOrDefault().ToLocalTime().ToShortDateString()}";
    }
}
```

## Result
- Clean separation of UI logic and business logic
- Reusable custom controls with proper data binding
- Event-driven interactions with async support
- Programmatic control creation and configuration

## Variations

### 1. Real AppoMobi.Mobile Inline Assignment Pattern
```csharp
// Actual pattern from AppoMobi.Mobile project
public class MessageListCell : SkiaLayout
{
    private SkiaLabel LabelFirstDate;
    private SkiaLabel LabelMessage;
    private SkiaImage ImageUser;
    private SkiaButton ButtonAction;

    public MessageListCell()
    {
        // Real pattern: inline creation with Assign in Children collection
        var cellContent = new SkiaStack()
        {
            Spacing = 0,
            Children = new List<SkiaControl>()
            {
                // Date header
                new SkiaLabel()
                {
                    Margin = new Thickness(10, 4, 10, 8),
                    LineBreakMode = LineBreakMode.NoWrap,
                    MaxLines = 1,
                    FontFamily = AppFonts.Bold,
                    Text = "Time",
                    FontSize = 10,
                    HorizontalOptions = LayoutOptions.Center,
                    TextColor = AppColors.TextMinor,
                }.Assign(out LabelFirstDate),

                // Message row
                new SkiaRow()
                {
                    Spacing = 12,
                    Padding = new Thickness(16, 8, 16, 8),
                    Children = new List<SkiaControl>()
                    {
                        new SkiaImage()
                        {
                            WidthRequest = 44,
                            HeightRequest = 44,
                            CornerRadius = 22,
                            Aspect = TransformAspect.AspectCover,
                            UseCache = SkiaCacheType.Image,
                            BackgroundColor = AppColors.ImagePlaceholder
                        }.Assign(out ImageUser),

                        new SkiaStack()
                        {
                            Spacing = 4,
                            HorizontalOptions = LayoutOptions.Fill,
                            Children = new List<SkiaControl>()
                            {
                                new SkiaLabel()
                                {
                                    FontFamily = AppFonts.Regular,
                                    FontSize = 14,
                                    TextColor = AppColors.TextPrimary,
                                    LineBreakMode = LineBreakMode.WordWrap,
                                    HorizontalOptions = LayoutOptions.Fill
                                }.Assign(out LabelMessage),

                                new SkiaButton()
                                {
                                    Text = "Reply",
                                    FontFamily = AppFonts.Medium,
                                    FontSize = 12,
                                    WidthRequest = 60,
                                    HeightRequest = 28,
                                    BackgroundColor = AppColors.Primary,
                                    TextColor = Colors.White,
                                    CornerRadius = 14,
                                    HorizontalOptions = LayoutOptions.Start
                                }.Assign(out ButtonAction)
                            }
                        }
                    }
                }
            }
        };

        // Event handlers assigned after control creation
        ButtonAction.Clicked += OnReplyClicked;

        AddSubView(cellContent);
    }

    private async void OnReplyClicked(object sender, EventArgs e)
    {
        // Handle reply action
        await HandleReplyAsync();
    }
}
```

### 2. Real TabRequests Usage Pattern with ObserveBindingContext
```csharp
// Real pattern from TabRequests.cs showing how cells are used with data binding
public class TabRequests : SkiaLayout
{
    private SkiaCarousel Carousel;

    public TabRequests()
    {
        Children = new List<SkiaControl>()
        {
            new SkiaCarousel()
            {
                ItemsSource = new List<SkiaControl>()
                {
                    // Tab 0 - Active requests
                    new CellsScroll()
                    {
                        Tag = "Active",
                        RefreshCommand = Model.CommandRefresh,
                        RefreshEnabled = true,
                        Content = new CellsStack()
                        {
                            // Real ItemTemplate pattern - creates cells dynamically
                            ItemTemplate = new DataTemplate(() =>
                            {
                                var cell = new CellServiceRequest();
                                cell.AnimationTapped = SkiaTouchAnimation.Ripple;
                                cell.TouchEffectColor = AppColors.PrimaryLight;
                                cell.Tapped += (sender, args) =>
                                {
                                    Model.CommandRequestDetails.Execute(cell.BindingContext);
                                };
                                return cell;
                            })
                        }
                        // Real ObserveBindingContext pattern for reactive data binding
                        .ObserveBindingContext<SkiaLayout, MainPageViewModel>((me, vm, prop) =>
                        {
                            bool attached = prop == nameof(BindingContext);

                            if (attached || prop == nameof(vm.HasData))
                            {
                                if (vm.HasData)
                                    me.ItemsSource = vm.RequestsDataService.Items;
                            }

                            if (attached || prop == nameof(vm.HasError))
                            {
                                if (vm.HasError)
                                    me.ItemsSource = null;
                            }
                        }),
                    }
                    // Real scroll state observation pattern
                    .ObserveBindingContext<SkiaScroll, MainPageViewModel>((me, vm, prop) =>
                    {
                        bool attached = prop == nameof(BindingContext);

                        if (attached || prop == nameof(vm.IsRefreshing))
                        {
                            me.IsRefreshing = vm.IsRefreshing;
                        }
                    }),

                    // Tab 1 - Archive with similar pattern
                    new CellsScroll()
                    {
                        Tag = "Archive",
                        RefreshCommand = Model.CommandRefreshArchive,
                        RefreshEnabled = true,
                        Content = new CellsStack()
                        {
                            ItemTemplate = new DataTemplate(() =>
                            {
                                var cell = new CellServiceRequest();
                                cell.Tapped += (sender, args) =>
                                {
                                    Model.CommandRequestDetails.Execute(cell.BindingContext);
                                };
                                return cell;
                            })
                        }
                        .ObserveBindingContext<SkiaLayout, MainPageViewModel>((me, vm, prop) =>
                        {
                            bool attached = prop == nameof(BindingContext);

                            if (attached || prop == nameof(vm.HasLoadedArchive))
                            {
                                me.ItemsSource = vm.DataServiceArchive.Items;
                            }

                            if (attached || prop == nameof(vm.HasErrorArchive))
                            {
                                if (vm.HasError)
                                    me.ItemsSource = null;
                            }
                        }),
                    }
                }
            }
            // Real self-observation pattern for carousel state management
            .ObserveSelf((me, prop) =>
            {
                if (prop.IsEither(nameof(BindingContext), nameof(me.SelectedIndex)))
                {
                    Model.RequestFilterIndex = me.SelectedIndex;
                }
            })
            .Assign(out Carousel),
        };
    }
}
```

## Related Components
- **Also see**: FluentExtensions, SkiaControl, Custom Controls
- **Requires**: Understanding of C# events and async patterns
- **Patterns**: MVVM, Observer, Command patterns

## Common Mistakes

### ❌ Not disposing event handlers
```csharp
// Wrong - memory leak
public partial class LeakyControl : SkiaControl
{
    public LeakyControl()
    {
        SomeService.DataChanged += OnDataChanged;
        // Never unsubscribed!
    }
}
```

### ✅ Proper event lifecycle management
```csharp
// Correct - proper cleanup
public partial class ProperControl : SkiaControl, IDisposable
{
    public ProperControl()
    {
        SomeService.DataChanged += OnDataChanged;
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SomeService.DataChanged -= OnDataChanged;
        }
        base.Dispose(disposing);
    }
}
```

### ❌ Blocking UI thread with synchronous operations
```csharp
// Wrong - blocks UI
private void OnButtonClicked(object sender, EventArgs e)
{
    var result = LongRunningOperation(); // Blocks UI
    UpdateUI(result);
}
```

### ✅ Use async patterns for long operations
```csharp
// Correct - non-blocking
private async void OnButtonClicked(object sender, EventArgs e)
{
    var button = sender as SkiaButton;
    button.IsEnabled = false;
    
    try
    {
        var result = await LongRunningOperationAsync();
        UpdateUI(result);
    }
    finally
    {
        button.IsEnabled = true;
    }
}
```

## Tags
#code-behind #fluent-api #programmatic-creation #event-handling #mvvm #async-patterns #custom-controls #gestures #lifecycle-management #intermediate
