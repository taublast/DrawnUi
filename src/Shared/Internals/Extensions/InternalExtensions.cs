using System.Runtime.CompilerServices;

namespace DrawnUi.Extensions;

public static partial class InternalExtensions
{
    /// <summary>
    /// Radians to degrees
    /// </summary>
    /// <param name="radians"></param>
    /// <returns></returns>
    public static float ToDegrees(this float radians)
    {
        return radians * 180f / (float)Math.PI;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IntersectsWith(this SKRect rect, SKRect with, SKPoint offset)
    {
        with.Offset(offset);
        return rect.IntersectsWith(with);
    }

    /// <summary>
    /// The default Skia method is returning false if point is on the bounds, We correct this by custom function.
    /// </summary>
    /// <param name="rect"></param>
    /// <param name="point"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsInclusive(this SKRect rect, SKPoint point)
    {
        return rect.ContainsInclusive(point.X, point.Y);
    }

    /// <summary>
    /// The default Skia method is returning false if point is on the bounds, We correct this by custom function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsInclusive(this SKRect rect, float x, float y)
    {
        // Check if the point is within the rectangle or on its boundaries
        return (x >= rect.Left && x <= rect.Right &&
                y >= rect.Top && y <= rect.Bottom);
    }

    public static T GetItemAtIndex<T>(this LinkedList<T> linkedStack, int index)
    {
        // Check for invalid index
        if (index < 0 || index >= linkedStack.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");
        }

        LinkedListNode<T> currentNode = linkedStack.First;
        for (int i = 0; i < index; i++)
        {
            currentNode = currentNode.Next;
        }

        return currentNode.Value;
    }

    public static void DisposeControlAndChildren(this IView view)
    {
        if (view == null) return;

        if (view.Handler is IElementHandler nativeViewHandler)
        {

            MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    if (MainThread.IsMainThread)
                        nativeViewHandler.DisconnectHandler();
                    else
                    {
                        Super.Log($"Failed to disconnect handler for {nativeViewHandler}");
                    }
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }
            });
        }

        if (view is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                DisposeControlAndChildren(child);
            }
        }

        if (view is IContentView hasContent)
        {
            DisposeControlAndChildren(hasContent.PresentedContent);
        }

        if (view is View mauiView)
        {
            while (mauiView.Effects.Count > 0)
            {
                mauiView.Effects.RemoveAt(mauiView.Effects.Count - 1);
            }
            while (mauiView.Behaviors.Count > 0)
            {
                mauiView.Behaviors.RemoveAt(mauiView.Behaviors.Count - 1);
            }
        }

        if (view is IDisposable disposable)
        {
            disposable.Dispose();
        }

    }

    public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(() => tcs.TrySetResult(true)))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
                throw new OperationCanceledException(cancellationToken);
        }
        await task;
    }

    public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(() => tcs.TrySetResult(true)))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
                throw new OperationCanceledException(cancellationToken);
        }
        return await task;
    }
    public static SKRect Clone(this SKRect rect)
    {
        return new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    public static bool IsEven(this int value)
    {
        return value % 2 == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(this float self, float min, float max)
    {
        if (max < min)
        {
            return max;
        }
        else if (self < min)
        {
            return min;
        }
        else if (self > max)
        {
            return max;
        }

        return self;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Clamp(this double self, double min, double max)
    {
        if (max < min)
        {
            return max;
        }
        else if (self < min)
        {
            return min;
        }
        else if (self > max)
        {
            return max;
        }

        return self;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp(this int self, int min, int max)
    {
        if (max < min)
        {
            return max;
        }
        else if (self < min)
        {
            return min;
        }
        else if (self > max)
        {
            return max;
        }

        return self;
    }
}
