namespace Sandbox
{
    public partial class View1 : SkiaLayout
    {
        public View1()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
            }
        }
    }
}
