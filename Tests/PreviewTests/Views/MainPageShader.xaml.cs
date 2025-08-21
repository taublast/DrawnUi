using AppoMobi.Maui.Gestures;
using DrawnUi.Infrastructure;

namespace PreviewTests.Views
{
    public partial class MainPageShader 
    {

        public MainPageShader()
        {
            try
            {
                InitializeComponent();

                //avoid setting context BEFORE InitializeComponent, can bug 
                //having parent BindingContext still null when constructing from xaml
                BindingContext = new MainPageViewModel();

            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
                Console.WriteLine(e);
            }
        }

        void Test()
        {
            string shaderCode = SkSl.LoadFromResources($"{MauiProgram.ShadersFolder}/blur.sksl");
            var effect = SkSl.Compile(shaderCode);
        }



    }
}
