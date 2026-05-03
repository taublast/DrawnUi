using DrawnUi.Blazor.Views;
using DrawnUi.Infrastructure.Xaml;

namespace DrawnUi.Draw;

public partial class SkiaLayout
{
    #region GRID

    public int GetColumn(BindableObject bindable) => Grid.GetColumn(bindable);
    public int GetColumnSpan(BindableObject bindable) => Grid.GetColumnSpan(bindable);
    public int GetRow(BindableObject bindable) => Grid.GetRow(bindable);
    public int GetRowSpan(BindableObject bindable) => Grid.GetRowSpan(bindable);

    public void SetColumn(BindableObject bindable, int value) => Grid.SetColumn(bindable, value);
    public void SetColumnSpan(BindableObject bindable, int value) => Grid.SetColumnSpan(bindable, value);
    public void SetRow(BindableObject bindable, int value) => Grid.SetRow(bindable, value);
    public void SetRowSpan(BindableObject bindable, int value) => Grid.SetRowSpan(bindable, value);

    #endregion
}
