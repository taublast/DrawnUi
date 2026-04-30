using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;


namespace DrawnUi.Views
{
    public static class XamlExtensions
    {

    }

    public class VisualElement : View, IDisposable
    {
        [Parameter]
        public string Units { get; set; } = "px";

        protected override void OnParametersSet()
        {
            var cssForGrid = "";
            var col = 0;
            var row = 0;
            if (GridColumn != null)
            {
                cssForGrid += $" grid-column-start: {GridColumn + 1};";
                col = GridColumn.Value;
            }
            if (GridRow != null)
            {
                cssForGrid += $" grid-row-start: {GridRow + 1};";
                row = GridRow.Value;
            }
            if (GridColumnSpan != null)
            {
                cssForGrid += $" grid-column-end: {col + GridColumnSpan + 1};";
            }
            if (GridRowSpan != null)
            {
                cssForGrid += $" grid-row-end: {row + GridRowSpan + 1};";
            }

            CssGridPosition = cssForGrid;

            base.OnParametersSet();
        }

        protected string CssGridPosition { get; set; }

        [Parameter]
        public int? GridColumnSpan { get; set; }

        [Parameter]
        public int? GridRowSpan { get; set; }

        [Parameter]
        public int? GridColumn { get; set; }

        [Parameter]
        public int? GridRow { get; set; }

        [Parameter]
        public new virtual LayoutOptions HorizontalOptions { get; set; } = LayoutOptions.Start;

        [Parameter]
        public new virtual LayoutOptions VerticalOptions { get; set; } = LayoutOptions.Start;

        [Parameter]
        public new virtual double WidthRequest { get; set; } = -1.0;

        [Parameter]
        public new virtual double HeightRequest { get; set; } = -1.0;

        protected string CssLayoutAlignment
        {
            get
            {
                var ret = "";
                var hAlign = HorizontalOptions.Alignment;
                var vAlign = VerticalOptions.Alignment;

                var width = " width: fit-content;";
                var height = " height: fit-content;";

                if (WidthRequest >= 0)
                {
                    width = $"width: {WidthRequest.ToString(System.Globalization.CultureInfo.InvariantCulture)}{Units};";
                }
                else
                {
                    if (hAlign == LayoutAlignment.Fill)
                    {
                        width = $"width: initial;";
                    }
                }

                if (HeightRequest >= 0)
                {
                    height = $"height: {HeightRequest.ToString(System.Globalization.CultureInfo.InvariantCulture)}{Units};";
                }
                else
                {
                    if (vAlign == LayoutAlignment.Fill)
                    {
                        height = $"height: 100%;";
                    }
                }

                if (hAlign == LayoutAlignment.Start || hAlign == LayoutAlignment.Fill)
                {
                    ret += width;
                }
                else if (hAlign == LayoutAlignment.Center)
                {
                    ret += "margin-left: auto; margin-right: auto;" + width;
                }
                else if (hAlign == LayoutAlignment.End)
                {
                    ret += "margin-left: auto;" + width;
                }

                if (vAlign == LayoutAlignment.Start || vAlign == LayoutAlignment.Fill)
                {
                    ret += height;
                }
                else if (vAlign == LayoutAlignment.Center)
                {
                    ret += "margin-top: auto; margin-bottom: auto;" + height;
                }
                else if (vAlign == LayoutAlignment.End)
                {
                    ret += "margin-top: auto;" + height;
                }

                return ret;
            }
        }

        protected string CssMargins
        {
            get
            {
                var m = Margin;
                if (m == default)
                    return string.Empty;
                return $"margin: {m.Top.ToString(System.Globalization.CultureInfo.InvariantCulture)}{Units} {m.Right.ToString(System.Globalization.CultureInfo.InvariantCulture)}{Units} {m.Bottom.ToString(System.Globalization.CultureInfo.InvariantCulture)}{Units} {m.Left.ToString(System.Globalization.CultureInfo.InvariantCulture)}{Units};";
            }
        }

        protected string CssPadding
        {
            get
            {
                var p = Padding;
                if (p == default)
                    return string.Empty;
                return $"padding: {p.Top.ToString(System.Globalization.CultureInfo.InvariantCulture)}{Units} {p.Right.ToString(System.Globalization.CultureInfo.InvariantCulture)}{Units} {p.Bottom.ToString(System.Globalization.CultureInfo.InvariantCulture)}{Units} {p.Left.ToString(System.Globalization.CultureInfo.InvariantCulture)}{Units};";
            }
        }

        [Parameter]
        public string Class { get; set; }

        [Parameter]
        public new string Style { get; set; }

        [Parameter]
        public new bool IsVisible { get; set; } = true;

        protected override bool ShouldRender()
        {
            return IsVisible && base.ShouldRender();
        }

        public bool Not(bool value)
        {
            return !value;
        }

        public string Uid { get; protected set; } = Guid.NewGuid().ToString();

        public async void Update()
        {
            await InvokeAsync(() =>
            {
                StateHasChanged();
            });
        }

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            //Update();
            base.OnPropertyChanged(propertyName);
        }

        public virtual void OnDisposing()
        {
        }

        public void Dispose()
        {
            OnDisposing();
        }

        public MarkupString ClassUid
        {
            get
            {
                return new MarkupString($"xe{Uid.Replace("-", "")}");
            }
        }

        public MarkupString ClassUidStyle
        {
            get
            {
                return new MarkupString($".xe{Uid.Replace("-", "")}");
            }
        }

        // Frame for GetIsVisibleWithParent compatibility
        public virtual SKRect Frame { get; protected set; }
    }
}
