using Microsoft.AspNetCore.Components;

namespace DrawnUi.Draw
{
    /// <summary>
    /// Blazor adds Razor LayoutComponentBase as base via partial declaration.
    /// Plain INotifyPropertyChanged + GetValue/SetValue logic lives in
    /// SharedNet/Draw/BindableObject.Plain.cs.
    /// </summary>
    public partial class BindableObject : LayoutComponentBase
    {
    }
}
