namespace DrawnUi.Infrastructure.Models
{

	public class BitmapLoadedEventArgs : ContentLoadedEventArgs
	{
		public BitmapLoadedEventArgs(string content, SKBitmap bitmap) : base(content)
		{
			Bitmap = bitmap;
		}
		public SKBitmap Bitmap { private set; get; }
	}

	public class ContentLoadedEventArgs : EventArgs
	{
		public ContentLoadedEventArgs(string content)
		{
			Content = content;
		}
		public string Content { private set; get; }
	}
}