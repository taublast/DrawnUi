using DrawnUi.Controls;
using DrawnUi.Draw;
using Xunit;

namespace UnitTests
{
    /// <summary>
    /// Style content is built lazily at first measure. It must not override layout/cache
    /// properties the user set explicitly (regression: SkiaSlider Cupertino forced HorizontalOptions=Fill,
    /// a Center-aligned 150pt slider landed flush left).
    /// </summary>
    public class StyleDefaultsTests : DrawnTestsBase
    {
        [Theory]
        [InlineData(PrebuiltControlStyle.Cupertino)]
        [InlineData(PrebuiltControlStyle.Material)]
        [InlineData(PrebuiltControlStyle.Windows)]
        [InlineData(PrebuiltControlStyle.Unset)]
        public void SliderStyle_KeepsUserAlignmentAndCache(PrebuiltControlStyle style)
        {
            var slider = new SkiaSlider
            {
                ControlStyle = style,
                WidthRequest = 150,
                HorizontalOptions = LayoutOptions.Center,
                UseCache = SkiaCacheType.Operations,
            };

            slider.CommitInvalidations();
            slider.Measure(400, 100, 1);

            Assert.Equal(LayoutAlignment.Center, slider.HorizontalOptions.Alignment);
            Assert.Equal(SkiaCacheType.Operations, slider.UseCache);
            Assert.True(slider.Views.Count > 0, "style content was not created");
        }

        [Fact]
        public void SliderStyle_AppliesDefaultsWhenUserDidNotSet()
        {
            var slider = new SkiaSlider { ControlStyle = PrebuiltControlStyle.Cupertino };
            slider.CommitInvalidations();
            slider.Measure(400, 100, 1);

            Assert.Equal(LayoutAlignment.Fill, slider.HorizontalOptions.Alignment);
            Assert.Equal(64, slider.MinimumWidthRequest);
        }

        [Theory]
        [InlineData(typeof(SkiaSwitch))]
        [InlineData(typeof(SkiaCheckbox))]
        [InlineData(typeof(SkiaRadioButton))]
        public void ToggleStyle_KeepsUserColor(System.Type type)
        {
            var toggle = (SkiaToggle)System.Activator.CreateInstance(type);
            toggle.ControlStyle = PrebuiltControlStyle.Cupertino;
            toggle.ColorFrameOn = Colors.Red;
            toggle.ColorThumbOn = Colors.Red;

            toggle.CommitInvalidations();
            toggle.Measure(200, 60, 1);

            Assert.Equal(Colors.Red, toggle.ColorFrameOn);
            Assert.Equal(Colors.Red, toggle.ColorThumbOn);
            Assert.True(toggle.Views.Count > 0, "style content was not created");
        }
    }
}
