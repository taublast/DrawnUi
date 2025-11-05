namespace DrawnUi.Infrastructure
{
    public struct VisualTransformNative
    {
        public bool IsVisible { get; set; }

        /// <summary>
        /// Pixels only
        /// </summary>
        public SKRect Rect { get; set; }

        public SKPoint Translation { get; set; }

        public float Opacity { get; set; }

        public float Rotation { get; set; }

        public SKPoint Scale { get; set; }

        public static bool operator ==(VisualTransformNative left, VisualTransformNative right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(VisualTransformNative left, VisualTransformNative right)
        {
            return !left.Equals(right);
        }
        public override bool Equals(object obj)
        {
            if (!(obj is VisualTransformNative))
            {
                return false;
            }
            var native = (VisualTransformNative)obj;

            return IsVisible == native.IsVisible &&
                Opacity == native.Opacity &&
                Rect == native.Rect &&
                Translation == native.Translation &&
                Rotation == native.Rotation &&
                Scale == native.Scale;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IsVisible, Rect, Translation, Rotation, Scale);
        }


    }
}
