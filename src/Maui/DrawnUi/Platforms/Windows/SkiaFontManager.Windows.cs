using Microsoft.Maui;
using Microsoft.Maui.Controls;
using System.Diagnostics;

namespace DrawnUi.Draw
{
    public partial class SkiaFontManager
    {


        SemaphoreSlim _semaphore = new(1, 1);

        public SKTypeface GetFont(string alias)
        {
            if (!Initialized)
            {
                return DefaultTypeface;
            }

            try
            {
                if (Fonts.TryGetValue(alias, out var existing))
                {
                    return existing;
                }

                var font = SKTypeface.FromFamilyName(alias);
                if (!string.IsNullOrEmpty(alias) && (font == null || font.FamilyName != alias))
                {
                    font = null;
                    try
                    {
                        var instance = FontRegistrar as FontRegistrar;
                        var type = instance.GetType();
                        var fields = type.GetAllHiddenFields();
                        var field = fields.First(x => x.Name == "_nativeFonts");
                        var fonts = (Dictionary<string, (string Filename, string? Alias)>)field.GetValue(instance);

                        var registered = fonts.Values.FirstOrDefault(x =>
                            x.Filename == alias
                            || x.Alias == alias);

                        using (Stream fileStream = FileSystem.Current.OpenAppPackageFileAsync(registered.Filename).GetAwaiter().GetResult())
                        {
                            font = SKTypeface.FromStream(fileStream);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                    }
                }


                if (font == null)
                {
                    if (ThrowIfFailedToCreateFont)
                    {
                        throw new Exception($"[SKIA] Couldn't create font {alias}");
                    }

                    font = DefaultTypeface;

                    Trace.WriteLine($"[SKIA] Couldn't create font {alias}");
                }
                else
                {
                    if (!string.IsNullOrEmpty(alias))
                        Fonts[alias] = font;
                }

                return font;

            }
            finally
            {

            }
        }

    }
}
