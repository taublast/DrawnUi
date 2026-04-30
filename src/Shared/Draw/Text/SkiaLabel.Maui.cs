namespace DrawnUi.Draw
{

    public partial class SkiaLabel
    {

        private static IFontRegistrar _registrar;
        public static IFontRegistrar FontRegistrar
        {
            get
            {
                if (_registrar == null)
                {
                    _registrar = Super.Services.GetService<IFontRegistrar>();
                }
                return _registrar;
            }
        }

        const string TypicalFontAssetsPath = "../Fonts/";

    }
}
