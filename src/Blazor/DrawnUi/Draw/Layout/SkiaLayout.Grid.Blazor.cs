//Adapted code from the Xamarin.Forms Grid implementation

using System.ComponentModel;
using DrawnUi.Blazor.Views;
using DrawnUi.Infrastructure.Xaml;

namespace DrawnUi.Draw;

public partial class SkiaLayout
{
    #region GRID

    public int GetColumn(BindableObject bindable)
    {
        return Grid.GetColumn(bindable);
    }

    public int GetColumnSpan(BindableObject bindable)
    {
        return Grid.GetColumnSpan(bindable);
    }

    public int GetRow(BindableObject bindable)
    {
        return Grid.GetRow(bindable);
    }

    public int GetRowSpan(BindableObject bindable)
    {
        return Grid.GetRowSpan(bindable);
    }

    public void SetColumn(BindableObject bindable, int value)
    {
        Grid.SetColumn(bindable, value);
    }

    public void SetColumnSpan(BindableObject bindable, int value)
    {
        Grid.SetColumnSpan(bindable, value);
    }

    public void SetRow(BindableObject bindable, int value)
    {
        Grid.SetRow(bindable, value);
    }

    public void SetRowSpan(BindableObject bindable, int value)
    {
        Grid.SetRowSpan(bindable, value);
    }


    #endregion


}
