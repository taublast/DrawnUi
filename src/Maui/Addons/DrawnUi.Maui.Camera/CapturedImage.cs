namespace DrawnUi.Camera
{
    /// <summary>
    /// This bitmaps comes zoomed with hardware and NOT zoomed with TextureScale, you nave to do it yourself
    /// </summary>
    public class CapturedImage : IDisposable
    {
        public SKImage Image { get; set; }

        public Metadata Meta { get; set; }

        public int Rotation { get; set; }

        public CameraPosition Facing { get; set; }

        /// <summary>
        /// Device local time will be set
        /// </summary>
        public DateTime Time { get; set; }

        public string Path { get; set; }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                Image?.Dispose();
                Image = null;
            }
        }

        public bool IsDisposed { get; protected set; }

        /// <summary>
        /// Applies EXIF orientation correction to the current instance and disposes old image, uses Meta.Orientation.
        /// This phisically rotates the image, heavy impact on performance, use only when really necessary.
        /// </summary>
        public void SolveExifOrientation()
        {
            var orientation = this.Meta?.Orientation ?? 1;

            if (orientation == 1)
                return;

            var oldImage = this.Image;

            using var bitmap = SKBitmap.FromImage(oldImage);
            using var correctedBitmap = ApplyExifOrientation(bitmap, orientation);
            this.Image = SKImage.FromBitmap(correctedBitmap);

            oldImage?.Dispose();

            if (this.Meta != null)
                this.Meta.Orientation = 1;

            this.Rotation = 0;
        }

        /// <summary>
        /// Creates clone with corrected EXIF orientation
        /// </summary>
        /// <returns></returns>
        public CapturedImage CreateRotatedClone()
        {
            var orientation = this.Meta?.Orientation ?? 1;

            using var bitmap = SKBitmap.FromImage(this.Image);
            using var correctedBitmap = ApplyExifOrientation(bitmap, orientation);
            var correctedImage = SKImage.FromBitmap(correctedBitmap);

            var clone = new CapturedImage
            {
                Image = correctedImage,
                Meta = this.Meta ?? new Metadata(),
                Rotation = 0,
                Facing = this.Facing,
                Time = this.Time,
                Path = this.Path
            };

            clone.Meta.Orientation = 1;
            clone.Rotation = 0;

            return clone;
        }

        /// <summary>
        /// Applies EXIF orientation correction to bitmap
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="orientation"></param>
        /// <returns></returns>
        public SKBitmap ApplyExifOrientation(SKBitmap bitmap, int orientation)
        {
            switch (orientation)
            {
                case 1: // Normal
                    return bitmap.Copy();

                case 2: // Flip horizontal
                {
                    var flipped = new SKBitmap(bitmap.Width, bitmap.Height);
                    using var canvas = new SKCanvas(flipped);
                    canvas.Scale(-1, 1, bitmap.Width / 2.0f, bitmap.Height / 2.0f);
                    canvas.DrawBitmap(bitmap, 0, 0);
                    return flipped;
                }

                case 3: // Rotate 180°
                {
                    var rotated = new SKBitmap(bitmap.Width, bitmap.Height);
                    using var canvas = new SKCanvas(rotated);
                    canvas.RotateDegrees(180, bitmap.Width / 2.0f, bitmap.Height / 2.0f);
                    canvas.DrawBitmap(bitmap, 0, 0);
                    return rotated;
                }

                case 4: // Flip vertical
                {
                    var flipped = new SKBitmap(bitmap.Width, bitmap.Height);
                    using var canvas = new SKCanvas(flipped);
                    canvas.Scale(1, -1, bitmap.Width / 2.0f, bitmap.Height / 2.0f);
                    canvas.DrawBitmap(bitmap, 0, 0);
                    return flipped;
                }

                case 5: // Rotate 90° CW + flip horizontal
                {
                    var transformed = new SKBitmap(bitmap.Height, bitmap.Width);
                    using var canvas = new SKCanvas(transformed);
                    canvas.Translate(transformed.Width, 0);
                    canvas.RotateDegrees(90);
                    canvas.Scale(-1, 1, bitmap.Width / 2.0f, bitmap.Height / 2.0f);
                    canvas.DrawBitmap(bitmap, 0, 0);
                    return transformed;
                }

                case 6: // Rotate 90° CW
                {
                    var rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                    using var canvas = new SKCanvas(rotated);
                    canvas.Translate(rotated.Width, 0);
                    canvas.RotateDegrees(90);
                    canvas.DrawBitmap(bitmap, 0, 0);
                    return rotated;
                }

                case 7: // Rotate 90° CCW + flip horizontal
                {
                    var transformed = new SKBitmap(bitmap.Height, bitmap.Width);
                    using var canvas = new SKCanvas(transformed);
                    canvas.Translate(0, transformed.Height);
                    canvas.RotateDegrees(270);
                    canvas.Scale(-1, 1, bitmap.Width / 2.0f, bitmap.Height / 2.0f);
                    canvas.DrawBitmap(bitmap, 0, 0);
                    return transformed;
                }

                case 8: // Rotate 90° CCW
                {
                    var rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                    using var canvas = new SKCanvas(rotated);
                    canvas.Translate(0, rotated.Height);
                    canvas.RotateDegrees(270);
                    canvas.DrawBitmap(bitmap, 0, 0);
                    return rotated;
                }

                default:
                    return bitmap.Copy();
            }
        }

        public SKSurface GetFlatImage(SKPaint paint=null)
        {
            var orientation = this.Meta.Orientation;
            using var bitmap = SKBitmap.FromImage(Image);

            var width = bitmap.Width;
            var height = bitmap.Height;

            SKSurface surface;
            var info = new SKImageInfo(width, height);

            switch (orientation)
            {
 
                case 2: // Flip horizontal
                {
                    surface = SKSurface.Create(info);
                    surface.Canvas.Scale(-1, 1, bitmap.Width / 2.0f, bitmap.Height / 2.0f);
                    surface.Canvas.DrawBitmap(bitmap, 0, 0);
                    return surface;
                }

                case 3: // Rotate 180°
                {
                    surface = SKSurface.Create(info);
                    surface.Canvas.RotateDegrees(180, bitmap.Width / 2.0f, bitmap.Height / 2.0f);
                    surface.Canvas.DrawBitmap(bitmap, 0, 0);
                    return surface;
                }

                case 4: // Flip vertical
                {
                    surface = SKSurface.Create(info);
                    surface.Canvas.Scale(1, -1, bitmap.Width / 2.0f, bitmap.Height / 2.0f);
                    surface.Canvas.DrawBitmap(bitmap, 0, 0);
                    return surface;
                }

                case 5: // Rotate 90° CW + flip horizontal
                {
                    info = new SKImageInfo(height, width);
                    surface = SKSurface.Create(info);

                    surface.Canvas.Translate(info.Width, 0);
                    surface.Canvas.RotateDegrees(90);
                    surface.Canvas.Scale(-1, 1, bitmap.Width / 2.0f, bitmap.Height / 2.0f);
                    surface.Canvas.DrawBitmap(bitmap, 0, 0);
                    return surface;
                }

                case 6: // Rotate 90° CW
                {
                    info = new SKImageInfo(height, width);
                    surface = SKSurface.Create(info);
                    surface.Canvas.Translate(info.Width, 0);
                    surface.Canvas.RotateDegrees(90);
                    surface.Canvas.DrawBitmap(bitmap, 0, 0);
                    return surface;
                }

                case 7: // Rotate 90° CCW + flip horizontal
                {
                    info = new SKImageInfo(height, width);
                    surface = SKSurface.Create(info);
                    surface.Canvas.Translate(0, info.Height);
                    surface.Canvas.RotateDegrees(270);
                    surface.Canvas.Scale(-1, 1, bitmap.Width / 2.0f, bitmap.Height / 2.0f);
                    surface.Canvas.DrawBitmap(bitmap, 0, 0);
                    return surface;
                }

                case 8: // Rotate 90° CCW
                {
                    info = new SKImageInfo(height, width);
                    surface = SKSurface.Create(info);
                    surface.Canvas.Translate(0, info.Height);
                    surface.Canvas.RotateDegrees(270);
                    surface.Canvas.DrawBitmap(bitmap, 0, 0);
                    return surface;
                }

                case 1:
                default:
                    surface = SKSurface.Create(info);
                    surface.Canvas.DrawBitmap(bitmap, 0, 0);
                    return surface;
            }
        }
    }
}
