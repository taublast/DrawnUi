namespace DrawnUi.Draw
{

    public partial class SkiaLabel
    {

        public static readonly BindableProperty FormattedTextProperty = BindableProperty.Create(
            nameof(FormattedText),
            typeof(FormattedString),
            typeof(SkiaLabel),
            defaultValue: null,
            propertyChanged: NeedInvalidateMeasure);

        public FormattedString FormattedText
        {
            get { return (FormattedString)GetValue(FormattedTextProperty); }
            set { SetValue(FormattedTextProperty, value); }
        }

        //const string TypicalFontAssetsPath = "../Fonts/";

    }
}
