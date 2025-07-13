namespace Sandbox.Views
{
    public partial class MainPageMarkdown : BasePageCodeBehind
    {

        public MainPageMarkdown()
        {
            try
            {
                InitializeComponent();

                LoadData().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
            }
        }

        private void OnLinkTapped(object sender, string link)
        {
            Super.OpenLink(link);
        }

        public async Task LoadData()
        {

            try
            {
                IsBusy = true;

                using var stream = await FileSystem.OpenAppPackageFileAsync("Markdown.md");
                using var reader = new StreamReader(stream);
                var text = await reader.ReadToEndAsync();

                LabelMarkdown.Text = text;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                IsBusy = false;
            }


        }

    }
}
