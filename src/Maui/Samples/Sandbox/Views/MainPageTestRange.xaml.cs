using DrawnUi.Draw;

namespace Sandbox.Views.Controls;

public partial class MainPageTestRange 
{
    public MainPageTestRange()
    {
        InitializeComponent();
        
        // Test backward compatibility events
        TestSlider.EndChanged += (s, value) =>
        {
            System.Diagnostics.Debug.WriteLine($"Slider EndChanged: {value}");
        };

        //TestSlider.ValueChanged += (s, value) =>
        //{
        //    System.Diagnostics.Debug.WriteLine($"Slider ValueChanged: {value}");
        //};

        // Test ranged slider events
        RangedSlider.StartChanged += (s, value) =>
        {
            System.Diagnostics.Debug.WriteLine($"RangedSlider StartChanged: {value}");
        };

        RangedSlider.EndChanged += (s, value) =>
        {
            System.Diagnostics.Debug.WriteLine($"RangedSlider EndChanged: {value}");
        };
        
        // Test progress controls
        ProgressDefault.ValueChanged += (s, value) => 
        {
            System.Diagnostics.Debug.WriteLine($"Progress ValueChanged: {value}");
        };
        
        // Button handlers
        BtnIncrease.Tapped += (s, e) =>
        {
            TestSlider.End = Math.Min(TestSlider.Max, TestSlider.End + 10);
            RangedSlider.End = Math.Min(RangedSlider.Max, RangedSlider.End + 5);
            RangedSlider.Start = Math.Min(RangedSlider.Start + 2, RangedSlider.End - 10);
            ProgressDefault.Value = Math.Min(ProgressDefault.Max, ProgressDefault.Value + 10);
            ProgressCupertino.Value = Math.Min(ProgressCupertino.Max, ProgressCupertino.Value + 10);
            ProgressMaterial.Value = Math.Min(ProgressMaterial.Max, ProgressMaterial.Value + 10);
            ProgressWindows.Value = Math.Min(ProgressWindows.Max, ProgressWindows.Value + 10);
            ProgressCustom.Value = Math.Min(ProgressCustom.Max, ProgressCustom.Value + 10);
        };

        BtnDecrease.Tapped += (s, e) =>
        {
            TestSlider.End = Math.Max(TestSlider.Min, TestSlider.End - 10);
            RangedSlider.End = Math.Max(RangedSlider.End - 5, RangedSlider.Start + 10);
            RangedSlider.Start = Math.Max(RangedSlider.Min, RangedSlider.Start - 2);
            ProgressDefault.Value = Math.Max(ProgressDefault.Min, ProgressDefault.Value - 10);
            ProgressCupertino.Value = Math.Max(ProgressCupertino.Min, ProgressCupertino.Value - 10);
            ProgressMaterial.Value = Math.Max(ProgressMaterial.Min, ProgressMaterial.Value - 10);
            ProgressWindows.Value = Math.Max(ProgressWindows.Min, ProgressWindows.Value - 10);
            ProgressCustom.Value = Math.Max(ProgressCustom.Min, ProgressCustom.Value - 10);
        };
    }
}
