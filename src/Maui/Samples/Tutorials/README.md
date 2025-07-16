# DrawnUI Tutorials Project

This project contains working examples of all the tutorials from the DrawnUI documentation. Each tutorial is organized in its own folder with complete, runnable code.

## 🚀 Available Tutorials

### 1. **Interactive Cards Tutorial**
- **XAML Version** (`Tutorials/InteractiveCards/InteractiveCardsPage.xaml`)
- **Code-Behind Version** (`Tutorials/InteractiveCards/InteractiveCardsCodePage.xaml`)
- Beautiful animated cards with gesture interactions
- Demonstrates gradients, shadows, and smooth animations

### 2. **News Feed Tutorial**
- **Location**: `Tutorials/NewsFeed/`
- Advanced scrolling lists with mixed content types
- Demonstrates cell recycling, pull-to-refresh, and performance optimization
- Includes: Models, Services, ViewModels, and custom cells

### 3. **First App Tutorial**
- **Location**: `Tutorials/FirstApp/`
- Your first DrawnUI app with basic controls
- Demonstrates Canvas, SkiaLayout, SkiaLabel, and SkiaButton

### 4. **Button Tutorial**
- **Location**: `Tutorials.GameButton/`
- Game-style interactive buttons with visual effects
- Demonstrates bevel effects, gradients, and press animations

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
├── InteractiveCards/
│   ├── InteractiveCardsPage.xaml       # XAML version
│   ├── InteractiveCardsCodePage.xaml   # Code-behind version
├── NewsFeed/
│   ├── Models/
│   ├── Services/
│   ├── ViewModels/
│   ├── Views/
│   └── NewsFeedPage.xaml
├── FirstApp/
│   └── FirstAppPage.xaml
├── Button/
│   └── ButtonPage.xaml
└── README.md
```

## 📖 Documentation

Each tutorial corresponds to documentation in the `docs/articles/` folder:
- [Interactive Cards Tutorial](https://github.com/taublast/DrawnUi.Maui/blob/main/docs/articles/interactive-cards.md)
- [News Feed Tutorial](https://github.com/taublast/DrawnUi.Maui/blob/main/docs/articles/news-feed-tutorial.md)
- [First App Tutorial](https://github.com/taublast/DrawnUi.Maui/blob/main/docs/articles/first-app.md)
- [Interactive Button Tutorial](https://github.com/taublast/DrawnUi.Maui/blob/main/docs/articles/interactive-button.md)

## 🎯 Key Features Demonstrated

- **Performance Optimization**: Smart caching strategies and efficient rendering
- **Gesture Handling**: Touch interactions, panning, and tapping
- **Visual Effects**: Gradients, shadows, bevels, and animations
- **Layout Systems**: Flexible layouts with automatic sizing
- **Data Binding**: ViewModels, collections, and recycling
- **Cross-Platform**: Consistent UI across iOS, Android, and Windows

## 🚀 Next Steps

After exploring these tutorials, check out:
- [Controls Gallery](../Sandbox/) for more advanced examples
- [Documentation](https://github.com/taublast/DrawnUi.Maui/tree/main/docs) for detailed guides
- [API Reference](https://github.com/taublast/DrawnUi.Maui/tree/main/docs/api) for complete control documentation

Happy coding with DrawnUI! 🎉