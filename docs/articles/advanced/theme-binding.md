# Theme Bindings

### 📊 **Usage Examples**

#### XAML:
```xml
<draw:SkiaLabel TextColor="{draw:ThemeBinding Light=Red, Dark=Blue}" />
```

#### Code-Behind:
```csharp
// Method 1: Direct binding
ThemeBindings.SetThemeBinding(
    myLabel, SkiaLabel.TextColorProperty, Colors.Red, Colors.Blue);

// Method 2: Fluent syntax
myLabel.WithThemeBinding(SkiaLabel.TextColorProperty, Colors.Red, Colors.Blue)
       .WithThemeBinding(SkiaLabel.FontSizeProperty, 16.0, 18.0);

// Method 3: Get value without binding
var color = ThemeBindings.GetThemeValue(Colors.Red, Colors.Blue);
```

#### Custom Theme Provider (Blazor):
```csharp
// Set custom theme provider for Blazor
ThemeBindingManager.SetThemeProvider(new BlazorThemeProvider());
```

### 🔧 **Diagnostics & Monitoring**

```csharp
// Check active binding count
var activeBindings = ThemeBindingManager.ActiveBindingCount;

// Force cleanup
ThemeBindingManager.Cleanup();

// Force update all bindings
ThemeBindingManager.UpdateAllBindings();
```


 
 