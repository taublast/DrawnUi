namespace Sandbox
{
    public partial class AppShell : Shell, IDisposable
    {
        public AppShell()
        {
            InitializeComponent();
        }

        public void Dispose()
        {
            if (this.CurrentPage is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
