
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AppoMobi.Specials;
using AVFoundation;
using CoreFoundation;
using CoreGraphics;
using CoreImage;
using CoreMedia;
using CoreVideo;
using DrawnUi.Controls;
using Foundation;
using ImageIO;
using Microsoft.Maui.Media;
using Photos;
using SkiaSharp;
using SkiaSharp.Views.iOS;
using UIKit;
using static AVFoundation.AVMetadataIdentifiers;

namespace DrawnUi.Camera;

public partial class Metadata
{
    /// <summary>
    /// Creates comprehensive Metadata object from iOS image properties
    /// </summary>
    /// <param name="props">Image properties from CIImage</param>
    /// <param name="metaData">Mutable dictionary containing image metadata</param>
    /// <returns>Populated Metadata object with all available EXIF data</returns>
    public static Metadata CreateMetadataFromProperties(CoreGraphics.CGImageProperties props, NSMutableDictionary metaData)
    {
        var metadata = new Metadata();

        // Orientation
        if (metaData?["Orientation"] is NSNumber orientationNumber)
        {
            metadata.Orientation = orientationNumber.Int32Value;
        }

        // EXIF Data
        var exif = props.Exif;
        if (exif != null)
        {
            ExtractBasicExifData(metadata, exif);
            ExtractAdvancedExifData(metadata, exif);
            ExtractDateTimeData(metadata, exif);
            ExtractLensData(metadata, exif);
            ExtractImageProperties(metadata, exif);
        }

        // GPS Data
        var gps = props.Gps;
        if (gps != null)
        {
            ExtractGpsData(metadata, gps);
        }

        return metadata;
    }

    /// <summary>
    /// Extracts basic EXIF data (original properties)
    /// </summary>
    private static void ExtractBasicExifData(Metadata metadata, CoreGraphics.CGImagePropertiesExif exif)
    {
        if (exif.Dictionary["ISOSpeedRatings"] is NSArray isoArray && isoArray.Count > 0)
        {
            if (isoArray.GetItem<NSNumber>(0) is NSNumber isoNumber)
            {
                metadata.ISO = isoNumber.Int32Value;
            }
        }

        if (exif.Dictionary["FocalLength"] is NSNumber focalNumber)
            metadata.FocalLength = focalNumber.DoubleValue;

        if (exif.Dictionary["FNumber"] is NSNumber apertureNumber)
            metadata.Aperture = apertureNumber.DoubleValue;

        if (exif.Dictionary["ExposureTime"] is NSNumber shutterNumber)
            metadata.Shutter = shutterNumber.DoubleValue;

        if (exif.Dictionary["Software"] is NSString softwareString)
            metadata.Software = softwareString.ToString();

        if (exif.Dictionary["Make"] is NSString makeString)
            metadata.Vendor = makeString.ToString();

        if (exif.Dictionary["Model"] is NSString modelString)
            metadata.Model = modelString.ToString();
    }

    /// <summary>
    /// Extracts advanced camera settings from EXIF
    /// </summary>
    private static void ExtractAdvancedExifData(Metadata metadata, CoreGraphics.CGImagePropertiesExif exif)
    {
        if (exif.Dictionary["Flash"] is NSNumber flashNumber)
            metadata.Flash = flashNumber.StringValue;

        if (exif.Dictionary["WhiteBalance"] is NSNumber wbNumber)
            metadata.WhiteBalance = wbNumber.StringValue;

        if (exif.Dictionary["ExposureMode"] is NSNumber expModeNumber)
            metadata.ExposureMode = expModeNumber.StringValue;

        if (exif.Dictionary["MeteringMode"] is NSNumber meteringNumber)
            metadata.MeteringMode = meteringNumber.StringValue;

        if (exif.Dictionary["SceneCaptureType"] is NSNumber sceneNumber)
            metadata.SceneCaptureType = sceneNumber.StringValue;

        if (exif.Dictionary["ExposureBiasValue"] is NSNumber expBiasNumber)
            metadata.ExposureBias = expBiasNumber.DoubleValue;

        if (exif.Dictionary["BrightnessValue"] is NSNumber brightnessNumber)
            metadata.BrightnessValue = brightnessNumber.DoubleValue;

        if (exif.Dictionary["DigitalZoomRatio"] is NSNumber zoomNumber)
            metadata.DigitalZoomRatio = zoomNumber.DoubleValue;

        if (exif.Dictionary["FocalLenIn35mmFilm"] is NSNumber focal35Number)
            metadata.FocalLengthIn35mm = focal35Number.DoubleValue;

        if (exif.Dictionary["ExposureProgram"] is NSNumber expProgNumber)
            metadata.ExposureProgram = expProgNumber.StringValue;

        if (exif.Dictionary["SceneType"] is NSNumber sceneTypeNumber)
            metadata.SceneType = sceneTypeNumber.StringValue;

        if (exif.Dictionary["CustomRendered"] is NSNumber customNumber)
            metadata.CustomRendered = customNumber.StringValue;

        if (exif.Dictionary["GainControl"] is NSNumber gainNumber)
            metadata.GainControl = gainNumber.StringValue;

        if (exif.Dictionary["Contrast"] is NSNumber contrastNumber)
            metadata.Contrast = contrastNumber.StringValue;

        if (exif.Dictionary["Saturation"] is NSNumber satNumber)
            metadata.Saturation = satNumber.StringValue;

        if (exif.Dictionary["Sharpness"] is NSNumber sharpNumber)
            metadata.Sharpness = sharpNumber.StringValue;

        if (exif.Dictionary["SubjectArea"] is NSArray subjectArray)
            metadata.SubjectArea = string.Join(",", subjectArray.ToArray().Select(x => x.ToString()));

        if (exif.Dictionary["SubjectDistanceRange"] is NSNumber subDistNumber)
            metadata.SubjectDistanceRange = subDistNumber.StringValue;

        if (exif.Dictionary["SensingMethod"] is NSNumber sensingNumber)
            metadata.SensingMethod = sensingNumber.StringValue;
    }

