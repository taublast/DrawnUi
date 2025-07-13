using System.Globalization;
using System.Text;
using Android.Media;


namespace DrawnUi.Camera;

public partial class Metadata
{
    public static void FillExif(ExifInterface newexif, Metadata meta)
    {
        // Device information
        if (!string.IsNullOrEmpty(meta.Vendor))
            newexif.SetAttribute(ExifInterface.TagMake, meta.Vendor);

        if (!string.IsNullOrEmpty(meta.Model))
            newexif.SetAttribute(ExifInterface.TagModel, meta.Model);

        if (!string.IsNullOrEmpty(meta.Software))
            newexif.SetAttribute(ExifInterface.TagSoftware, meta.Software);
        else if (!string.IsNullOrEmpty(meta.Model))
            newexif.SetAttribute(ExifInterface.TagSoftware, meta.Model);

        if (meta.Orientation.HasValue)
            newexif.SetAttribute(ExifInterface.TagOrientation, meta.Orientation.Value.ToString());

        // Camera settings
        if (meta.ISO.HasValue && meta.ISO.Value > 0)
            newexif.SetAttribute(ExifInterface.TagIsoSpeedRatings, meta.ISO.Value.ToString());

        if (meta.FocalLength.HasValue && meta.FocalLength.Value > 0)
            newexif.SetAttribute(ExifInterface.TagFocalLength, ConvertToRational(meta.FocalLength.Value));

        if (meta.Aperture.HasValue && meta.Aperture.Value > 0)
            newexif.SetAttribute(ExifInterface.TagFNumber, ConvertToRational(meta.Aperture.Value));

        if (meta.Shutter.HasValue && meta.Shutter.Value > 0)
            newexif.SetAttribute(ExifInterface.TagExposureTime, ConvertToRational(meta.Shutter.Value));

        // Advanced camera settings
        if (!string.IsNullOrEmpty(meta.Flash))
            newexif.SetAttribute(ExifInterface.TagFlash, meta.Flash);

        if (!string.IsNullOrEmpty(meta.WhiteBalance))
            newexif.SetAttribute(ExifInterface.TagWhiteBalance, meta.WhiteBalance);

        if (!string.IsNullOrEmpty(meta.ExposureMode))
            newexif.SetAttribute(ExifInterface.TagExposureMode, meta.ExposureMode);

        if (!string.IsNullOrEmpty(meta.MeteringMode))
            newexif.SetAttribute(ExifInterface.TagMeteringMode, meta.MeteringMode);

        if (!string.IsNullOrEmpty(meta.SceneCaptureType))
            newexif.SetAttribute(ExifInterface.TagSceneCaptureType, meta.SceneCaptureType);

        if (meta.ExposureBias.HasValue)
            newexif.SetAttribute(ExifInterface.TagExposureBiasValue, ConvertToRational(meta.ExposureBias.Value));

        if (meta.BrightnessValue.HasValue)
            newexif.SetAttribute(ExifInterface.TagBrightnessValue, ConvertToRational(meta.BrightnessValue.Value));

        if (meta.DigitalZoomRatio.HasValue && meta.DigitalZoomRatio.Value > 0)
            newexif.SetAttribute(ExifInterface.TagDigitalZoomRatio, ConvertToRational(meta.DigitalZoomRatio.Value));

        if (meta.FocalLengthIn35mm.HasValue && meta.FocalLengthIn35mm.Value > 0)
            newexif.SetAttribute(ExifInterface.TagFocalLengthIn35mmFilm, meta.FocalLengthIn35mm.Value.ToString());

        // Image properties
        if (meta.PixelWidth.HasValue && meta.PixelWidth.Value > 0)
            newexif.SetAttribute(ExifInterface.TagImageWidth, meta.PixelWidth.Value.ToString());

        if (meta.PixelHeight.HasValue && meta.PixelHeight.Value > 0)
            newexif.SetAttribute(ExifInterface.TagImageLength, meta.PixelHeight.Value.ToString());

        if (meta.XResolution.HasValue && meta.XResolution.Value > 0)
            newexif.SetAttribute(ExifInterface.TagXResolution, ConvertToRational(meta.XResolution.Value));

        if (meta.YResolution.HasValue && meta.YResolution.Value > 0)
            newexif.SetAttribute(ExifInterface.TagYResolution, ConvertToRational(meta.YResolution.Value));

        if (!string.IsNullOrEmpty(meta.ResolutionUnit))
            newexif.SetAttribute(ExifInterface.TagResolutionUnit, meta.ResolutionUnit);

        if (!string.IsNullOrEmpty(meta.ColorSpace))
            newexif.SetAttribute(ExifInterface.TagColorSpace, meta.ColorSpace);

        /*
        // Lens information
        if (!string.IsNullOrEmpty(meta.LensMake))
            newexif.SetAttribute(ExifInterface.TagLensMake, meta.LensMake);

        if (!string.IsNullOrEmpty(meta.LensModel))
            newexif.SetAttribute(ExifInterface.TagLensModel, meta.LensModel);

        if (!string.IsNullOrEmpty(meta.LensSpecification))
            newexif.SetAttribute(ExifInterface.TagLensSpecification, meta.LensSpecification);
        */

        // Date/Time information
        if (meta.DateTimeOriginal.HasValue)
            newexif.SetAttribute(ExifInterface.TagDatetimeOriginal,
                meta.DateTimeOriginal.Value.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture));

