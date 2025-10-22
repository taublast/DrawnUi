using System.Numerics;

namespace DrawnUi.Infrastructure.Helpers;

public static class RubberBandUtils
{

    #region HELPERS

    /// <summary>
    /// track is the bounds of the possible scrolling offset, for example can be like {0, -1000, 0, 0}
    /// </summary>
    /// <param name="point"></param>
    /// <param name="track"></param>
    /// <param name="coeff"></param>
    /// <returns></returns>
    //public static Vector2 ClampOnTrack(Vector2 point, SKRect track, float coeff = 0.55f)
    //{
    //    var dims = new Vector2(track.Width, track.Height);

    //    float x = RubberBandClamp(point.X, dims.X, new RangeF(track.Left, track.Right), coeff);
    //    float y = RubberBandClamp(point.Y, dims.Y, new RangeF(track.Top, track.Bottom), coeff);

    //    //Super.Log($"CLAMPED {point.Y} for {track} to {y}");

    //    return new Vector2(x, y);
    //}

    public static Vector2 ClampOnTrack(Vector2 point, SKRect track, float coeff = 0.55f, Vector2? customDims = null)
    {
        var dims = customDims ?? new Vector2(track.Width, track.Height);

        float x = RubberBandClamp(point.X, dims.X, new RangeF(track.Left, track.Right), coeff);
        float y = RubberBandClamp(point.Y, dims.Y, new RangeF(track.Top, track.Bottom), coeff);

        return new Vector2(x, y);
    }


    public static Vector2 Clamp(Vector2 point, Vector2 dims, SKRect bounds, float coeff = 0.55f)
    {
        float x = RubberBandClamp(point.X, dims.X, new RangeF(bounds.Left, bounds.Right), coeff);
        float y = RubberBandClamp(point.Y, dims.Y, new RangeF(bounds.Top, bounds.Bottom), coeff);

        return new Vector2(x, y);
    }

    #endregion

    #region CORE


    public static float RubberBandClamp(float diff, float coeff, float dim, float onEmpty)
    {
        if (dim == 0)
            dim = onEmpty;

        return (1.0f - 1.0f / (diff * coeff / dim + 1.0f)) * dim;
    }


    /// <summary>
    /// onEmpty - how much to simulate scrollable area when its zero
    /// </summary>
    /// <param name="coord"></param>
    /// <param name="coeff"></param>
    /// <param name="dim"></param>
    /// <param name="limits"></param>
    /// <param name="onEmpty"></param>
    /// <returns></returns>
    //public static float RubberBandClamp(float coord, float dim, RangeF limits, float coeff = 0.275f, float onEmpty = 40f)
    //{
    //    if (limits.Start > limits.End)
    //    {
    //        return coord;
    //    }

    //    float clampedCoord = Math.Clamp(coord, limits.Start, limits.End);

    //    var overscroll = 0f;
    //    if (coord < limits.Start)
    //    {
    //        overscroll = -(limits.Start - coord);
    //    }
    //    else
    //    if (coord > limits.End) //уходит выше нуля, тянем вниз напр. 4
    //    {
    //        overscroll = coord - limits.End; // работает ок, овер > 0
    //    }

    //    if (overscroll != 0)
    //    {
    //        float sign = Math.Sign(overscroll);
    //        var rubber = RubberBandClamp(Math.Abs(overscroll), coeff, dim, onEmpty);
    //        return clampedCoord + sign * rubber;
    //    }

    //    return clampedCoord;
    //}
    public static float RubberBandClamp(float coord, float dim, RangeF limits, float coeff = 0.275f, float onEmpty = 40f)
    {
        if (limits.Start > limits.End)
        {
            return coord;
        }

        float clampedCoord = Math.Clamp(coord, limits.Start, limits.End);

        float overscroll = 0f;
        if (coord < limits.Start)
        {
            overscroll = coord - limits.Start;
        }
        else if (coord > limits.End)
        {
            overscroll = coord - limits.End;
        }

        if (overscroll != 0)
        {
            float sign = Math.Sign(overscroll);
            if (dim == 0)
                dim = onEmpty;

            var rubber = (1.0f - 1.0f / (Math.Abs(overscroll) * coeff / dim + 1.0f)) * dim;
            return clampedCoord + sign * rubber;
        }

        return clampedCoord;
    }

    #endregion


}
