using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace DrawnUi.Infrastructure.Xaml
{

    public class FrameworkImageSourceConverter : TypeConverter
    {

        public static ImageSource FromStream(Func<Stream> stream, string url, string resource)
        {
            return new ImageSourceResourceStream
            {
                Stream = (token) => Task.Run(stream, token),
                Url = url,
                Resource = resource
            };
        }

        public static ImageSource StreamFromResourceUrl(string url, Assembly assembly = null)
        {
            var uri = new Uri(url);

            var parts = uri.OriginalString.Substring(11).Split('?');
            var resourceName = parts.First();

            if (parts.Count() > 1)
            {
                var name = Uri.UnescapeDataString(uri.Query.Substring(10));
                var assemblyName = new AssemblyName(name);
                assembly = Assembly.Load(assemblyName);
            }

            if (assembly == null)
            {
                assembly = Assembly.GetCallingAssembly();
            }

            var fullPath = $"{assembly.GetName().Name}.{resourceName}";

            //Trace.WriteLine($"[StreamFromResourceUrl] loading {fullPath}..");

            return FromStream(() => assembly.GetManifestResourceStream(fullPath), url, fullPath);
        }

        public static ImageSource FromInvariantString(string value)
        {
            if (value != null)
            {
                if (value.StartsWith("resource://", StringComparison.OrdinalIgnoreCase))
                {
                    return StreamFromResourceUrl(value);
                }

                if (!Uri.TryCreate(value, UriKind.Absolute, out Uri result) || !(result.Scheme != "file"))
                {
                    return ImageSource.FromFile(value);
                }

                return ImageSource.FromUri(result);
            }

            throw new InvalidOperationException($"Cannot convert \"{value}\" into {typeof(ImageSource)}");
        }

        //
        // Summary:
        //     Returns an image source created from a URI that is contained in value.
        //
        // Parameters:
        //   value:
        //     The value to convert.
        //
        // Returns:
        //     To be added.
        //
        // Remarks:
        //     To be added.

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            return FromInvariantString(value as string);

            //            return base.ConvertFrom(context, culture, value);
        }



        //public override object ConvertFromInvariantString(string value)
        //{
        //    return FromInvariantString(value);
        //}

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            return ConvertToString(value);

            //return base.ConvertTo(context, culture, value, destinationType);
        }
        public new static string ConvertToString(object value)
        {
            ImageSourceResourceStream streamFromResources = value as ImageSourceResourceStream;
            if (streamFromResources != null)
            {
                return streamFromResources.Url;
            }

            FileImageSource fileImageSource = value as FileImageSource;
            if (fileImageSource != null)
            {
                return fileImageSource.File;
            }

            UriImageSource uriImageSource = value as UriImageSource;
            if (uriImageSource != null)
            {
                return uriImageSource.Uri.OriginalString;
            }

            return string.Empty;
        }

        //public override string ConvertToInvariantString(object value)
        //{
        //    return ConvertToString(value);

        //    //ImageSourceResourceStream streamFromResources = value as ImageSourceResourceStream;
        //    //if (streamFromResources != null)
        //    //{
        //    //    return streamFromResources.Url;
        //    //}

        //    //FileImageSource fileImageSource = value as FileImageSource;
        //    //if (fileImageSource != null)
        //    //{
        //    //    return fileImageSource.File;
        //    //}

        //    //UriImageSource uriImageSource = value as UriImageSource;
        //    //if (uriImageSource != null)
        //    //{
        //    //    return uriImageSource.Uri.OriginalString;
        //    //}

        //    //throw new NotSupportedException();
        //}
    }
}