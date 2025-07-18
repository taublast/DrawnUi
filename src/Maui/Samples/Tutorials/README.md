# DrawnUI Tutorials Project

This project contains working examples of all the tutorials from the DrawnUI documentation. Each tutorial is organized in its own folder with complete, runnable code.

## 🚀 Available Tutorials

### **First App Tutorial**
- **XAML Version** (`Tutorials/FirstApp/FirstAppPage.xaml`)
- **C# Fluent Version** (`Tutorials/FirstApp/FirstAppPageCode.cs`)
- Your first DrawnUI app with basic controls
- Demonstrates Canvas, SkiaLayout, SkiaLabel, and SkiaButton
- Perfect starting point for beginners

### **Interactive Cards Tutorial**
- **Location**: `Tutorials/InteractiveCards/TutorialCards.xaml`
- Beautiful animated cards with gesture interactions
- Demonstrates gradients, shadows, and smooth animations
- Perfect for learning visual effects and touch handling

### **Custom Drawn Control Tutorial**
- **Location**: `Tutorials/CustomButton/`
- **Advanced tutorial** teaching how to create custom drawn controls
- Uses a game-style button as example with `GameButton.cs` class
- Demonstrates bindable properties, visual effects, and interactive animations
- Features bevel effects, gradients, optional accessory images (like animated GIFs)

### **News Feed Scroller Tutorial**
- **Location**: `Tutorials/NewsFeed/`
- Advanced scrolling lists with mixed content types
- Demonstrates cell recycling, pull-to-refresh, and performance optimization
- Includes: Models, Services, ViewModels, and custom cells
- Real internet images and infinite scroll implementation

## 🛠️ How to Run

1. Clone the repository
2. Navigate to `src/Maui/Samples/Tutorials/`
3. Build and run the project:
   ```bash
   dotnet build
   dotnet run
   ```

## 📁 Project Structure

```
Tutorials/
├── FirstApp/
│   ├── FirstAppPage.xaml              # XAML version
│   ├── FirstAppPage.xaml.cs           # Code-behind
│   └── FirstAppPageCode.cs            # C# fluent version
├── CustomButton/
│   ├── ButtonPage.xaml                # Demo page
│   ├── ButtonPage.xaml.cs             # Demo code-behind
│   └── GameButton.cs                  # Custom control implementation
├── InteractiveCards/
│   ├── TutorialCards.xaml             # Interactive cards demo
│   └── TutorialCards.xaml.cs          # Code-behind
├── NewsFeed/
│   ├── Models/                        # Data models
│   ├── Services/                      # Data providers
│   ├── ViewModels/                    # MVVM ViewModels
│   ├── NewsCell.xaml                  # Custom cell
│   ├── NewsCell.xaml.cs               # Cell code-behind
│   ├── NewsFeedPage.xaml              # Main page
│   └── NewsFeedPage.xaml.cs           # Page code-behind
└── README.md
```

## 📖 Documentation

Each tutorial corresponds to documentation in the `docs/articles/` folder:
- [First App Tutorial (XAML)](https://github.com/taublast/DrawnUi.Maui/blob/main/docs/articles/first-app.md)
- [First App Tutorial (C# Fluent)](https://github.com/taublast/DrawnUi.Maui/blob/main/docs/articles/first-app-code.md)
- [Creating Custom Drawn Controls](https://github.com/taublast/DrawnUi.Maui/blob/main/docs/articles/interactive-button.md)
- [Interactive Cards Tutorial](https://github.com/taublast/DrawnUi.Maui/blob/main/docs/articles/interactive-cards.md)
- [News Feed Scroller Tutorial](https://github.com/taublast/DrawnUi.Maui/blob/main/docs/articles/news-feed-tutorial.md)

## 🎓 Learning Path

### **Recommended Order**
1. **Start with First App** - Learn the basics with either XAML or C# fluent approach
2. **Try Interactive Cards** - Explore visual effects and animations
3. **Build Custom Controls** - Advanced tutorial on creating your own drawn controls
4. **Master News Feed** - Complex real-world scenario with performance optimization

**Ready to draw your own UI?** Start with the First App tutorial and work your way up! 🎨

Happy coding with DrawnUI! 🎉