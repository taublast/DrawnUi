<!-- Component: Fluent API, Category: Programming/Advanced, Complexity: Advanced -->
# Fluent API Comprehensive Guide

## Scenario
DrawnUI's fluent API provides a powerful way to create and configure controls using method chaining. This approach enables clean, readable code that's easier to maintain than traditional object initialization. The fluent API includes property observation, gesture handling, layout configuration, and lifecycle management. Use this when building complex UIs programmatically or when you need dynamic, data-driven interfaces.

## Complete Working Example

### Real AppoMobi.Mobile Pattern - Inline Assignment
```csharp
using DrawnUi.Draw;

public class ChatMessageCell : SkiaLayout
{
    private SkiaLabel LabelTime;
    private SkiaLabel LabelMessage;
    private SkiaImage ImageAvatar;

    public ChatMessageCell()
    {
        // Real-world pattern: inline creation with Assign in Children collection
        var messageLayout = new SkiaStack()
        {
            Spacing = 8,
            Padding = new Thickness(16, 12, 16, 12),
            Children = new List<SkiaControl>()
            {
                // Time header
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
                }.Assign(out LabelTime),

                // Message content row
                new SkiaRow()
                {
                    Spacing = 12,
                    Children = new List<SkiaControl>()
                    {
                        new SkiaImage()
                        {
                            WidthRequest = 40,
                            HeightRequest = 40,
                            CornerRadius = 20,
                            Aspect = TransformAspect.AspectCover,
                            UseCache = SkiaCacheType.Image,
                            BackgroundColor = AppColors.ImagePlaceholder
                        }.Assign(out ImageAvatar),

                        new SkiaLabel()
                        {
                            FontFamily = AppFonts.Regular,
                            FontSize = 14,
                            TextColor = AppColors.TextPrimary,
                            LineBreakMode = LineBreakMode.WordWrap,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Start
                        }.Assign(out LabelMessage)
                    }
                }
            }
        };

        AddSubView(messageLayout);
    }
            
            // Content section with scroll
            new SkiaScroll()
            {
                Orientation = ScrollOrientation.Vertical,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            }
            .Assign(out contentScroll)
            .WithContent(
                new SkiaLayout()
                {
                    Type = LayoutType.Column,
                    Spacing = 16,
                    Padding = 16
                }
                .ObserveCollection(viewModel.Items, (layout, items) =>
                {
                    layout.Children.Clear();
                    foreach (var item in items)
                    {
                        layout.AddSubView(CreateDashboardItem(item));
                    }
                })
            )
        )
        .Initialize(dashboard =>
        {
            // Post-construction setup
            titleLabel.Text = viewModel.Title;
            refreshButton.CommandTapped = viewModel.RefreshCommand;
            
            // Setup scroll behavior
            contentScroll.Scrolled += (s, e) =>
            {
                if (e.ScrollY > 100)
                {
                    titleLabel.Opacity = 0.7;
                }
                else
                {
                    titleLabel.Opacity = 1.0;
                }
            };
        });
    }
    
    private static SkiaControl CreateDashboardItem(DashboardItem item)
    {
        return new SkiaLayout()
        {
            Type = LayoutType.Row,
            Spacing = 12,
            Padding = 16,
            BackgroundColor = Colors.LightGray,
            CornerRadius = 8
        }
        .WithChildren(
            new SkiaShape()
            {
                Type = ShapeType.Circle,
                WidthRequest = 40,
                HeightRequest = 40,
                BackgroundColor = item.StatusColor
            },
            
            new SkiaLayout()
            {
                Type = LayoutType.Column,
                Spacing = 4,
                HorizontalOptions = LayoutOptions.Fill
            }
            .WithChildren(
                new SkiaLabel(item.Title)
                    .WithFontSize(16)
                    .WithFontWeight(FontWeight.Bold),
                    
                new SkiaLabel(item.Description)
                    .WithFontSize(14)
                    .WithTextColor(Colors.Gray)
            ),
            
            new SkiaButton("View")
                .OnTapped(btn => NavigateToItem(item))
                .WithBackgroundColor(Colors.Blue)
                .WithTextColor(Colors.White)
                .WithCornerRadius(6)
                .Height(32)
                .Width(60)
        )
        .OnTapped(layout => NavigateToItem(item))
        .WithCache(SkiaCacheType.Operations);
    }
}
```