    /// <summary>
    /// Extracts date and time information from EXIF
    /// </summary>
    private static void ExtractDateTimeData(Metadata metadata, CoreGraphics.CGImagePropertiesExif exif)
    {
        if (exif.Dictionary["DateTimeOriginal"] is NSString dateOrigString &&
            DateTime.TryParse(dateOrigString.ToString(), out var dateOrig))
            metadata.DateTimeOriginal = dateOrig;

        if (exif.Dictionary["DateTimeDigitized"] is NSString dateDigString &&
            DateTime.TryParse(dateDigString.ToString(), out var dateDig))
            metadata.DateTimeDigitized = dateDig;

        if (exif.Dictionary["SubsecTime"] is NSString subsecString)
            metadata.SubsecTime = subsecString.ToString();

        if (exif.Dictionary["SubsecTimeOriginal"] is NSString subsecOrigString)
            metadata.SubsecTimeOriginal = subsecOrigString.ToString();

        if (exif.Dictionary["SubsecTimeDigitized"] is NSString subsecDigString)
            metadata.SubsecTimeDigitized = subsecDigString.ToString();
    }

    /// <summary>
    /// Extracts lens information from EXIF
    /// </summary>
    private static void ExtractLensData(Metadata metadata, CoreGraphics.CGImagePropertiesExif exif)
    {
        if (exif.Dictionary["LensMake"] is NSString lensMakeString)
            metadata.LensMake = lensMakeString.ToString();

        if (exif.Dictionary["LensModel"] is NSString lensModelString)
            metadata.LensModel = lensModelString.ToString();

        if (exif.Dictionary["LensSpecification"] is NSArray lensSpecArray)
            metadata.LensSpecification = string.Join(",", lensSpecArray.ToArray().Select(x => x.ToString()));

        if (exif.Dictionary["BodySerialNumber"] is NSString bodySerialString)
            metadata.BodySerialNumber = bodySerialString.ToString();

        if (exif.Dictionary["CameraOwnerName"] is NSString ownerString)
            metadata.CameraOwnerName = ownerString.ToString();

        if (exif.Dictionary["ImageUniqueID"] is NSString uniqueIdString)
            metadata.ImageUniqueId = uniqueIdString.ToString();

        if (exif.Dictionary["SpectralSensitivity"] is NSString spectralString)
            metadata.SpectralSensitivity = spectralString.ToString();
    }

    /// <summary>
    /// Extracts image properties from EXIF
    /// </summary>
    private static void ExtractImageProperties(Metadata metadata, CoreGraphics.CGImagePropertiesExif exif)
    {
        if (exif.Dictionary["PixelXDimension"] is NSNumber pixelXNumber)
            metadata.PixelWidth = pixelXNumber.Int32Value;

        if (exif.Dictionary["PixelYDimension"] is NSNumber pixelYNumber)
            metadata.PixelHeight = pixelYNumber.Int32Value;

        if (exif.Dictionary["XResolution"] is NSNumber xResNumber)
            metadata.XResolution = xResNumber.DoubleValue;

        if (exif.Dictionary["YResolution"] is NSNumber yResNumber)
            metadata.YResolution = yResNumber.DoubleValue;

        if (exif.Dictionary["ResolutionUnit"] is NSNumber resUnitNumber)
            metadata.ResolutionUnit = resUnitNumber.StringValue;

        if (exif.Dictionary["ColorSpace"] is NSNumber colorSpaceNumber)
            metadata.ColorSpace = colorSpaceNumber.StringValue;
    }

    /// <summary>
    /// Extracts GPS data from image properties
    /// </summary>
    private static void ExtractGpsData(Metadata metadata, CoreGraphics.CGImagePropertiesGps gps)
    {
        if (gps.Dictionary["Latitude"] is NSNumber latNumber)
            metadata.GpsLatitude = latNumber.DoubleValue;

        if (gps.Dictionary["Longitude"] is NSNumber lonNumber)
            metadata.GpsLongitude = lonNumber.DoubleValue;

        if (gps.Dictionary["Altitude"] is NSNumber altNumber)
            metadata.GpsAltitude = altNumber.DoubleValue;

        if (gps.Dictionary["LatitudeRef"] is NSString latRefString)
            metadata.GpsLatitudeRef = latRefString.ToString();

        if (gps.Dictionary["LongitudeRef"] is NSString lonRefString)
            metadata.GpsLongitudeRef = lonRefString.ToString();

        if (gps.Dictionary["AltitudeRef"] is NSNumber altRefNumber)
            metadata.GpsAltitudeRef = altRefNumber.StringValue;

        if (gps.Dictionary["TimeStamp"] is NSString timeString &&
            DateTime.TryParse(timeString.ToString(), out var gpsTime))
            metadata.GpsTimestamp = gpsTime;
    }

}
