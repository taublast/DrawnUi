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
        /// Injects EXIF metadata into a JPEG stream without temporary files
        /// </summary>
        /// <param name="jpegStream">The JPEG stream to modify</param>
        /// <param name="meta">Metadata to embed</param>
        /// <returns>New stream with EXIF metadata embedded</returns>
        public static async Task<Stream> InjectExifMetadata(Stream jpegStream, Metadata meta)
        {
            if (!jpegStream.CanSeek || jpegStream.Length == 0)
                throw new InvalidDataException("Input stream must be seekable and non-empty");

            jpegStream.Position = 0;
            var jpegBytes = new byte[jpegStream.Length];
            await jpegStream.ReadAsync(jpegBytes, 0, (int)jpegStream.Length);

            var exifSegment = CreateExifSegment(meta);
            var modifiedJpeg = ReplaceOrInsertExifSegment(jpegBytes, exifSegment);

            var outputStream = new MemoryStream(modifiedJpeg);
            outputStream.Position = 0;
            return outputStream;
        }

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
                Super.Log($"[JpegExifInjector] Insertion point: {insertionPoint}, Existing EXIF: {existingExifStart}-{existingExifEnd}");

            byte[] result;
            if (existingExifStart != -1)
            {
                var existingExifLength = existingExifEnd - existingExifStart;
                result = new byte[jpegBytes.Length - existingExifLength + exifSegment.Length];
                Array.Copy(jpegBytes, 0, result, 0, existingExifStart);
                Array.Copy(exifSegment, 0, result, existingExifStart, exifSegment.Length);
                Array.Copy(jpegBytes, existingExifEnd, result, existingExifStart + exifSegment.Length, jpegBytes.Length - existingExifEnd);
            }
            else
            {
                result = new byte[jpegBytes.Length + exifSegment.Length];
                Array.Copy(jpegBytes, 0, result, 0, insertionPoint);
                Array.Copy(exifSegment, 0, result, insertionPoint, exifSegment.Length);
                Array.Copy(jpegBytes, insertionPoint, result, insertionPoint + exifSegment.Length, jpegBytes.Length - insertionPoint);
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

            writer.Write((byte)0x49); writer.Write((byte)0x49);
            writer.Write((ushort)0x002A);
            writer.Write((uint)8);

            var ifd0Entries = CreateIfd0Entries(meta);
            var exifIfdEntries = CreateExifIfdEntries(meta);
            var gpsIfdEntries = CreateGpsIfdEntries(meta);

            if (Debug)
                Super.Log($"[JpegExifInjector] Created {ifd0Entries.Count} IFD0 entries, {exifIfdEntries.Count} EXIF entries, {gpsIfdEntries.Count} GPS entries");

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
                    Super.Log($"[JpegExifInjector] Starting EXIF IFD at position {stream.Position}, offset from TIFF: {exifIfdOffset}");
                WriteIfd(writer, exifIfdEntries, tiffStart);
                UpdateIfdPointer(stream, tiffStart, ifd0Offset, 0x8769, exifIfdOffset);
            }

            uint gpsIfdOffset = 0;
            if (gpsIfdEntries.Count > 0)
            {
                gpsIfdOffset = (uint)(stream.Position - tiffStart);
                if (Debug)
                    Super.Log($"[JpegExifInjector] Starting GPS IFD at position {stream.Position}, offset from TIFF: {gpsIfdOffset}");
                WriteIfd(writer, gpsIfdEntries, tiffStart);
                UpdateIfdPointer(stream, tiffStart, ifd0Offset, 0x8825, gpsIfdOffset);
            }

            var segmentEnd = stream.Position;
            var segmentLength = (ushort)(segmentEnd - lengthPosition);
            if (Debug)
                Super.Log($"[JpegExifInjector] Segment ends at position {segmentEnd}, calculated length: {segmentLength}");

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

            if (!string.IsNullOrEmpty(meta.ResolutionUnit))
                entries.Add(new IfdEntry(0x0128, 3, ushort.Parse(meta.ResolutionUnit)));

            if (!string.IsNullOrEmpty(meta.Software))
                entries.Add(new IfdEntry(0x0131, 2, meta.Software));

            if (meta.DateTimeOriginal.HasValue)
                entries.Add(new IfdEntry(0x0132, 2,
                    meta.DateTimeOriginal.Value.ToString("yyyy:MM:dd HH:mm:ss")));

            return entries;
        }

        /// <summary>
        /// Creates EXIF IFD entries (camera-specific metadata)
        /// </summary>
        private static List<IfdEntry> CreateExifIfdEntries(Metadata meta)
        {
            var entries = new List<IfdEntry>();

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

            if (!string.IsNullOrEmpty(meta.ColorSpace))
                entries.Add(new IfdEntry(0xA001, 3, ushort.Parse(meta.ColorSpace)));

            if (!string.IsNullOrEmpty(meta.Flash))
                entries.Add(new IfdEntry(0x9209, 3, ushort.Parse(meta.Flash)));

            if (!string.IsNullOrEmpty(meta.WhiteBalance))
                entries.Add(new IfdEntry(0xA403, 3, ushort.Parse(meta.WhiteBalance)));

            if (meta.DigitalZoomRatio.HasValue)
                entries.Add(new IfdEntry(0xA404, 5, CreateRational(meta.DigitalZoomRatio.Value)));

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
                Super.Log($"[JpegExifInjector] Data will start at position {dataStart}, offset from TIFF: {dataStart - tiffStart}");

            var dataOffsets = new Dictionary<IfdEntry, uint>();
            var currentDataOffset = (uint)(dataStart - tiffStart);

            foreach (var entry in largeDataEntries)
            {
                dataOffsets[entry] = currentDataOffset;
                if (Debug)
                    Super.Log($"[JpegExifInjector] Pre-calculating offset for tag 0x{entry.Tag:X4}: {currentDataOffset} (length {entry.DataLength})");
                currentDataOffset += (uint)entry.DataLength;
                if (currentDataOffset % 2 == 1) currentDataOffset++;
            }

            foreach (var entry in entries)
            {
                if (Debug)
                    Super.Log($"[JpegExifInjector] Processing tag 0x{entry.Tag:X4}, type {entry.Type}, count {entry.Count}, data length {entry.DataLength}");
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
                Super.Log($"[JpegExifInjector] Starting to write data at position {writer.BaseStream.Position}, expected {dataStart}");

            foreach (var entry in largeDataEntries)
            {
                var expectedPosition = tiffStart + dataOffsets[entry];
                var actualPosition = writer.BaseStream.Position;
                if (Debug)
                    Super.Log($"[JpegExifInjector] Writing data for tag 0x{entry.Tag:X4} at position {actualPosition}, expected {expectedPosition}");

                if (actualPosition != expectedPosition && Debug)
                {
                    Super.Log($"[JpegExifInjector] ERROR: Position mismatch for tag 0x{entry.Tag:X4}! Expected {expectedPosition}, actual {actualPosition}");
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
                        Super.Log($"[JpegExifInjector] Writing rational data for tag 0x{entry.Tag:X4}: {rational[0]}/{rational[1]}");
                    for (int i = 0; i < rational.Length; i++)
                        writer.Write(rational[i]);
                    break;
                case 10:
                    var signedRational = (int[])entry.Data;
                    if (Debug)
                        Super.Log($"[JpegExifInjector] Writing signed rational data for tag 0x{entry.Tag:X4}: {signedRational[0]}/{signedRational[1]}");
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
                Super.Log($"[JpegExifInjector] Updating IFD pointer for tag 0x{tag:X4}, entry count: {entryCount}, new offset: {newOffset}");

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