### Advanced Property Observation Patterns
```csharp
public class ObservationPatterns
{
    public static SkiaControl CreateObservableControl(UserViewModel viewModel)
    {
        SkiaLabel statusLabel;
        SkiaImage avatarImage;
        
        return new SkiaLayout()
        {
            Type = LayoutType.Column,
            Spacing = 12
        }
        .WithChildren(
            // Simple property observation
            new SkiaLabel()
                .Assign(out statusLabel)
                .Observe(viewModel, (label, prop) =>
                {
                    if (prop.IsEither(nameof(BindingContext), nameof(UserViewModel.Name)))
                    {
                        label.Text = $"Hello, {viewModel.Name}!";
                    }
                }),
                
            // Deep property observation
            new SkiaImage()
                .Assign(out avatarImage)
                .ObserveDeep<SkiaImage, UserViewModel, UserProfile, string>(
                    vm => vm.Profile,
                    nameof(UserViewModel.Profile),
                    profile => profile.AvatarUrl,
                    nameof(UserProfile.AvatarUrl),
                    (image, url) =>
                    {
                        image.Source = url;
                        image.IsVisible = !string.IsNullOrEmpty(url);
                    }
                ),
                
            // Self observation for two-way binding
            new SkiaEntry()
                .ObserveSelf((entry, prop) =>
                {
                    if (prop.IsEither(nameof(BindingContext), nameof(SkiaEntry.Text)))
                    {
                        viewModel.SearchText = entry.Text;
                    }
                })
                .Observe(viewModel, (entry, prop) =>
                {
                    if (prop.IsEither(nameof(BindingContext), nameof(UserViewModel.SearchText)))
                    {
                        if (entry.Text != viewModel.SearchText)
                        {
                            entry.Text = viewModel.SearchText;
                        }
                    }
                }),
                
            // Observe another control
            new SkiaLabel()
                .Observe(() => statusLabel, (label, prop) =>
                {
                    if (prop.IsEither(nameof(BindingContext), nameof(SkiaLabel.Text)))
                    {
                        label.Text = $"Status changed: {statusLabel.Text}";
                        label.TextColor = statusLabel.Text.Contains("Error") ? Colors.Red : Colors.Green;
                    }
                })
        );
    }
}
```

### Gesture and Animation Fluent Patterns
```csharp
public class InteractivePatterns
{
    public static SkiaControl CreateInteractiveCard(CardViewModel viewModel)
    {
        return new SkiaLayout()
        {
            Type = LayoutType.Column,
            Padding = 16,
            BackgroundColor = Colors.White,
            CornerRadius = 12
        }
        .WithShadow(0, 4, 8, Colors.Black, 0.1f)
        .OnTapped(async card =>
        {
            // Tap animation
            await card.ScaleTo(0.95, 100);
            await card.ScaleTo(1.0, 100);
            
            // Navigate or perform action
            await viewModel.SelectCardAsync();
        })
        .OnLongPressing(card =>
        {
            // Long press feedback
            card.BackgroundColor = Colors.LightBlue;
            viewModel.ShowContextMenu();
        })
        .WithGestures((card, args, apply) =>
        {
            // Real DrawnUI gesture handling with actual TouchActionResult values
            switch (args.Type)
            {
                case TouchActionResult.Down:
                    // Store initial position for panning
                    card.Tag = args.Event.Location;
                    return card;

                case TouchActionResult.Panning:
                    if (card.Tag is SKPoint startPoint)
                    {
                        var deltaX = args.Event.Location.X - startPoint.X;
                        card.TranslationX = deltaX;
                        card.Rotation = Math.Max(-15, Math.Min(15, deltaX / 20));
                    }
                    return card;

                case TouchActionResult.Up:
                    if (card.Tag is SKPoint startPoint)
                    {
                        var deltaX = args.Event.Location.X - startPoint.X;
                        if (Math.Abs(deltaX) > 100)
                        {
                            // Swipe to dismiss
                            _ = DismissCard(card, deltaX > 0);
                        }
                        else
                        {
                            // Return to position
                            _ = ReturnCardToPosition(card);
                        }
                        card.Tag = null;
                    }
                    return null; // Don't consume UP gesture

                default:
                    return null;
            }
        })
        .WithChildren(
            new SkiaLabel(viewModel.Title)
                .WithFontSize(18)
                .WithFontWeight(FontWeight.Bold),
                
            new SkiaLabel(viewModel.Description)
                .WithFontSize(14)
                .WithTextColor(Colors.Gray)
        );
    }
    
    private static async Task DismissCard(SkiaControl card, bool swipeRight)
    {
        var targetX = swipeRight ? 400 : -400;
        await Task.WhenAll(
            card.TranslateTo(targetX, 0, 300),
            card.FadeTo(0, 300),
            card.RotateTo(swipeRight ? 30 : -30, 300)
        );
        
        // Remove from parent
        (card.Parent as SkiaLayout)?.RemoveSubView(card);
    }
    
    private static async Task ReturnCardToPosition(SkiaControl card)
    {
        await Task.WhenAll(
            card.TranslateTo(0, 0, 250, Easing.SpringOut),
            card.RotateTo(0, 250),
            card.FadeTo(1, 250)
        );
    }
}
```

