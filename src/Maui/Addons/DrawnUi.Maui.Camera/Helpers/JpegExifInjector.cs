using System.Diagnostics;
using System.Text;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Injects EXIF metadata directly into JPEG streams without temporary files
    /// </summary>
    public static class JpegExifInjector
    {
        /// <summary>
        /// Enable debug logging
        /// </summary>
        public static bool Debug { get; set; } = false;

        /// <summary>
        /// Injects EXIF metadata into a JPEG stream, preserving existing data when new data is missing
        /// </summary>
        /// <param name="jpegStream">The JPEG stream to modify</param>
        /// <param name="newMeta">New metadata to merge with existing</param>
        /// <returns>New stream with merged EXIF metadata</returns>
        public static async Task<Stream> InjectExifMetadata(Stream jpegStream, Metadata newMeta)
        {
            if (!jpegStream.CanSeek || jpegStream.Length == 0)
                throw new InvalidDataException("Input stream must be seekable and non-empty");

            jpegStream.Position = 0;
            var jpegBytes = new byte[jpegStream.Length];
            await jpegStream.ReadAsync(jpegBytes, 0, (int)jpegStream.Length);

            // Step 1: Read existing EXIF data if present
            var existingMeta = ReadExistingMetadata(jpegBytes);

            // Step 2: Merge existing with new (new takes precedence, but preserve existing when new is null/empty)
            var mergedMeta = MergeMetadata(existingMeta, newMeta);

            // Step 3: Create new EXIF segment with merged data
            var exifSegment = CreateExifSegment(mergedMeta);
            var modifiedJpeg = ReplaceOrInsertExifSegment(jpegBytes, exifSegment);

            var outputStream = new MemoryStream(modifiedJpeg);
            outputStream.Position = 0;
            return outputStream;
        }

        /// <summary>
        /// Reads existing EXIF metadata from JPEG bytes
        /// </summary>
        private static Metadata ReadExistingMetadata(byte[] jpegBytes)
        {
            var existingMeta = new Metadata();

            if (jpegBytes.Length < 2 || jpegBytes[0] != 0xFF || jpegBytes[1] != 0xD8)
                return existingMeta;

            int position = 2;

            while (position < jpegBytes.Length - 1)
            {
                if (jpegBytes[position] != 0xFF)
                    break;

                byte marker = jpegBytes[position + 1];
                position += 2;

                if (HasSegmentLength(marker))
                {
                    if (position + 2 > jpegBytes.Length)
                        break;

                    int segmentLength = (jpegBytes[position] << 8) | jpegBytes[position + 1];

                    // Check if this is an EXIF segment
                    if (marker == 0xE1 && IsExifSegment(jpegBytes, position + 2, segmentLength - 2))
                    {
                        try
                        {
                            ParseExifData(jpegBytes, position + 2, segmentLength - 2, existingMeta);
                        }
                        catch (Exception ex)
                        {
                            if (Debug)
                                Super.Log($"[JpegExifInjector] Error parsing existing EXIF: {ex.Message}");
                        }

                        break;
                    }

                    position += segmentLength;
                }
                else
                {
                    break;
                }
            }

            return existingMeta;
        }

        /// <summary>
        /// Parses EXIF data from bytes and populates metadata object
        /// </summary>
        private static void ParseExifData(byte[] jpegBytes, int exifStart, int exifLength, Metadata meta)
        {
            // Skip "Exif\0\0" header
            int tiffStart = exifStart + 6;

            if (tiffStart + 8 > jpegBytes.Length)
                return;

            // Read TIFF header
            bool isLittleEndian = jpegBytes[tiffStart] == 0x49 && jpegBytes[tiffStart + 1] == 0x49;
            int ifd0Offset = ReadUInt32(jpegBytes, tiffStart + 4, isLittleEndian);

            // Parse IFD0
            ParseIfd(jpegBytes, tiffStart, tiffStart + ifd0Offset, isLittleEndian, meta, true);

            // Look for EXIF IFD and GPS IFD pointers in IFD0 and parse them
            int ifd0Position = tiffStart + ifd0Offset;
            if (ifd0Position + 2 <= jpegBytes.Length)
            {
                int entryCount = ReadUInt16(jpegBytes, ifd0Position, isLittleEndian);
                int entryPos = ifd0Position + 2;

                for (int i = 0; i < entryCount && entryPos + 12 <= jpegBytes.Length; i++)
                {
                    int tag = ReadUInt16(jpegBytes, entryPos, isLittleEndian);
                    int valueOffset = ReadUInt32(jpegBytes, entryPos + 8, isLittleEndian);

                    if (tag == 0x8769) // EXIF IFD
                    {
                        ParseIfd(jpegBytes, tiffStart, tiffStart + valueOffset, isLittleEndian, meta, false);
                    }
                    else if (tag == 0x8825) // GPS IFD
                    {
                        ParseGpsIfd(jpegBytes, tiffStart, tiffStart + valueOffset, isLittleEndian, meta);
                    }

                    entryPos += 12;
                }
            }
        }

        /// <summary>
        /// Parses an IFD and extracts metadata
        /// </summary>
        private static void ParseIfd(byte[] data, int tiffStart, int ifdStart, bool isLittleEndian, Metadata meta,
            bool isIfd0)
        {
            if (ifdStart + 2 > data.Length)
                return;

            int entryCount = ReadUInt16(data, ifdStart, isLittleEndian);
            int entryPos = ifdStart + 2;

            for (int i = 0; i < entryCount && entryPos + 12 <= data.Length; i++)
            {
                int tag = ReadUInt16(data, entryPos, isLittleEndian);
                int type = ReadUInt16(data, entryPos + 2, isLittleEndian);
                int count = ReadUInt32(data, entryPos + 4, isLittleEndian);

                try
                {
                    string stringValue = ReadExifString(data, tiffStart, entryPos + 8, type, count, isLittleEndian);
                    double? doubleValue = ReadExifDouble(data, tiffStart, entryPos + 8, type, count, isLittleEndian);
                    int? intValue = ReadExifInt(data, tiffStart, entryPos + 8, type, count, isLittleEndian);

                    // Map common EXIF tags to metadata properties
                    switch (tag)
                    {
                        case 0x010F:
                            if (stringValue != null) meta.Vendor = stringValue;
                            break;
                        case 0x0110:
                            if (stringValue != null) meta.Model = stringValue;
                            break;
                        case 0x0112:
                            if (intValue.HasValue) meta.Orientation = intValue;
                            break;
                        case 0x011A:
                            if (doubleValue.HasValue) meta.XResolution = doubleValue;
                            break;
                        case 0x011B:
                            if (doubleValue.HasValue) meta.YResolution = doubleValue;
                            break;
                        case 0x0128:
                            if (intValue.HasValue) meta.ResolutionUnit = intValue.ToString();
                            break;
                        case 0x0131:
                            if (stringValue != null) meta.Software = stringValue;
                            break;
                        case 0x0132:
                            if (stringValue != null &&
                                DateTime.TryParse(stringValue.Replace(":", "-", StringComparison.InvariantCulture),
                                    out var dt)) meta.DateTimeOriginal = dt;
                            break;
                        case 0x8827:
                            if (intValue.HasValue) meta.ISO = intValue;
                            break;
                        case 0x829A:
                            if (doubleValue.HasValue) meta.Shutter = doubleValue;
                            break;
                        case 0x829D:
                            if (doubleValue.HasValue) meta.Aperture = doubleValue;
                            break;
                        case 0x9003:
                            if (stringValue != null &&
                                DateTime.TryParse(stringValue.Replace(":", "-", StringComparison.InvariantCulture),
                                    out var dto)) meta.DateTimeOriginal = dto;
                            break;
                        case 0x9004:
                            if (stringValue != null &&
                                DateTime.TryParse(stringValue.Replace(":", "-", StringComparison.InvariantCulture),
                                    out var dtd)) meta.DateTimeDigitized = dtd;
                            break;
                        case 0x920A:
                            if (doubleValue.HasValue) meta.FocalLength = doubleValue;
                            break;
                        case 0x9204:
                            if (doubleValue.HasValue) meta.ExposureBias = doubleValue;
                            break;
                        case 0x9209:
                            if (intValue.HasValue) meta.Flash = intValue.ToString();
                            break;
                        case 0xA001:
                            if (intValue.HasValue) meta.ColorSpace = intValue.ToString();
                            break;
                        case 0xA002:
                            if (intValue.HasValue) meta.PixelWidth = intValue;
                            break;
                        case 0xA003:
                            if (intValue.HasValue) meta.PixelHeight = intValue;
                            break;
                        case 0xA403:
                            if (intValue.HasValue) meta.WhiteBalance = intValue.ToString();
                            break;
                        case 0xA404:
                            if (doubleValue.HasValue) meta.DigitalZoomRatio = doubleValue;
                            break;
                        case 0xA405:
                            if (intValue.HasValue) meta.FocalLengthIn35mm = (double)intValue;
                            break;
                        case 0xA430:
                            if (stringValue != null) meta.CameraOwnerName = stringValue;
                            break;
                        case 0xA431:
                            if (stringValue != null) meta.BodySerialNumber = stringValue;
                            break;
                        case 0xA432:
                            if (stringValue != null) meta.LensSpecification = stringValue;
                            break;
                        case 0xA433:
                            if (stringValue != null) meta.LensMake = stringValue;
                            break;
                        case 0xA434:
                            if (stringValue != null) meta.LensModel = stringValue;
                            break;
                        // Add more mappings as needed
                    }
                }
                catch (Exception ex)
                {
                    if (Debug)
                        Super.Log($"[JpegExifInjector] Error reading tag 0x{tag:X4}: {ex.Message}");
                }

                entryPos += 12;
            }
        }

        /// <summary>
        /// Parses GPS IFD
        /// </summary>
        private static void ParseGpsIfd(byte[] data, int tiffStart, int ifdStart, bool isLittleEndian, Metadata meta)
        {
            if (ifdStart + 2 > data.Length)
                return;

            int entryCount = ReadUInt16(data, ifdStart, isLittleEndian);
            int entryPos = ifdStart + 2;

            for (int i = 0; i < entryCount && entryPos + 12 <= data.Length; i++)
            {
                int tag = ReadUInt16(data, entryPos, isLittleEndian);
                int type = ReadUInt16(data, entryPos + 2, isLittleEndian);
                int count = ReadUInt32(data, entryPos + 4, isLittleEndian);

                try
                {
                    string stringValue = ReadExifString(data, tiffStart, entryPos + 8, type, count, isLittleEndian);
                    double? doubleValue = ReadExifDouble(data, tiffStart, entryPos + 8, type, count, isLittleEndian);

                    switch (tag)
                    {
                        case 0x0001:
                            if (stringValue != null) meta.GpsLatitudeRef = stringValue;
                            break;
                        case 0x0002:
                            if (doubleValue.HasValue) meta.GpsLatitude = doubleValue;
                            break;
                        case 0x0003:
                            if (stringValue != null) meta.GpsLongitudeRef = stringValue;
                            break;
                        case 0x0004:
                            if (doubleValue.HasValue) meta.GpsLongitude = doubleValue;
                            break;
                        case 0x0006:
                            if (doubleValue.HasValue) meta.GpsAltitude = doubleValue;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    if (Debug)
                        Super.Log($"[JpegExifInjector] Error reading GPS tag 0x{tag:X4}: {ex.Message}");
                }

                entryPos += 12;
            }
        }

        /// <summary>
        /// Merges existing metadata with new metadata, preserving existing when new is null/empty
        /// </summary>
        private static Metadata MergeMetadata(Metadata existing, Metadata newMeta)
        {
            var merged = new Metadata();

            // Use new value if not null/empty, otherwise use existing
            merged.Vendor = !string.IsNullOrEmpty(newMeta.Vendor) ? newMeta.Vendor : existing.Vendor;
            merged.Model = !string.IsNullOrEmpty(newMeta.Model) ? newMeta.Model : existing.Model;
            merged.Software = !string.IsNullOrEmpty(newMeta.Software) ? newMeta.Software : existing.Software;
            merged.LensMake = !string.IsNullOrEmpty(newMeta.LensMake) ? newMeta.LensMake : existing.LensMake;
            merged.LensModel = !string.IsNullOrEmpty(newMeta.LensModel) ? newMeta.LensModel : existing.LensModel;
            merged.LensSpecification = !string.IsNullOrEmpty(newMeta.LensSpecification)
                ? newMeta.LensSpecification
                : existing.LensSpecification;
            merged.BodySerialNumber = !string.IsNullOrEmpty(newMeta.BodySerialNumber)
                ? newMeta.BodySerialNumber
                : existing.BodySerialNumber;
            merged.CameraOwnerName = !string.IsNullOrEmpty(newMeta.CameraOwnerName)
                ? newMeta.CameraOwnerName
                : existing.CameraOwnerName;
            merged.ImageUniqueId = !string.IsNullOrEmpty(newMeta.ImageUniqueId)
                ? newMeta.ImageUniqueId
                : existing.ImageUniqueId;

            // Numeric values
            merged.Orientation = newMeta.Orientation ?? existing.Orientation;
            merged.ISO = newMeta.ISO ?? existing.ISO;
            merged.FocalLength = newMeta.FocalLength ?? existing.FocalLength;
            merged.Aperture = newMeta.Aperture ?? existing.Aperture;
            merged.Shutter = newMeta.Shutter ?? existing.Shutter;
            merged.FocalLengthIn35mm = newMeta.FocalLengthIn35mm ?? existing.FocalLengthIn35mm;
            merged.PixelWidth = newMeta.PixelWidth ?? existing.PixelWidth;
            merged.PixelHeight = newMeta.PixelHeight ?? existing.PixelHeight;
            merged.XResolution = newMeta.XResolution ?? existing.XResolution;
            merged.YResolution = newMeta.YResolution ?? existing.YResolution;
            merged.ExposureBias = newMeta.ExposureBias ?? existing.ExposureBias;
            merged.DigitalZoomRatio = newMeta.DigitalZoomRatio ?? existing.DigitalZoomRatio;
            merged.BrightnessValue = newMeta.BrightnessValue ?? existing.BrightnessValue;

            // GPS
            merged.GpsLatitude = newMeta.GpsLatitude ?? existing.GpsLatitude;
            merged.GpsLongitude = newMeta.GpsLongitude ?? existing.GpsLongitude;
            merged.GpsAltitude = newMeta.GpsAltitude ?? existing.GpsAltitude;
            merged.GpsLatitudeRef = !string.IsNullOrEmpty(newMeta.GpsLatitudeRef)
                ? newMeta.GpsLatitudeRef
                : existing.GpsLatitudeRef;
            merged.GpsLongitudeRef = !string.IsNullOrEmpty(newMeta.GpsLongitudeRef)
                ? newMeta.GpsLongitudeRef
                : existing.GpsLongitudeRef;
            merged.GpsTimestamp = newMeta.GpsTimestamp ?? existing.GpsTimestamp;

            // Date/Time
            merged.DateTimeOriginal = newMeta.DateTimeOriginal ?? existing.DateTimeOriginal;
            merged.DateTimeDigitized = newMeta.DateTimeDigitized ?? existing.DateTimeDigitized;

            // String fields that might be numeric codes
            merged.Flash = !string.IsNullOrEmpty(newMeta.Flash) ? newMeta.Flash : existing.Flash;
            merged.WhiteBalance = !string.IsNullOrEmpty(newMeta.WhiteBalance)
                ? newMeta.WhiteBalance
                : existing.WhiteBalance;
            merged.ColorSpace = !string.IsNullOrEmpty(newMeta.ColorSpace) ? newMeta.ColorSpace : existing.ColorSpace;
            merged.ResolutionUnit = !string.IsNullOrEmpty(newMeta.ResolutionUnit)
                ? newMeta.ResolutionUnit
                : existing.ResolutionUnit;
            merged.ExposureMode = !string.IsNullOrEmpty(newMeta.ExposureMode)
                ? newMeta.ExposureMode
                : existing.ExposureMode;
            merged.MeteringMode = !string.IsNullOrEmpty(newMeta.MeteringMode)
                ? newMeta.MeteringMode
                : existing.MeteringMode;
            merged.SceneCaptureType = !string.IsNullOrEmpty(newMeta.SceneCaptureType)
                ? newMeta.SceneCaptureType
                : existing.SceneCaptureType;
            merged.ExposureProgram = !string.IsNullOrEmpty(newMeta.ExposureProgram)
                ? newMeta.ExposureProgram
                : existing.ExposureProgram;
            merged.SceneType = !string.IsNullOrEmpty(newMeta.SceneType) ? newMeta.SceneType : existing.SceneType;
            merged.CustomRendered = !string.IsNullOrEmpty(newMeta.CustomRendered)
                ? newMeta.CustomRendered
                : existing.CustomRendered;
            merged.GainControl =
                !string.IsNullOrEmpty(newMeta.GainControl) ? newMeta.GainControl : existing.GainControl;
            merged.Contrast = !string.IsNullOrEmpty(newMeta.Contrast) ? newMeta.Contrast : existing.Contrast;
            merged.Saturation = !string.IsNullOrEmpty(newMeta.Saturation) ? newMeta.Saturation : existing.Saturation;
            merged.Sharpness = !string.IsNullOrEmpty(newMeta.Sharpness) ? newMeta.Sharpness : existing.Sharpness;
            merged.SubjectArea =
                !string.IsNullOrEmpty(newMeta.SubjectArea) ? newMeta.SubjectArea : existing.SubjectArea;
            merged.SubjectDistanceRange = !string.IsNullOrEmpty(newMeta.SubjectDistanceRange)
                ? newMeta.SubjectDistanceRange
                : existing.SubjectDistanceRange;
            merged.SensingMethod = !string.IsNullOrEmpty(newMeta.SensingMethod)
                ? newMeta.SensingMethod
                : existing.SensingMethod;
            merged.SpectralSensitivity = !string.IsNullOrEmpty(newMeta.SpectralSensitivity)
                ? newMeta.SpectralSensitivity
                : existing.SpectralSensitivity;
            merged.SubsecTime = !string.IsNullOrEmpty(newMeta.SubsecTime) ? newMeta.SubsecTime : existing.SubsecTime;
            merged.SubsecTimeOriginal = !string.IsNullOrEmpty(newMeta.SubsecTimeOriginal)
                ? newMeta.SubsecTimeOriginal
                : existing.SubsecTimeOriginal;
            merged.SubsecTimeDigitized = !string.IsNullOrEmpty(newMeta.SubsecTimeDigitized)
                ? newMeta.SubsecTimeDigitized
                : existing.SubsecTimeDigitized;

            if (Debug)
                Super.Log(
                    $"[JpegExifInjector] Merged metadata - preserved {(string.IsNullOrEmpty(merged.Vendor) ? "none" : "vendor")}, {(string.IsNullOrEmpty(merged.LensMake) ? "none" : "lens make")}");

            return merged;
        }

        // Helper methods for reading EXIF data
        private static int ReadUInt16(byte[] data, int offset, bool isLittleEndian)
        {
            if (offset + 2 > data.Length) return 0;
            return isLittleEndian ? data[offset] | (data[offset + 1] << 8) : (data[offset] << 8) | data[offset + 1];
        }

        private static int ReadUInt32(byte[] data, int offset, bool isLittleEndian)
        {
            if (offset + 4 > data.Length) return 0;
            return isLittleEndian
                ? data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24)
                : (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        private static string ReadExifString(byte[] data, int tiffStart, int valueOffset, int type, int count,
            bool isLittleEndian)
        {
            if (type != 2) return null; // Not a string type

            int actualOffset = count <= 4 ? valueOffset : tiffStart + ReadUInt32(data, valueOffset, isLittleEndian);
            if (actualOffset + count > data.Length) return null;

            var bytes = new byte[Math.Max(0, count - 1)]; // Exclude null terminator
            Array.Copy(data, actualOffset, bytes, 0, bytes.Length);
            return Encoding.ASCII.GetString(bytes);
        }

        private static double? ReadExifDouble(byte[] data, int tiffStart, int valueOffset, int type, int count,
            bool isLittleEndian)
        {
            if (type == 5) // Rational
            {
                int actualOffset = tiffStart + ReadUInt32(data, valueOffset, isLittleEndian);
                if (actualOffset + 8 > data.Length) return null;

                int numerator = ReadUInt32(data, actualOffset, isLittleEndian);
                int denominator = ReadUInt32(data, actualOffset + 4, isLittleEndian);

                return denominator != 0 ? (double)numerator / denominator : null;
            }

            return null;
        }

        private static int? ReadExifInt(byte[] data, int tiffStart, int valueOffset, int type, int count,
            bool isLittleEndian)
        {
            if (type == 3) // Short
            {
                return ReadUInt16(data, valueOffset, isLittleEndian);
            }
            else if (type == 4) // Long
            {
                return count <= 1
                    ? ReadUInt32(data, valueOffset, isLittleEndian)
                    : ReadUInt32(data, tiffStart + ReadUInt32(data, valueOffset, isLittleEndian), isLittleEndian);
            }

            return null;
        }

        // Rest of the original methods remain the same...

        /// <summary>
        /// Replaces existing EXIF or inserts new EXIF segment in proper location
        /// </summary>
        private static byte[] ReplaceOrInsertExifSegment(byte[] jpegBytes, byte[] exifSegment)
        {
            if (jpegBytes.Length < 2 || jpegBytes[0] != 0xFF || jpegBytes[1] != 0xD8)
                throw new InvalidDataException("Invalid JPEG file");

            int position = 2;
            int insertionPoint = 2;
            int existingExifStart = -1;
            int existingExifEnd = -1;

            while (position < jpegBytes.Length - 1)
            {
                if (jpegBytes[position] != 0xFF)
                    break;

                byte marker = jpegBytes[position + 1];
                position += 2;

                if (HasSegmentLength(marker))
                {
                    if (position + 2 > jpegBytes.Length)
                        break;

                    int segmentLength = (jpegBytes[position] << 8) | jpegBytes[position + 1];
                    int segmentStart = position - 2;
                    int segmentEnd = position + segmentLength;

                    if (marker == 0xE0)
                    {
                        insertionPoint = segmentEnd;
                    }
                    else if (marker == 0xE1)
                    {
                        if (IsExifSegment(jpegBytes, position + 2, segmentLength - 2))
                        {
                            existingExifStart = segmentStart;
                            existingExifEnd = segmentEnd;
                        }
                        else if (existingExifStart == -1)
                        {
                            insertionPoint = segmentStart;
                        }
                    }
                    else if (marker >= 0xE2 && marker <= 0xEF)
                    {
                        if (existingExifStart == -1)
                            insertionPoint = segmentStart;
                    }
                    else if (marker == 0xDB || marker == 0xC0 || marker == 0xC2)
                    {
                        if (existingExifStart == -1)
                            insertionPoint = segmentStart;
                        break;
                    }

                    position = segmentEnd;
                }
                else
                {
                    break;
                }
            }

            if (Debug)
                Super.Log(
                    $"[JpegExifInjector] Insertion point: {insertionPoint}, Existing EXIF: {existingExifStart}-{existingExifEnd}");

            byte[] result;
            if (existingExifStart != -1)
            {
                var existingExifLength = existingExifEnd - existingExifStart;
                result = new byte[jpegBytes.Length - existingExifLength + exifSegment.Length];
                Array.Copy(jpegBytes, 0, result, 0, existingExifStart);
                Array.Copy(exifSegment, 0, result, existingExifStart, exifSegment.Length);
                Array.Copy(jpegBytes, existingExifEnd, result, existingExifStart + exifSegment.Length,
                    jpegBytes.Length - existingExifEnd);
            }
            else
            {
                result = new byte[jpegBytes.Length + exifSegment.Length];
                Array.Copy(jpegBytes, 0, result, 0, insertionPoint);
                Array.Copy(exifSegment, 0, result, insertionPoint, exifSegment.Length);
                Array.Copy(jpegBytes, insertionPoint, result, insertionPoint + exifSegment.Length,
                    jpegBytes.Length - insertionPoint);
            }

            return result;
        }

        /// <summary>
        /// Checks if a JPEG marker has a length field
        /// </summary>
        private static bool HasSegmentLength(byte marker)
        {
            return marker != 0xD8 &&
                   marker != 0xD9 &&
                   marker != 0x01 &&
                   (marker < 0xD0 || marker > 0xD7);
        }

        /// <summary>
        /// Checks if an APP1 segment contains EXIF data
        /// </summary>
        private static bool IsExifSegment(byte[] jpegBytes, int dataStart, int dataLength)
        {
            if (dataLength < 6)
                return false;

            return jpegBytes[dataStart] == 0x45 &&
                   jpegBytes[dataStart + 1] == 0x78 &&
                   jpegBytes[dataStart + 2] == 0x69 &&
                   jpegBytes[dataStart + 3] == 0x66 &&
                   jpegBytes[dataStart + 4] == 0x00 &&
                   jpegBytes[dataStart + 5] == 0x00;
        }

        /// <summary>
        /// Creates a complete EXIF APP1 segment with metadata
        /// </summary>
        private static byte[] CreateExifSegment(Metadata meta)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write((byte)0xFF);
            writer.Write((byte)0xE1);
            var lengthPosition = stream.Position;
            writer.Write((ushort)0);

            writer.Write(Encoding.ASCII.GetBytes("Exif\0\0"));
            var tiffStart = stream.Position;
            if (Debug)
                Super.Log($"[JpegExifInjector] TIFF header starts at position {tiffStart}");

            writer.Write((byte)0x49);
            writer.Write((byte)0x49);
            writer.Write((ushort)0x002A);
            writer.Write((uint)8);

            var ifd0Entries = CreateIfd0Entries(meta);
            var exifIfdEntries = CreateExifIfdEntries(meta);
            var gpsIfdEntries = CreateGpsIfdEntries(meta);

            if (Debug)
                Super.Log(
                    $"[JpegExifInjector] Created {ifd0Entries.Count} IFD0 entries, {exifIfdEntries.Count} EXIF entries, {gpsIfdEntries.Count} GPS entries");

            if (exifIfdEntries.Count > 0)
                ifd0Entries.Add(new IfdEntry(0x8769, 4, (uint)0));
            if (gpsIfdEntries.Count > 0)
                ifd0Entries.Add(new IfdEntry(0x8825, 4, (uint)0));

            ifd0Entries = ifd0Entries.OrderBy(e => e.Tag).ToList();

            if (Debug)
                Super.Log($"[JpegExifInjector] Starting IFD0 at position {stream.Position}");
            var ifd0Offset = WriteIfd(writer, ifd0Entries, tiffStart);

            uint exifIfdOffset = 0;
            if (exifIfdEntries.Count > 0)
            {
                exifIfdOffset = (uint)(stream.Position - tiffStart);
                if (Debug)
                    Super.Log(
                        $"[JpegExifInjector] Starting EXIF IFD at position {stream.Position}, offset from TIFF: {exifIfdOffset}");
                WriteIfd(writer, exifIfdEntries, tiffStart);
                UpdateIfdPointer(stream, tiffStart, ifd0Offset, 0x8769, exifIfdOffset);
            }

            uint gpsIfdOffset = 0;
            if (gpsIfdEntries.Count > 0)
            {
                gpsIfdOffset = (uint)(stream.Position - tiffStart);
                if (Debug)
                    Super.Log(
                        $"[JpegExifInjector] Starting GPS IFD at position {stream.Position}, offset from TIFF: {gpsIfdOffset}");
                WriteIfd(writer, gpsIfdEntries, tiffStart);
                UpdateIfdPointer(stream, tiffStart, ifd0Offset, 0x8825, gpsIfdOffset);
            }

            var segmentEnd = stream.Position;
            var segmentLength = (ushort)(segmentEnd - lengthPosition);
            if (Debug)
                Super.Log(
                    $"[JpegExifInjector] Segment ends at position {segmentEnd}, calculated length: {segmentLength}");

            stream.Seek(lengthPosition, SeekOrigin.Begin);
            writer.Write(ReverseBytes(segmentLength));

            return stream.ToArray();
        }

        /// <summary>
        /// Creates IFD0 entries (main image metadata)
        /// </summary>
        private static List<IfdEntry> CreateIfd0Entries(Metadata meta)
        {
            var entries = new List<IfdEntry>();

            if (!string.IsNullOrEmpty(meta.Vendor))
                entries.Add(new IfdEntry(0x010F, 2, meta.Vendor));

            if (!string.IsNullOrEmpty(meta.Model))
                entries.Add(new IfdEntry(0x0110, 2, meta.Model));

            if (meta.Orientation.HasValue)
                entries.Add(new IfdEntry(0x0112, 3, (ushort)meta.Orientation.Value));

            if (meta.XResolution.HasValue)
                entries.Add(new IfdEntry(0x011A, 5, CreateRational(meta.XResolution.Value)));

            if (meta.YResolution.HasValue)
                entries.Add(new IfdEntry(0x011B, 5, CreateRational(meta.YResolution.Value)));

            if (!string.IsNullOrEmpty(meta.ResolutionUnit) && ushort.TryParse(meta.ResolutionUnit, out var resUnit))
                entries.Add(new IfdEntry(0x0128, 3, resUnit));

            if (!string.IsNullOrEmpty(meta.Software))
                entries.Add(new IfdEntry(0x0131, 2, meta.Software));

            if (meta.DateTimeOriginal.HasValue)
                entries.Add(new IfdEntry(0x0132, 2,
                    meta.DateTimeOriginal.Value.ToString("yyyy:MM:dd HH:mm:ss")));

            if (!string.IsNullOrEmpty(meta.CameraOwnerName))
                entries.Add(new IfdEntry(0xA430, 2, meta.CameraOwnerName));

            if (!string.IsNullOrEmpty(meta.BodySerialNumber))
                entries.Add(new IfdEntry(0xA431, 2, meta.BodySerialNumber));

            return entries;
        }

        /// <summary>
        /// Creates EXIF IFD entries (camera-specific metadata)
        /// </summary>
        private static List<IfdEntry> CreateExifIfdEntries(Metadata meta)
        {
            var entries = new List<IfdEntry>();

            // EXIF Version - essential for iPhone recognition
            entries.Add(new IfdEntry(0x9000, 7, new byte[] { 0x30, 0x32, 0x33, 0x32 })); // "0232" for EXIF 2.32

            // Essential camera information for iPhone recognition
            if (meta.ExposureBias.HasValue)
                entries.Add(new IfdEntry(0x9204, 10, CreateSignedRational(meta.ExposureBias.Value)));

            if (meta.ISO.HasValue)
                entries.Add(new IfdEntry(0x8827, 3, (ushort)meta.ISO.Value));

            if (meta.DateTimeOriginal.HasValue)
                entries.Add(new IfdEntry(0x9003, 2,
                    meta.DateTimeOriginal.Value.ToString("yyyy:MM:dd HH:mm:ss")));

            if (meta.DateTimeDigitized.HasValue)
                entries.Add(new IfdEntry(0x9004, 2,
                    meta.DateTimeDigitized.Value.ToString("yyyy:MM:dd HH:mm:ss")));

            if (meta.Shutter.HasValue)
                entries.Add(new IfdEntry(0x829A, 5, CreateRational(meta.Shutter.Value)));

            if (meta.Aperture.HasValue)
                entries.Add(new IfdEntry(0x829D, 5, CreateRational(meta.Aperture.Value)));

            if (meta.FocalLength.HasValue)
                entries.Add(new IfdEntry(0x920A, 5, CreateRational(meta.FocalLength.Value)));

            if (meta.FocalLengthIn35mm.HasValue)
                entries.Add(new IfdEntry(0xA405, 3, (ushort)meta.FocalLengthIn35mm.Value));

            if (meta.PixelWidth.HasValue)
                entries.Add(new IfdEntry(0xA002, 4, (uint)meta.PixelWidth.Value));

            if (meta.PixelHeight.HasValue)
                entries.Add(new IfdEntry(0xA003, 4, (uint)meta.PixelHeight.Value));

            if (!string.IsNullOrEmpty(meta.ColorSpace) && ushort.TryParse(meta.ColorSpace, out var colorSpace))
                entries.Add(new IfdEntry(0xA001, 3, colorSpace));

            if (!string.IsNullOrEmpty(meta.Flash) && ushort.TryParse(meta.Flash, out var flash))
                entries.Add(new IfdEntry(0x9209, 3, flash));

            if (!string.IsNullOrEmpty(meta.WhiteBalance) && ushort.TryParse(meta.WhiteBalance, out var wb))
                entries.Add(new IfdEntry(0xA403, 3, wb));

            if (meta.DigitalZoomRatio.HasValue)
                entries.Add(new IfdEntry(0xA404, 5, CreateRational(meta.DigitalZoomRatio.Value)));

            // Lens Information - essential for camera recognition
            if (!string.IsNullOrEmpty(meta.LensMake))
                entries.Add(new IfdEntry(0xA433, 2, meta.LensMake));

            if (!string.IsNullOrEmpty(meta.LensModel))
                entries.Add(new IfdEntry(0xA434, 2, meta.LensModel));

            if (!string.IsNullOrEmpty(meta.LensSpecification))
                entries.Add(new IfdEntry(0xA432, 2, meta.LensSpecification));

            // Additional fields with safe parsing
            if (!string.IsNullOrEmpty(meta.ExposureMode) && ushort.TryParse(meta.ExposureMode, out var expMode))
                entries.Add(new IfdEntry(0xA402, 3, expMode));

            if (!string.IsNullOrEmpty(meta.MeteringMode) && ushort.TryParse(meta.MeteringMode, out var metering))
                entries.Add(new IfdEntry(0x9207, 3, metering));

            if (!string.IsNullOrEmpty(meta.SceneCaptureType) && ushort.TryParse(meta.SceneCaptureType, out var scene))
                entries.Add(new IfdEntry(0xA406, 3, scene));

            if (meta.BrightnessValue.HasValue)
                entries.Add(new IfdEntry(0x9203, 10, CreateSignedRational(meta.BrightnessValue.Value)));

            if (!string.IsNullOrEmpty(meta.ExposureProgram) && ushort.TryParse(meta.ExposureProgram, out var expProg))
                entries.Add(new IfdEntry(0x8822, 3, expProg));

            if (!string.IsNullOrEmpty(meta.SceneType) && byte.TryParse(meta.SceneType, out var sceneType))
                entries.Add(new IfdEntry(0xA301, 1, sceneType));

            if (!string.IsNullOrEmpty(meta.CustomRendered) && ushort.TryParse(meta.CustomRendered, out var custom))
                entries.Add(new IfdEntry(0xA401, 3, custom));

            if (!string.IsNullOrEmpty(meta.GainControl) && ushort.TryParse(meta.GainControl, out var gain))
                entries.Add(new IfdEntry(0xA407, 3, gain));

            if (!string.IsNullOrEmpty(meta.Contrast) && ushort.TryParse(meta.Contrast, out var contrast))
                entries.Add(new IfdEntry(0xA408, 3, contrast));

            if (!string.IsNullOrEmpty(meta.Saturation) && ushort.TryParse(meta.Saturation, out var saturation))
                entries.Add(new IfdEntry(0xA409, 3, saturation));

            if (!string.IsNullOrEmpty(meta.Sharpness) && ushort.TryParse(meta.Sharpness, out var sharpness))
                entries.Add(new IfdEntry(0xA40A, 3, sharpness));

            if (!string.IsNullOrEmpty(meta.SubjectArea))
                entries.Add(new IfdEntry(0x9214, 2, meta.SubjectArea));

            if (!string.IsNullOrEmpty(meta.SubjectDistanceRange) &&
                ushort.TryParse(meta.SubjectDistanceRange, out var subDist))
                entries.Add(new IfdEntry(0xA40C, 3, subDist));

            if (!string.IsNullOrEmpty(meta.SensingMethod) && ushort.TryParse(meta.SensingMethod, out var sensing))
                entries.Add(new IfdEntry(0xA217, 3, sensing));

            if (!string.IsNullOrEmpty(meta.ImageUniqueId))
                entries.Add(new IfdEntry(0xA420, 2, meta.ImageUniqueId));

            if (!string.IsNullOrEmpty(meta.SpectralSensitivity))
                entries.Add(new IfdEntry(0x8824, 2, meta.SpectralSensitivity));

            // Subsec time information
            if (!string.IsNullOrEmpty(meta.SubsecTime))
                entries.Add(new IfdEntry(0x9290, 2, meta.SubsecTime));

            if (!string.IsNullOrEmpty(meta.SubsecTimeOriginal))
                entries.Add(new IfdEntry(0x9291, 2, meta.SubsecTimeOriginal));

            if (!string.IsNullOrEmpty(meta.SubsecTimeDigitized))
                entries.Add(new IfdEntry(0x9292, 2, meta.SubsecTimeDigitized));

            return entries.OrderBy(e => e.Tag).ToList();
        }

        /// <summary>
        /// Creates GPS IFD entries
        /// </summary>
        private static List<IfdEntry> CreateGpsIfdEntries(Metadata meta)
        {
            var entries = new List<IfdEntry>();

            if (meta.GpsLatitude.HasValue && meta.GpsLongitude.HasValue)
            {
                entries.Add(new IfdEntry(0x0001, 2,
                    meta.GpsLatitudeRef ?? (meta.GpsLatitude.Value >= 0 ? "N" : "S")));
                entries.Add(new IfdEntry(0x0002, 5, CreateGpsCoordinate(Math.Abs(meta.GpsLatitude.Value))));
                entries.Add(new IfdEntry(0x0003, 2,
                    meta.GpsLongitudeRef ?? (meta.GpsLongitude.Value >= 0 ? "E" : "W")));
                entries.Add(new IfdEntry(0x0004, 5,
                    CreateGpsCoordinate(Math.Abs(meta.GpsLongitude.Value))));
            }

            if (meta.GpsAltitude.HasValue)
            {
                entries.Add(new IfdEntry(0x0005, 1, (byte)(meta.GpsAltitude.Value >= 0 ? 0 : 1)));
                entries.Add(new IfdEntry(0x0006, 5, CreateRational(Math.Abs(meta.GpsAltitude.Value))));
            }

            if (meta.GpsTimestamp.HasValue)
            {
                var time = meta.GpsTimestamp.Value;
                entries.Add(new IfdEntry(0x0007, 5, CreateGpsTime(time)));
                entries.Add(new IfdEntry(0x001D, 2, time.ToString("yyyy:MM:dd")));
            }

            return entries.OrderBy(e => e.Tag).ToList();
        }

        /// <summary>
        /// Writes an IFD and returns its offset from TIFF start
        /// </summary>
        private static uint WriteIfd(BinaryWriter writer, List<IfdEntry> entries, long tiffStart)
        {
            var ifdStart = writer.BaseStream.Position;
            var ifdOffset = (uint)(ifdStart - tiffStart);

            entries = entries.OrderBy(e => e.Tag).ToList();

            writer.Write((ushort)entries.Count);
            if (Debug)
                Super.Log($"[JpegExifInjector] Writing IFD with {entries.Count} entries at position {ifdStart}");

            var largeDataEntries = entries.Where(e => e.DataLength > 4).ToList();
            var dataStart = writer.BaseStream.Position + (entries.Count * 12) + 4;

            if (Debug)
                Super.Log(
                    $"[JpegExifInjector] Data will start at position {dataStart}, offset from TIFF: {dataStart - tiffStart}");

            var dataOffsets = new Dictionary<IfdEntry, uint>();
            var currentDataOffset = (uint)(dataStart - tiffStart);

            foreach (var entry in largeDataEntries)
            {
                dataOffsets[entry] = currentDataOffset;
                if (Debug)
                    Super.Log(
                        $"[JpegExifInjector] Pre-calculating offset for tag 0x{entry.Tag:X4}: {currentDataOffset} (length {entry.DataLength})");
                currentDataOffset += (uint)entry.DataLength;
                if (currentDataOffset % 2 == 1) currentDataOffset++;
            }

            foreach (var entry in entries)
            {
                if (Debug)
                    Super.Log(
                        $"[JpegExifInjector] Processing tag 0x{entry.Tag:X4}, type {entry.Type}, count {entry.Count}, data length {entry.DataLength}");
                writer.Write(entry.Tag);
                writer.Write(entry.Type);
                writer.Write(entry.Count);

                if (entry.DataLength <= 4)
                {
                    WriteEntryData(writer, entry);
                    var paddingBytes = 4 - entry.DataLength;
                    for (int i = 0; i < paddingBytes; i++)
                        writer.Write((byte)0);
                }
                else
                {
                    var offset = dataOffsets[entry];
                    writer.Write(offset);
                    if (Debug)
                        Super.Log($"[JpegExifInjector] Writing offset {offset} for tag 0x{entry.Tag:X4}");
                }
            }

            writer.Write((uint)0);

            if (Debug)
                Super.Log(
                    $"[JpegExifInjector] Starting to write data at position {writer.BaseStream.Position}, expected {dataStart}");

            foreach (var entry in largeDataEntries)
            {
                var expectedPosition = tiffStart + dataOffsets[entry];
                var actualPosition = writer.BaseStream.Position;
                if (Debug)
                    Super.Log(
                        $"[JpegExifInjector] Writing data for tag 0x{entry.Tag:X4} at position {actualPosition}, expected {expectedPosition}");

                if (actualPosition != expectedPosition && Debug)
                {
                    Super.Log(
                        $"[JpegExifInjector] ERROR: Position mismatch for tag 0x{entry.Tag:X4}! Expected {expectedPosition}, actual {actualPosition}");
                }

                WriteEntryData(writer, entry);
                if (writer.BaseStream.Position % 2 == 1)
                    writer.Write((byte)0);
            }

            return ifdOffset;
        }

        /// <summary>
        /// Writes entry data based on type
        /// </summary>
        private static void WriteEntryData(BinaryWriter writer, IfdEntry entry)
        {
            switch (entry.Type)
            {
                case 1:
                    writer.Write((byte)entry.Data);
                    break;
                case 2:
                    var str = (string)entry.Data;
                    writer.Write(Encoding.ASCII.GetBytes(str));
                    writer.Write((byte)0);
                    break;
                case 3:
                    writer.Write((ushort)entry.Data);
                    break;
                case 4:
                    writer.Write((uint)entry.Data);
                    break;
                case 5:
                    var rational = (uint[])entry.Data;
                    if (Debug)
                        Super.Log(
                            $"[JpegExifInjector] Writing rational data for tag 0x{entry.Tag:X4}: {rational[0]}/{rational[1]}");
                    for (int i = 0; i < rational.Length; i++)
                        writer.Write(rational[i]);
                    break;
                case 7:
                    var bytes = (byte[])entry.Data;
                    writer.Write(bytes);
                    break;
                case 10:
                    var signedRational = (int[])entry.Data;
                    if (Debug)
                        Super.Log(
                            $"[JpegExifInjector] Writing signed rational data for tag 0x{entry.Tag:X4}: {signedRational[0]}/{signedRational[1]}");
                    for (int i = 0; i < signedRational.Length; i++)
                        writer.Write(signedRational[i]);
                    break;
            }
        }

        /// <summary>
        /// Updates an IFD pointer value
        /// </summary>
        private static void UpdateIfdPointer(Stream stream, long tiffStart, uint ifdOffset, ushort tag, uint newOffset)
        {
            var currentPos = stream.Position;
            stream.Position = tiffStart + ifdOffset;

            var entryCount = ReadUInt16(stream);
            if (Debug)
                Super.Log(
                    $"[JpegExifInjector] Updating IFD pointer for tag 0x{tag:X4}, entry count: {entryCount}, new offset: {newOffset}");

            for (int i = 0; i < entryCount; i++)
            {
                var entryPos = stream.Position;
                var entryTag = ReadUInt16(stream);
                if (Debug)
                    Super.Log($"[JpegExifInjector] Checking entry {i}, tag: 0x{entryTag:X4}");

                if (entryTag == tag)
                {
                    stream.Position = entryPos + 8;
                    var writer = new BinaryWriter(stream);
                    writer.Write(newOffset);
                    if (Debug)
                        Super.Log($"[JpegExifInjector] Updated tag 0x{tag:X4} with offset {newOffset}");
                    break;
                }
                else
                {
                    stream.Position = entryPos + 12;
                }
            }

            stream.Position = currentPos;
        }

        /// <summary>
        /// Creates a rational number (two 32-bit unsigned integers)
        /// </summary>
        private static uint[] CreateRational(double value)
        {
            if (value == 0 || double.IsNaN(value) || double.IsInfinity(value))
                return new uint[] { 0, 1 };
            if (value < 1 && value > 0)
            {
                var denominator = (uint)Math.Round(1 / value);
                if (denominator == 0) return new uint[] { 0, 1 };
                return new uint[] { 1, denominator };
            }

            var numerator = (uint)(value * 100);
            return new uint[] { numerator, 100 };
        }

        /// <summary>
        /// Creates a signed rational number (two 32-bit signed integers)
        /// </summary>
        private static int[] CreateSignedRational(double value)
        {
            if (value == 0) return new int[] { 0, 1 };

            var numerator = (int)(value * 100);
            return new int[] { numerator, 100 };
        }

        /// <summary>
        /// Creates GPS coordinate in degrees, minutes, seconds format
        /// </summary>
        private static uint[] CreateGpsCoordinate(double coordinate)
        {
            var degrees = (uint)coordinate;
            var minutesFloat = (coordinate - degrees) * 60;
            var minutes = (uint)minutesFloat;
            var secondsFloat = (minutesFloat - minutes) * 60;
            var seconds = (uint)(secondsFloat * 1000);

            return new uint[] { degrees, 1, minutes, 1, seconds, 1000 };
        }

        /// <summary>
        /// Creates GPS time in hours, minutes, seconds format
        /// </summary>
        private static uint[] CreateGpsTime(DateTime time)
        {
            return new uint[]
            {
                (uint)time.Hour, 1, (uint)time.Minute, 1, (uint)(time.Second * 1000 + time.Millisecond), 1000
            };
        }

        /// <summary>
        /// Reverses bytes for big-endian values
        /// </summary>
        private static ushort ReverseBytes(ushort value)
        {
            return (ushort)((value << 8) | (value >> 8));
        }

        /// <summary>
        /// Reads a 16-bit unsigned integer from stream
        /// </summary>
        private static ushort ReadUInt16(Stream stream)
        {
            var bytes = new byte[2];
            stream.Read(bytes, 0, 2);
            return (ushort)(bytes[0] | (bytes[1] << 8));
        }

        /// <summary>
        /// Represents an IFD entry
        /// </summary>
        public class IfdEntry
        {
            public ushort Tag { get; }
            public ushort Type { get; }
            public uint Count { get; }
            public object Data { get; }
            public int DataLength { get; }

            public IfdEntry(ushort tag, ushort type, object data)
            {
                Tag = tag;
                Type = type;
                Data = data;

                switch (type)
                {
                    case 1:
                        Count = 1;
                        DataLength = 1;
                        break;
                    case 2:
                        var str = (string)data;
                        Count = (uint)(str.Length + 1);
                        DataLength = str.Length + 1;
                        break;
                    case 3:
                        Count = 1;
                        DataLength = 2;
                        break;
                    case 4:
                        Count = 1;
                        DataLength = 4;
                        break;
                    case 5:
                        var rational = (uint[])data;
                        Count = (uint)(rational.Length / 2);
                        DataLength = rational.Length * 4;
                        break;
                    case 7:
                        var bytes = (byte[])data;
                        Count = (uint)bytes.Length;
                        DataLength = bytes.Length;
                        break;
                    case 10:
                        var signedRational = (int[])data;
                        Count = (uint)(signedRational.Length / 2);
                        DataLength = signedRational.Length * 4;
                        break;
                    default:
                        throw new ArgumentException($"Unsupported EXIF type: {type}");
                }
            }
        }
    }
}