        if (meta.DateTimeDigitized.HasValue)
            newexif.SetAttribute(ExifInterface.TagDatetimeDigitized,
                meta.DateTimeDigitized.Value.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture));

        if (!string.IsNullOrEmpty(meta.SubsecTime))
            newexif.SetAttribute(ExifInterface.TagSubsecTime, meta.SubsecTime);

        if (!string.IsNullOrEmpty(meta.SubsecTimeOriginal))
            newexif.SetAttribute(ExifInterface.TagSubsecTimeOriginal, meta.SubsecTimeOriginal);

        if (!string.IsNullOrEmpty(meta.SubsecTimeDigitized))
            newexif.SetAttribute(ExifInterface.TagSubsecTimeDigitized, meta.SubsecTimeDigitized);

        // Advanced settings
        if (!string.IsNullOrEmpty(meta.ExposureProgram))
            newexif.SetAttribute(ExifInterface.TagExposureProgram, meta.ExposureProgram);

        if (!string.IsNullOrEmpty(meta.SceneType))
            newexif.SetAttribute(ExifInterface.TagSceneType, meta.SceneType);

        if (!string.IsNullOrEmpty(meta.CustomRendered))
            newexif.SetAttribute(ExifInterface.TagCustomRendered, meta.CustomRendered);

        if (!string.IsNullOrEmpty(meta.GainControl))
            newexif.SetAttribute(ExifInterface.TagGainControl, meta.GainControl);

        if (!string.IsNullOrEmpty(meta.Contrast))
            newexif.SetAttribute(ExifInterface.TagContrast, meta.Contrast);

        if (!string.IsNullOrEmpty(meta.Saturation))
            newexif.SetAttribute(ExifInterface.TagSaturation, meta.Saturation);

        if (!string.IsNullOrEmpty(meta.Sharpness))
            newexif.SetAttribute(ExifInterface.TagSharpness, meta.Sharpness);

        if (!string.IsNullOrEmpty(meta.SubjectArea))
            newexif.SetAttribute(ExifInterface.TagSubjectArea, meta.SubjectArea);

        if (!string.IsNullOrEmpty(meta.SubjectDistanceRange))
            newexif.SetAttribute(ExifInterface.TagSubjectDistanceRange, meta.SubjectDistanceRange);

        if (!string.IsNullOrEmpty(meta.SensingMethod))
            newexif.SetAttribute(ExifInterface.TagSensingMethod, meta.SensingMethod);

        // Device information
        /*
        if (!string.IsNullOrEmpty(meta.BodySerialNumber))
            newexif.SetAttribute(ExifInterface.TagBodySerialNumber, meta.BodySerialNumber);

        if (!string.IsNullOrEmpty(meta.CameraOwnerName))
            newexif.SetAttribute(ExifInterface.TagCameraOwnerName, meta.CameraOwnerName);
        */

        if (!string.IsNullOrEmpty(meta.ImageUniqueId))
            newexif.SetAttribute(ExifInterface.TagImageUniqueId, meta.ImageUniqueId);

        // GPS coordinates from metadata
        if (meta.GpsLatitude.HasValue && meta.GpsLongitude.HasValue &&
            meta.GpsLatitude.Value != 0 && meta.GpsLongitude.Value != 0)
        {
            newexif.SetAttribute(ExifInterface.TagGpsLatitude, ConvertCoords(meta.GpsLatitude.Value));
            newexif.SetAttribute(ExifInterface.TagGpsLongitude, ConvertCoords(meta.GpsLongitude.Value));
            newexif.SetAttribute(ExifInterface.TagGpsLatitudeRef,
                !string.IsNullOrEmpty(meta.GpsLatitudeRef)
                    ? meta.GpsLatitudeRef
                    : (meta.GpsLatitude.Value > 0 ? "N" : "S"));
            newexif.SetAttribute(ExifInterface.TagGpsLongitudeRef,
                !string.IsNullOrEmpty(meta.GpsLongitudeRef)
                    ? meta.GpsLongitudeRef
                    : (meta.GpsLongitude.Value > 0 ? "E" : "W"));
        }

        if (meta.GpsAltitude.HasValue)
        {
            newexif.SetAttribute(ExifInterface.TagGpsAltitude,
                Metadata.ConvertToRational(Math.Abs(meta.GpsAltitude.Value)));
            newexif.SetAttribute(ExifInterface.TagGpsAltitudeRef,
                !string.IsNullOrEmpty(meta.GpsAltitudeRef)
                    ? meta.GpsAltitudeRef
                    : (meta.GpsAltitude.Value >= 0 ? "0" : "1"));
        }

        if (meta.GpsTimestamp.HasValue)
        {
            newexif.SetAttribute(ExifInterface.TagGpsTimestamp,
                meta.GpsTimestamp.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            newexif.SetAttribute(ExifInterface.TagGpsDatestamp,
                meta.GpsTimestamp.Value.ToString("yyyy:MM:dd", CultureInfo.InvariantCulture));
        }

        // Timestamp (current time if not from metadata)
        newexif.SetAttribute(ExifInterface.TagDatetime,
            DateTime.UtcNow.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture));
 
    }

}