## Result
- Clean, readable UI construction code
- Automatic property observation and updates
- Efficient event handling and gesture management
- Proper resource cleanup and lifecycle management

## Variations

### 1. Layout Builder Pattern
```csharp
public class LayoutBuilder
{
    public static SkiaLayout CreateResponsiveGrid(int columns, IEnumerable<object> items)
    {
        return new SkiaLayout()
        {
            Type = LayoutType.Grid,
            ColumnDefinitions = string.Join(",", Enumerable.Repeat("*", columns))
        }
        .Adapt(grid =>
        {
            var itemIndex = 0;
            foreach (var item in items)
            {
                var row = itemIndex / columns;
                var col = itemIndex % columns;
                
                var itemControl = CreateGridItem(item)
                    .WithRow(row)
                    .WithColumn(col);
                    
                grid.AddSubView(itemControl);
                itemIndex++;
            }
        });
    }
}
```

### 2. Conditional Configuration
```csharp
public static SkiaButton CreateConditionalButton(ButtonConfig config)
{
    return new SkiaButton(config.Text)
        .Adapt(btn =>
        {
            if (config.IsPrimary)
            {
                btn.BackgroundColor = Colors.Blue;
                btn.TextColor = Colors.White;
            }
            else
            {
                btn.BackgroundColor = Colors.Transparent;
                btn.TextColor = Colors.Blue;
                btn.StrokeColor = Colors.Blue;
                btn.StrokeWidth = 1;
            }
            
            if (config.IsLarge)
            {
                btn.HeightRequest = 56;
                btn.FontSize = 18;
            }
            else
            {
                btn.HeightRequest = 40;
                btn.FontSize = 14;
            }
        })
        .OnTapped(config.Action)
        .WithCache(SkiaCacheType.Operations);
}
```

## Related Components
- **Also see**: SkiaControl, Property Observation, Gesture Handling
- **Requires**: Understanding of C# method chaining and lambda expressions
- **Extensions**: FluentExtensions class provides all fluent methods

## Common Mistakes

### ❌ Using non-existent gesture methods
```csharp
// WRONG - OnPanning doesn't exist in DrawnUI!
var button = new SkiaButton("Test")
    .OnPanning((btn, args) => { }); // COMPILATION ERROR!
```

### ✅ Use real DrawnUI gesture methods
```csharp
// Correct - only OnTapped and OnLongPressing exist
var button = new SkiaButton("Test")
    .OnTapped(btn => DoSomething())
    .OnLongPressing(btn => ShowContextMenu());

// For complex gestures, use WithGestures
var layout = new SkiaLayout()
    .WithGestures((layout, args, apply) =>
    {
        switch (args.Type)
        {
            case TouchActionResult.Down:
                return layout; // Consume gesture
            case TouchActionResult.Panning:
                // Handle panning
                return layout;
            case TouchActionResult.Up:
                return null; // Don't consume UP
            default:
                return null;
        }
    });
```

### ❌ Not using Assign for references
```csharp
// Wrong - can't access control later
var layout = new SkiaLayout()
    .WithChildren(
        new SkiaLabel("Status") // No way to access this later
    );
```

### ✅ Use Assign for control references
```csharp
// Correct - can access assigned controls
SkiaLabel statusLabel;

var layout = new SkiaLayout()
    .WithChildren(
        new SkiaLabel("Status")
            .Assign(out statusLabel)
    )
    .Initialize(l =>
    {
        // Can access statusLabel here
        statusLabel.TextColor = Colors.Red;
    });
```

## Tags
#fluent-api #method-chaining #property-observation #programmatic-ui #advanced #clean-code #mvvm #real-gestures #ontapped #onlongpressing #withgestures #touchactionresult #lifecycle-management
