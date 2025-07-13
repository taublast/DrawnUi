namespace DrawnUi.Draw;


[DebuggerDisplay("Left = {Left},Top ={Top}, Width={Width},Height={Height}")]
public class DrawingRect
{

    public DrawingRect()
    {
    }

    public DrawingRect(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public void Offset(float x, float y)
    {
        Left += x;
        Top += y;
        Right += x;
        Bottom += y;
    }

    public void Set(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public DrawingRect(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public DrawingRect(SKRect rect)
    {
        Left = rect.Left;
        Top = rect.Top;
        Right = rect.Right;
        Bottom = rect.Bottom;
    }

    public float Left { get; set; }
    public float Top { get; set; }
    public float Right { get; set; }
    public float Bottom { get; set; }

    public float Width
    {
        get
        {
            return Right - Left;
        }
        set
        {
            Right = Left + value;
        }
    }
    public float Height
    {
        get
        {
            return Bottom - Top;
        }
        set
        {
            Bottom = Top + value;
        }
    }

    public float MidY
    {
        get
        {
            return Top + Height / 2.0f;
        }
    }

    public float MidX
    {
        get
        {
            return Left + Width / 2.0f;
        }
    }

    public bool IntersectsWith(DrawingRect other)
    {
        return Left <= other.Right && Top <= other.Bottom &&
               Right >= other.Left && Bottom >= other.Top;
    }

    public bool IntersectsWith(SKRect other)
    {
        return Left <= other.Right && Top <= other.Bottom &&
               Right >= other.Left && Bottom >= other.Top;
    }

    public bool IntersectsWith(float left, float top, float right, float bottom)
    {
        return Left <= right && Top <= bottom &&
                       Right >= left && Bottom >= top;
    }

    public bool Contains(float x, float y)
    {
        return x >= Left && x <= Right && y >= Top && y <= Bottom;
    }


    public bool Contains(DrawingRect other)
    {
        return Left <= other.Left && other.Right <= Right &&
               Top <= other.Top && other.Bottom <= Bottom;
    }

    public SKRect ToSkia()
    {
        return new SKRect(Left, Top, Right, Bottom);
    }

    public Rect ToRect()
    {
        return new Rect(Left, Top, Width, Height);
    }

    public static DrawingRect Empty => new DrawingRect(0, 0, 0, 0);

    public DrawingRect Clone()
    {
        return new(Left, Top, Right, Bottom);
    }

    public bool Compare(DrawingRect b)
    {
        if (b == null)
        {
            return false;
        }

        return Left == b.Left && Top == b.Top && Right == b.Right && Bottom == b.Bottom;
    }

    //public static bool operator ==(DrawingRect a, DrawingRect b)
    //{
    //    return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
    //}

    //public static bool operator !=(DrawingRect a, DrawingRect b)
    //{
    //    return !(a == b);
    //}
    public override bool Equals(object obj)
    {
        if (obj is not DrawingRect)
        {
            return false;
        }
        var rectangle = (DrawingRect)obj;
        return Left == rectangle.Left &&
               Top == rectangle.Top &&
               Right == rectangle.Right &&
               Bottom == rectangle.Bottom;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Left, Top, Right, Bottom);
    }





}
