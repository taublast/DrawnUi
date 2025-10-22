namespace DrawnUi.Draw;

public static class SkiaImageEffects
{

    //public static SKColorFilter Tint(Color color)
    //{
    //	return SKColorFilter.CreateColorMatrix(new float[]
    //	{
    //			//row-major matrix
    //			// R     G       B        A   +translation
    //			1, 0, 0, 0, (float)color.Red * 255, // R result
    //			0, 1, 0, 0, (float)color.Green * 255, // G result
    //			0, 0, 1, 0, (float)color.Blue * 255, // B result
    //			0, 0, 0, 1, 0 // A result
    //	});
    //}

    /// <summary>
    /// If you want to Tint: SKBlendMode.SrcATop + ColorTint with alpha below 1
    /// </summary>
    /// <param name="color"></param>
    /// <param name="mode"></param>
    /// <returns></returns>
    public static SKColorFilter Tint(Color color, SKBlendMode mode)
    {
        return SKColorFilter.CreateBlendMode(
            new SKColor((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), (byte)(color.Alpha * 255)),
            mode);
    }

    public static SKColorFilter TintSL(Color tint, float saturation, float lightness, SKBlendMode mode)
    {

        // Create the individual filters
        var saturationFilter = Saturation(saturation);
        var lightnessFilter = Lightness(lightness);
        var tintFilter = Tint(tint, mode);

        // Combine the filters
        var combinedFilter = SKColorFilter.CreateCompose(saturationFilter, lightnessFilter);
        combinedFilter = SKColorFilter.CreateCompose(combinedFilter, tintFilter);

        return combinedFilter;
    }

    public static SKColorFilter HSL(float hue, float saturation, float lightness, SKBlendMode mode)
    {
        // Use hue and lightness to get a Color value. Use 1 for saturation as 
        // we're already adjusting saturation separately.
        Color tint = Color.FromHsla(hue, 1f, lightness, 1f);

        // Create a saturation adjustment filter
        var saturationFilter = Saturation(saturation);

        // Create a lightness adjustment filter
        var lightnessFilter = Lightness(lightness);

        // Create a color tint filter using the color derived from HSL values
        var tintFilter = Tint(tint, mode);

        // Combine the saturation and lightness filters
        var combinedFilter = SKColorFilter.CreateCompose(saturationFilter, lightnessFilter);

        // Add the tint filter to the combined filter
        combinedFilter = SKColorFilter.CreateCompose(combinedFilter, tintFilter);

        // Return the combined filter
        return combinedFilter;
    }


    public static SKColorFilter Darken(float amount)
    {
        return SKColorFilter.CreateColorMatrix(new float[]
        {
			//row-major matrix
			// R     G       B        A   +translation
			1, 0, 0, 0, -amount, // R result
			0, 1, 0, 0, -amount, // G result
			0, 0, 1, 0, -amount, // B result
			0, 0, 0, 1, 0 // A result
		});
    }

    public static SKColorFilter Lighten(float amount)
    {
        return SKColorFilter.CreateColorMatrix(new float[]
        {
			//row-major matrix
			// R     G       B        A   +translation
			1, 0, 0, 0, amount, // R result
			0, 1, 0, 0, amount, // G result
			0, 0, 1, 0, amount, // B result
			0, 0, 0, 1, 0 // A result
		});
    }

    /// <summary>
    /// This effect turns an image to grayscale. This particular version uses the NTSC/PAL/SECAM standard luminance value weights: 0.2989 for red, 0.587 for green, and 0.114 for blue.
    /// </summary>
    /// <returns></returns>
    public static SKColorFilter Grayscale()
    {
        return SKColorFilter.CreateColorMatrix(new float[]
        {
            0.2989f, 0.587f, 0.114f, 0, 0,
            0.2989f, 0.587f, 0.114f, 0, 0,
            0.2989f, 0.587f, 0.114f, 0, 0,
            0, 0, 0, 1, 0
        });
    }

    // This effect turns an image to grayscale.
    //public static SKColorFilter Grayscale1()
    //{
    //	return SKColorFilter.CreateLumaColor();
    //}

    /// <summary>
    ///  This effect turns an image to grayscale.
    /// </summary>
    /// <returns></returns>
    public static SKColorFilter Grayscale2()
    {
        return SKColorFilter.CreateColorMatrix(new float[]
        {
			//row-major matrix
			// R     G       B        A    +translation
			0.21f, 0.72f, 0.07f, 0, 0, // R result
			0.21f, 0.72f, 0.07f, 0, 0, // G result
			0.21f, 0.72f, 0.07f, 0, 0, // B result
			0, 0, 0, 1, 0 // A result
		});
    }
    public static SKColorFilter Pastel()
    {
        return SKColorFilter.CreateColorMatrix(new float[]
        {
			//row-major matrix
			// R     G       B        A  +translation
			0.75f, 0.25f, 0.25f, 0, 0,
            0.25f, 0.75f, 0.25f, 0, 0,
            0.25f, 0.25f, 0.75f, 0, 0,
            0, 0, 0, 1, 0
        });
    }

    /// <summary>
    /// The sepia effect can give your photos a warm, brownish tone that mimics the look of an older photo.
    /// </summary>
    /// <returns></returns>
    public static SKColorFilter Sepia()
    {
        return SKColorFilter.CreateColorMatrix(new float[]
        {
			// row-major matrix
			// R     G     B      A  +translation
			0.393f, 0.769f, 0.189f, 0, 0, // R result
			0.349f, 0.686f, 0.168f, 0, 0, // G result
			0.272f, 0.534f, 0.131f, 0, 0, // B result
			0, 0, 0, 1, 0 // A result
		});
    }

    /// <summary>
    /// This effect inverts the colors in an image. NOT WORKING!
    /// </summary>
    /// <returns></returns>
    public static SKColorFilter InvertColors()
    {
        return SKColorFilter.CreateColorMatrix(new float[]
        {
            -1, 0, 0, 0, 255,
            0, -1, 0, 0, 255,
            0, 0, -1, 0, 255,
            0, 0, 0, 1, 0
        });
    }


    /// <summary>
    /// This effect adjusts the contrast of an image. amount is the adjustment level. Negative values decrease contrast, positive values increase contrast, and 0 means no change.
    /// </summary>
    /// <param name="amount"></param>
    /// <returns></returns>
    public static SKColorFilter Contrast(float amount)
    {
        float translatedContrast = amount + 1;
        float averageLuminance = 0.5f * (1 - amount);

        return SKColorFilter.CreateColorMatrix(new float[]
        {
            translatedContrast, 0, 0, 0, averageLuminance,
            0, translatedContrast, 0, 0, averageLuminance,
            0, 0, translatedContrast, 0, averageLuminance,
            0, 0, 0, 1, 0
        });
    }

    /// <summary>
    /// This effect adjusts the saturation of an image. amount is the adjustment level. Negative values desaturate the image, positive values increase saturation, and 0 means no change.
    /// </summary>
    /// <param name="amount"></param>
    /// <returns></returns>
    public static SKColorFilter Saturation(float amount)
    {
        float rr = 0.213f * (1 - amount);
        float rg = 0.715f * (1 - amount);
        float rb = 0.072f * (1 - amount);

        return SKColorFilter.CreateColorMatrix(new float[]
        {
            rr + amount, rg, rb, 0, 0,
            rr, rg + amount, rb, 0, 0,
            rr, rg, rb + amount, 0, 0,
            0, 0, 0, 1, 0
        });
    }




    /// <summary>
    /// This effect increases the brightness of an image. amount is between 0 (no change) and 1 (white).
    /// </summary>
    /// <param name="amount"></param>
    /// <returns></returns>
    public static SKColorFilter Brightness(float amount)
    {
        return SKColorFilter.CreateColorMatrix(new float[]
        {
            1, 0, 0, 0, amount * 255,
            0, 1, 0, 0, amount * 255,
            0, 0, 1, 0, amount * 255,
            0, 0, 0, 1, 0
        });
    }

    /// <summary>
    /// Adjusts the brightness of an image:
    /// </summary>
    /// <param name="amount"></param>
    /// <returns></returns>
    public static SKColorFilter Lightness(float amount)
    {
        return SKColorFilter.CreateColorMatrix(new float[]
        {
            1, 0, 0, 0, amount, // R
			0, 1, 0, 0, amount, // G
			0, 0, 1, 0, amount, // B
			0, 0, 0, 1, 0       // A
		});
    }


    /// <summary>
    /// This effect applies gamma correction to an image. gamma must be greater than 0. A .
    /// </summary>
    /// <param name="gamma"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static SKColorFilter Gamma(float gamma)
    {
        //value greater than 1 darkens the image, while a gamma value less than 1 brightens the image
        if (gamma < 1)
        {
            gamma += 1;
        }
        else
        if (gamma > 1)
        {
            gamma -= 1;
        }

        if (gamma <= 0) throw new ArgumentOutOfRangeException(nameof(gamma));

        byte[] gammaCurve = new byte[256];
        for (int i = 0; i < gammaCurve.Length; i++)
        {
            gammaCurve[i] = (byte)(Math.Pow(i / 255.0, gamma) * 255);
        }

        return SKColorFilter.CreateTable(gammaCurve);
    }







}

