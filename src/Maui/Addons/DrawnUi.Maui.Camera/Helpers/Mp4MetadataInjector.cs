using System.Diagnostics;
using System.Text;

namespace DrawnUi.Camera;

/// <summary>
/// Injects metadata (GPS location, author, camera model, date, etc.) into MP4/MOV video files.
/// Writes both QuickTime udta text atoms (moov > udta) and Apple mdta metadata (moov > meta >
/// hdlr+keys+ilst). The Apple mdta format is required for iOS Photos to display camera info.
/// Uses stream-based I/O — only the moov box (typically a few KB) is read into memory.
///
/// Supported atoms: ©xyz (GPS), ©ART (artist/author), ©nam (title), ©too (software),
/// ©cmt (comment), ©day (date), ©des (description), ©mak (make), ©mod (model), and any
/// other ©-prefixed text atom.
/// </summary>
public static class Mp4MetadataInjector
{
    public static bool Debug { get; set; } = false;

    // Box type constants
    private static readonly uint Box_moov = FourCC("moov");
    private static readonly uint Box_udta = FourCC("udta");
    private static readonly uint Box_meta = FourCC("meta");
    private static readonly uint Box_free = FourCC("free");
    private static readonly uint Box_skip = FourCC("skip");
    private static readonly uint Box_stco = FourCC("stco");
    private static readonly uint Box_co64 = FourCC("co64");

    private static readonly HashSet<uint> ContainerTypes = new()
    {
        FourCC("trak"), FourCC("mdia"), FourCC("minf"), FourCC("stbl"), FourCC("udta")
    };

    // Well-known atom FourCC strings (QuickTime udta text atoms)
    public const string Atom_Location = "\u00A9xyz";
    public const string Atom_Artist = "\u00A9ART";
    public const string Atom_Title = "\u00A9nam";
    public const string Atom_Software = "\u00A9too";
    public const string Atom_Comment = "\u00A9cmt";
    public const string Atom_Date = "\u00A9day";
    public const string Atom_Description = "\u00A9des";
    public const string Atom_Make = "\u00A9mak";
    public const string Atom_Model = "\u00A9mod";

    // Apple QuickTime mdta keys (used by iOS Photos for camera info display)
    private static readonly Dictionary<string, string> AppleKeyMap = new()
    {
        [Atom_Make] = "com.apple.quicktime.make",
        [Atom_Model] = "com.apple.quicktime.model",
        [Atom_Software] = "com.apple.quicktime.software",
        [Atom_Date] = "com.apple.quicktime.creationdate",
        [Atom_Location] = "com.apple.quicktime.location.ISO6709",
        [Atom_Artist] = "com.apple.quicktime.author",
        [Atom_Comment] = "com.apple.quicktime.comment",
        [Atom_Description] = "com.apple.quicktime.description",
        [Atom_Title] = "com.apple.quicktime.title",
    };

    #region Public API — Metadata object

    /// <summary>
    /// Injects metadata from a Metadata object into an MP4/MOV file.
    /// Maps Metadata properties to MP4 udta text atoms. Modifies the file in-place.
    /// Only reads the moov box into memory (typically a few KB).
    /// </summary>
    public static bool InjectMetadata(string filePath, Metadata meta)
    {
        if (meta == null) return false;
        var atoms = MetadataToAtoms(meta);
        if (atoms.Count == 0) return false;
        return InjectAtoms(filePath, atoms);
    }

    /// <summary>
    /// Injects metadata from a Metadata object into an MP4/MOV file.
    /// Maps Metadata properties to MP4 udta text atoms. Modifies the file in-place.
    /// Only reads the moov box into memory (typically a few KB).
    /// </summary>
    public static async Task<bool> InjectMetadataAsync(string filePath, Metadata meta)
    {
        if (meta == null) return false;
        var atoms = MetadataToAtoms(meta);
        if (atoms.Count == 0) return false;
        return await InjectAtomsAsync(filePath, atoms);
    }

    #endregion

    #region Public API — Raw atoms

    /// <summary>
    /// Injects arbitrary text atoms into an MP4/MOV file's moov > udta box.
    /// Keys are FourCC strings (e.g., "©ART"), values are UTF-8 text.
    /// Existing atoms of the same type are replaced; new ones are appended.
    /// </summary>
    public static bool InjectAtoms(string filePath, IReadOnlyDictionary<string, string> atoms)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath) || atoms == null || atoms.Count == 0)
            return false;

        try
        {
            var atomList = BuildAtomList(atoms);
            if (atomList.Count == 0) return false;

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return InjectCore(fs, atomList, atoms);
        }
        catch (Exception ex)
        {
            if (Debug) Super.Log($"[Mp4MetadataInjector] Error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Injects arbitrary text atoms into an MP4/MOV file's moov > udta box.
    /// Also injects Apple mdta metadata for iOS Photos camera info display.
    /// Keys are FourCC strings (e.g., "©ART"), values are UTF-8 text.
    /// Existing atoms of the same type are replaced; new ones are appended.
    /// </summary>
    public static async Task<bool> InjectAtomsAsync(string filePath, IReadOnlyDictionary<string, string> atoms)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath) || atoms == null || atoms.Count == 0)
            return false;

        try
        {
            var atomList = BuildAtomList(atoms);
            if (atomList.Count == 0) return false;

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite,
                FileShare.None, 4096, FileOptions.Asynchronous);
            return await InjectCoreAsync(fs, atomList, atoms);
        }
        catch (Exception ex)
        {
            if (Debug) Super.Log($"[Mp4MetadataInjector] Error: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Public API — Location convenience

    /// <summary>
    /// Injects GPS location into an MP4/MOV file (convenience wrapper).
    /// </summary>
    public static bool InjectLocation(string filePath, double latitude, double longitude)
    {
        if (latitude == 0 && longitude == 0) return false;
        var atoms = new Dictionary<string, string>
        {
            [Atom_Location] = FormatIso6709(latitude, longitude)
        };
        return InjectAtoms(filePath, atoms);
    }

    /// <summary>
    /// Injects GPS location into an MP4/MOV file (convenience wrapper).
    /// </summary>
    public static async Task<bool> InjectLocationAsync(string filePath, double latitude, double longitude)
    {
        if (latitude == 0 && longitude == 0) return false;
        var atoms = new Dictionary<string, string>
        {
            [Atom_Location] = FormatIso6709(latitude, longitude)
        };
        return await InjectAtomsAsync(filePath, atoms);
    }

    #endregion

    #region Public API — Reading

    /// <summary>
    /// Reads GPS location from an MP4/MOV file if present.
    /// </summary>
    public static bool ReadLocation(string filePath, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;

        var atoms = ReadAtoms(filePath);
        if (atoms == null || !atoms.TryGetValue(Atom_Location, out var locationString))
            return false;

        return ParseIso6709(locationString, out latitude, out longitude);
    }

    /// <summary>
    /// Reads all recognized text atoms from the moov > udta box.
    /// Keys are FourCC strings (e.g., "©ART"), values are UTF-8 text.
    /// Returns null if the file has no moov or udta box.
    /// </summary>
    public static Dictionary<string, string> ReadAtoms(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var moovResult = ScanTopLevelBox(fs, Box_moov);
            if (moovResult == null) return null;

            var (moovOffset, moovSize) = moovResult.Value;
            var moov = new byte[moovSize];
            fs.Position = moovOffset;
            ReadExact(fs, moov, 0, moovSize);

            return ReadAtomsFromMoov(moov);
        }
        catch (Exception ex)
        {
            if (Debug) Super.Log($"[Mp4MetadataInjector] Error reading atoms: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Metadata ↔ Atoms Mapping

    /// <summary>
    /// Converts a Metadata object into a dictionary of MP4 udta text atoms.
    /// </summary>
    public static Dictionary<string, string> MetadataToAtoms(Metadata meta)
    {
        var atoms = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(meta.Software))
            atoms[Atom_Software] = meta.Software;

        if (!string.IsNullOrEmpty(meta.Vendor))
            atoms[Atom_Make] = meta.Vendor;

        if (!string.IsNullOrEmpty(meta.Model))
            atoms[Atom_Model] = meta.Model;

        if (meta.DateTimeOriginal.HasValue)
            atoms[Atom_Date] = meta.DateTimeOriginal.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");

        if (!string.IsNullOrEmpty(meta.UserComment))
            atoms[Atom_Comment] = meta.UserComment;

        if (!string.IsNullOrEmpty(meta.CameraOwnerName))
            atoms[Atom_Artist] = meta.CameraOwnerName;

        // GPS: use signed values, convert ref to sign if needed
        if (meta.GpsLatitude.HasValue && meta.GpsLongitude.HasValue
            && (meta.GpsLatitude.Value != 0 || meta.GpsLongitude.Value != 0))
        {
            double lat = Math.Abs(meta.GpsLatitude.Value);
            if (meta.GpsLatitudeRef == "S") lat = -lat;
            else if (meta.GpsLatitudeRef != "N") lat = meta.GpsLatitude.Value; // already signed

            double lon = Math.Abs(meta.GpsLongitude.Value);
            if (meta.GpsLongitudeRef == "W") lon = -lon;
            else if (meta.GpsLongitudeRef != "E") lon = meta.GpsLongitude.Value;

            atoms[Atom_Location] = FormatIso6709(lat, lon);
        }

        return atoms;
    }

    /// <summary>
    /// Populates a Metadata object from MP4 udta text atoms read from a file.
    /// </summary>
    public static void AtomsToMetadata(IReadOnlyDictionary<string, string> atoms, Metadata meta)
    {
        if (atoms.TryGetValue(Atom_Software, out var software))
            meta.Software = software;

        if (atoms.TryGetValue(Atom_Make, out var make))
            meta.Vendor = make;

        if (atoms.TryGetValue(Atom_Model, out var model))
            meta.Model = model;

        if (atoms.TryGetValue(Atom_Date, out var dateStr) && DateTime.TryParse(dateStr, out var date))
            meta.DateTimeOriginal = date;

        if (atoms.TryGetValue(Atom_Comment, out var comment))
            meta.UserComment = comment;

        if (atoms.TryGetValue(Atom_Artist, out var artist))
            meta.CameraOwnerName = artist;

        if (atoms.TryGetValue(Atom_Location, out var loc) &&
            ParseIso6709(loc, out var lat, out var lon))
        {
            Metadata.ApplyGpsCoordinates(meta, lat, lon);
        }
    }

    #endregion

    #region Core Injection

    private static bool InjectCore(FileStream fs, List<(uint Type, byte[] Data)> atoms,
        IReadOnlyDictionary<string, string> rawAtoms = null)
    {
        var moovResult = ScanTopLevelBox(fs, Box_moov);
        if (moovResult == null)
        {
            if (Debug) Super.Log("[Mp4MetadataInjector] No moov atom found");
            return false;
        }

        var (moovOffset, moovSize) = moovResult.Value;
        long moovEnd = moovOffset + moovSize;

        var moov = new byte[moovSize];
        fs.Position = moovOffset;
        ReadExact(fs, moov, 0, moovSize);

        // 1. Inject QuickTime udta text atoms (©mak, ©mod, ©too, ©xyz, etc.)
        var newMoov = InjectAtomsIntoMoov(moov, atoms);
        if (newMoov == null) return false;

        // 2. Inject Apple mdta metadata (iOS Photos reads this for camera info display)
        if (rawAtoms != null)
        {
            var appleMetaBox = BuildAppleMetaBox(rawAtoms);
            if (appleMetaBox != null)
                newMoov = InjectAppleMetaBoxIntoMoov(newMoov, appleMetaBox);
        }

        int sizeDelta = newMoov.Length - moov.Length;
        WriteBack(fs, moovOffset, moovEnd, newMoov, sizeDelta);

        if (Debug) Super.Log($"[Mp4MetadataInjector] {atoms.Count} udta atom(s) + Apple mdta injected");
        return true;
    }

    private static async Task<bool> InjectCoreAsync(FileStream fs, List<(uint Type, byte[] Data)> atoms,
        IReadOnlyDictionary<string, string> rawAtoms = null)
    {
        var moovResult = ScanTopLevelBox(fs, Box_moov);
        if (moovResult == null)
        {
            if (Debug) Super.Log("[Mp4MetadataInjector] No moov atom found");
            return false;
        }

        var (moovOffset, moovSize) = moovResult.Value;
        long moovEnd = moovOffset + moovSize;

        var moov = new byte[moovSize];
        fs.Position = moovOffset;
        await ReadExactAsync(fs, moov, 0, moovSize);

        // 1. Inject QuickTime udta text atoms (©mak, ©mod, ©too, ©xyz, etc.)
        var newMoov = InjectAtomsIntoMoov(moov, atoms);
        if (newMoov == null) return false;

        // 2. Inject Apple mdta metadata (iOS Photos reads this for camera info display)
        if (rawAtoms != null)
        {
            var appleMetaBox = BuildAppleMetaBox(rawAtoms);
            if (appleMetaBox != null)
                newMoov = InjectAppleMetaBoxIntoMoov(newMoov, appleMetaBox);
        }

        int sizeDelta = newMoov.Length - moov.Length;
        await WriteBackAsync(fs, moovOffset, moovEnd, newMoov, sizeDelta);

        if (Debug) Super.Log($"[Mp4MetadataInjector] {atoms.Count} udta atom(s) + Apple mdta injected");
        return true;
    }

    /// <summary>
    /// Injects multiple atoms into the moov byte array. Processes sequentially —
    /// each atom insertion updates the byte array before the next.
    /// </summary>
    private static byte[] InjectAtomsIntoMoov(byte[] moov, List<(uint Type, byte[] Data)> atoms)
    {
        byte[] current = moov;
        foreach (var (type, data) in atoms)
        {
            current = InjectSingleAtom(current, type, data);
        }
        return current;
    }

    /// <summary>
    /// Injects a single atom into moov bytes. Finds or creates udta, then replaces
    /// existing atom of the same type or appends a new one.
    /// </summary>
    private static byte[] InjectSingleAtom(byte[] moov, uint atomType, byte[] atomBytes)
    {
        int moovSize = moov.Length;
        var udtaInfo = FindBox(moov, 8, moovSize, Box_udta);

        if (udtaInfo != null)
        {
            int udtaOff = udtaInfo.Value.Offset;
            int udtaSz = udtaInfo.Value.Size;
            int udtaEnd = udtaOff + udtaSz;

            var existing = FindBox(moov, udtaInfo.Value.DataOffset, udtaEnd, atomType);
            if (existing != null)
            {
                // Replace existing
                int delta = atomBytes.Length - existing.Value.Size;
                var result = ReplaceRegion(moov, existing.Value.Offset, existing.Value.Size, atomBytes);
                UpdateBoxSize(result, udtaOff, udtaSz + delta);
                UpdateBoxSize(result, 0, moovSize + delta);
                return result;
            }
            else
            {
                // Append at end of udta
                var result = InsertAt(moov, udtaEnd, atomBytes);
                UpdateBoxSize(result, udtaOff, udtaSz + atomBytes.Length);
                UpdateBoxSize(result, 0, moovSize + atomBytes.Length);
                return result;
            }
        }
        else
        {
            // Create udta containing this atom
            var udtaAtom = new byte[8 + atomBytes.Length];
            WriteUInt32BE(udtaAtom, 0, (uint)udtaAtom.Length);
            WriteFourCC(udtaAtom, 4, "udta");
            Array.Copy(atomBytes, 0, udtaAtom, 8, atomBytes.Length);

            var result = InsertAt(moov, moovSize, udtaAtom);
            UpdateBoxSize(result, 0, moovSize + udtaAtom.Length);
            return result;
        }
    }

    #endregion

    #region Reading from moov

    private static Dictionary<string, string> ReadAtomsFromMoov(byte[] moov)
    {
        var result = new Dictionary<string, string>();

        var udtaInfo = FindBox(moov, 8, moov.Length, Box_udta);
        if (udtaInfo == null) return result;

        int pos = udtaInfo.Value.DataOffset;
        int end = udtaInfo.Value.Offset + udtaInfo.Value.Size;

        while (pos + 8 <= end)
        {
            int boxSize = (int)ReadUInt32BE(moov, pos);
            uint boxType = ReadUInt32BE(moov, pos + 4);

            if (boxSize < 8)
            {
                if (boxSize == 0) boxSize = end - pos;
                else break;
            }

            // Check if this is a text atom (first byte of type is 0xA9 = ©)
            if ((boxType >> 24) == 0xA9 && boxSize > 12)
            {
                // Text atom: [8 header][2 string len][2 lang][string data]
                int stringLen = (moov[pos + 8] << 8) | moov[pos + 9];
                int dataStart = pos + 12;
                int available = boxSize - 12;
                int readLen = Math.Min(stringLen, available);

                if (readLen > 0 && dataStart + readLen <= moov.Length)
                {
                    var value = Encoding.UTF8.GetString(moov, dataStart, readLen);
                    var fourCC = FourCCToString(boxType);
                    result[fourCC] = value;
                }
            }

            pos += boxSize;
        }

        return result;
    }

    #endregion

    #region File Write-Back (shared with Mp4LocationInjector)

    private static void WriteBack(FileStream fs, long moovOffset, long originalMoovEnd,
        byte[] newMoov, int sizeDelta)
    {
        if (sizeDelta == 0)
        {
            fs.Position = moovOffset;
            fs.Write(newMoov, 0, newMoov.Length);
            return;
        }

        if (originalMoovEnd >= fs.Length)
        {
            fs.Position = moovOffset;
            fs.Write(newMoov, 0, newMoov.Length);
            fs.SetLength(moovOffset + newMoov.Length);
            return;
        }

        if (sizeDelta > 0 && TryAbsorbFreeBox(fs, originalMoovEnd, sizeDelta, out int oldFreeSize))
        {
            int newFreeSize = oldFreeSize - sizeDelta;
            fs.Position = moovOffset;
            fs.Write(newMoov, 0, newMoov.Length);
            if (newFreeSize >= 8)
            {
                var hdr = new byte[8];
                WriteUInt32BE(hdr, 0, (uint)newFreeSize);
                WriteFourCC(hdr, 4, "free");
                fs.Write(hdr, 0, 8);
            }
            return;
        }

        AdjustChunkOffsets(newMoov, 8, newMoov.Length, sizeDelta, originalMoovEnd);
        ShiftTail(fs, moovOffset, originalMoovEnd, newMoov, sizeDelta);
    }

    private static async Task WriteBackAsync(FileStream fs, long moovOffset, long originalMoovEnd,
        byte[] newMoov, int sizeDelta)
    {
        if (sizeDelta == 0)
        {
            fs.Position = moovOffset;
            await fs.WriteAsync(newMoov, 0, newMoov.Length);
            return;
        }

        if (originalMoovEnd >= fs.Length)
        {
            fs.Position = moovOffset;
            await fs.WriteAsync(newMoov, 0, newMoov.Length);
            fs.SetLength(moovOffset + newMoov.Length);
            return;
        }

        if (sizeDelta > 0 && TryAbsorbFreeBox(fs, originalMoovEnd, sizeDelta, out int oldFreeSize))
        {
            int newFreeSize = oldFreeSize - sizeDelta;
            fs.Position = moovOffset;
            await fs.WriteAsync(newMoov, 0, newMoov.Length);
            if (newFreeSize >= 8)
            {
                var hdr = new byte[8];
                WriteUInt32BE(hdr, 0, (uint)newFreeSize);
                WriteFourCC(hdr, 4, "free");
                await fs.WriteAsync(hdr, 0, 8);
            }
            return;
        }

        AdjustChunkOffsets(newMoov, 8, newMoov.Length, sizeDelta, originalMoovEnd);
        await ShiftTailAsync(fs, moovOffset, originalMoovEnd, newMoov, sizeDelta);
    }

    private static bool TryAbsorbFreeBox(FileStream fs, long position, int neededBytes, out int freeBoxSize)
    {
        freeBoxSize = 0;
        if (position + 8 > fs.Length) return false;

        fs.Position = position;
        var hdr = new byte[8];
        if (fs.Read(hdr, 0, 8) < 8) return false;

        uint size = ReadUInt32BE(hdr, 0);
        uint type = ReadUInt32BE(hdr, 4);

        if ((type == Box_free || type == Box_skip) && size >= neededBytes + 8)
        {
            freeBoxSize = (int)size;
            return true;
        }
        return false;
    }

    private static void ShiftTail(FileStream fs, long moovOffset, long originalMoovEnd,
        byte[] newMoov, int sizeDelta)
    {
        long originalLength = fs.Length;
        long newLength = originalLength + sizeDelta;

        if (sizeDelta > 0)
        {
            fs.SetLength(newLength);
            const int bufSize = 65536;
            var buf = new byte[bufSize];
            long srcEnd = originalLength;
            long dstEnd = newLength;
            while (srcEnd > originalMoovEnd)
            {
                int chunk = (int)Math.Min(bufSize, srcEnd - originalMoovEnd);
                srcEnd -= chunk;
                dstEnd -= chunk;
                fs.Position = srcEnd;
                int read = fs.Read(buf, 0, chunk);
                fs.Position = dstEnd;
                fs.Write(buf, 0, read);
            }
        }
        else
        {
            const int bufSize = 65536;
            var buf = new byte[bufSize];
            long srcPos = originalMoovEnd;
            long dstPos = moovOffset + newMoov.Length;
            long remaining = originalLength - originalMoovEnd;
            while (remaining > 0)
            {
                int chunk = (int)Math.Min(bufSize, remaining);
                fs.Position = srcPos;
                int read = fs.Read(buf, 0, chunk);
                fs.Position = dstPos;
                fs.Write(buf, 0, read);
                srcPos += read;
                dstPos += read;
                remaining -= read;
            }
            fs.SetLength(newLength);
        }

        fs.Position = moovOffset;
        fs.Write(newMoov, 0, newMoov.Length);
    }

    private static async Task ShiftTailAsync(FileStream fs, long moovOffset, long originalMoovEnd,
        byte[] newMoov, int sizeDelta)
    {
        long originalLength = fs.Length;
        long newLength = originalLength + sizeDelta;

        if (sizeDelta > 0)
        {
            fs.SetLength(newLength);
            const int bufSize = 65536;
            var buf = new byte[bufSize];
            long srcEnd = originalLength;
            long dstEnd = newLength;
            while (srcEnd > originalMoovEnd)
            {
                int chunk = (int)Math.Min(bufSize, srcEnd - originalMoovEnd);
                srcEnd -= chunk;
                dstEnd -= chunk;
                fs.Position = srcEnd;
                int read = await fs.ReadAsync(buf, 0, chunk);
                fs.Position = dstEnd;
                await fs.WriteAsync(buf, 0, read);
            }
        }
        else
        {
            const int bufSize = 65536;
            var buf = new byte[bufSize];
            long srcPos = originalMoovEnd;
            long dstPos = moovOffset + newMoov.Length;
            long remaining = originalLength - originalMoovEnd;
            while (remaining > 0)
            {
                int chunk = (int)Math.Min(bufSize, remaining);
                fs.Position = srcPos;
                int read = await fs.ReadAsync(buf, 0, chunk);
                fs.Position = dstPos;
                await fs.WriteAsync(buf, 0, read);
                srcPos += read;
                dstPos += read;
                remaining -= read;
            }
            fs.SetLength(newLength);
        }

        fs.Position = moovOffset;
        await fs.WriteAsync(newMoov, 0, newMoov.Length);
    }

    #endregion

    #region Chunk Offset Adjustment

    private static void AdjustChunkOffsets(byte[] data, int start, int end, int delta, long originalMoovEnd)
    {
        int pos = start;
        while (pos + 8 <= end)
        {
            int boxSize = (int)ReadUInt32BE(data, pos);
            uint boxType = ReadUInt32BE(data, pos + 4);

            if (boxSize < 8) { if (boxSize == 0) boxSize = end - pos; else break; }
            int boxEnd = pos + boxSize;
            if (boxEnd > end) break;

            if (boxType == Box_stco)
            {
                int fullHeaderEnd = pos + 8 + 4;
                if (fullHeaderEnd + 4 <= boxEnd)
                {
                    int count = (int)ReadUInt32BE(data, fullHeaderEnd);
                    int offsetsStart = fullHeaderEnd + 4;
                    for (int i = 0; i < count && offsetsStart + (i + 1) * 4 <= boxEnd; i++)
                    {
                        int entryPos = offsetsStart + i * 4;
                        uint offset = ReadUInt32BE(data, entryPos);
                        if (offset >= (uint)originalMoovEnd)
                            WriteUInt32BE(data, entryPos, (uint)(offset + delta));
                    }
                }
            }
            else if (boxType == Box_co64)
            {
                int fullHeaderEnd = pos + 8 + 4;
                if (fullHeaderEnd + 4 <= boxEnd)
                {
                    int count = (int)ReadUInt32BE(data, fullHeaderEnd);
                    int offsetsStart = fullHeaderEnd + 4;
                    for (int i = 0; i < count && offsetsStart + (i + 1) * 8 <= boxEnd; i++)
                    {
                        int entryPos = offsetsStart + i * 8;
                        ulong offset = ReadUInt64BE(data, entryPos);
                        if (offset >= (ulong)originalMoovEnd)
                            WriteUInt64BE(data, entryPos, (ulong)((long)offset + delta));
                    }
                }
            }
            else if (ContainerTypes.Contains(boxType))
            {
                AdjustChunkOffsets(data, pos + 8, boxEnd, delta, originalMoovEnd);
            }

            pos = boxEnd;
        }
    }

    #endregion

    #region Top-Level Box Scanning

    private static (long Offset, int Size)? ScanTopLevelBox(FileStream fs, uint targetType)
    {
        fs.Position = 0;
        var header = new byte[16];

        while (fs.Position + 8 <= fs.Length)
        {
            long boxOffset = fs.Position;
            if (fs.Read(header, 0, 8) < 8) break;

            uint size32 = ReadUInt32BE(header, 0);
            uint type = ReadUInt32BE(header, 4);
            long boxSize;

            if (size32 == 1)
            {
                if (fs.Read(header, 8, 8) < 8) break;
                boxSize = (long)ReadUInt64BE(header, 8);
            }
            else if (size32 == 0) { boxSize = fs.Length - boxOffset; }
            else { boxSize = size32; }

            if (boxSize < 8) break;
            if (type == targetType)
            {
                if (boxSize > int.MaxValue) return null;
                return (boxOffset, (int)boxSize);
            }
            fs.Position = boxOffset + boxSize;
        }
        return null;
    }

    #endregion

    #region In-Memory Box Navigation

    private struct BoxInfo
    {
        public int Offset;
        public int Size;
        public int DataOffset;
    }

    private static BoxInfo? FindBox(byte[] data, int start, int end, uint boxType)
    {
        int pos = start;
        while (pos + 8 <= end)
        {
            var size = (int)ReadUInt32BE(data, pos);
            var type = ReadUInt32BE(data, pos + 4);
            if (size < 8) { if (size == 0) size = end - pos; else break; }
            if (type == boxType)
                return new BoxInfo { Offset = pos, Size = size, DataOffset = pos + 8 };
            pos += size;
        }
        return null;
    }

    private static void UpdateBoxSize(byte[] data, int boxOffset, int newSize)
    {
        WriteUInt32BE(data, boxOffset, (uint)newSize);
    }

    #endregion

    #region Apple mdta Metadata (iOS Photos camera info)

    /// <summary>
    /// Builds an Apple-format meta box (moov > meta) with hdlr(mdta) + keys + ilst structure.
    /// iOS Photos reads this format for camera make, model, software, creation date display.
    /// Returns null if no atoms have Apple key mappings.
    /// </summary>
    private static byte[] BuildAppleMetaBox(IReadOnlyDictionary<string, string> atoms)
    {
        // Filter to atoms that have Apple key mappings
        var appleEntries = new List<(string Key, string Value)>();
        foreach (var (fourCC, value) in atoms)
        {
            if (AppleKeyMap.TryGetValue(fourCC, out var appleKey))
                appleEntries.Add((appleKey, value));
        }

        if (appleEntries.Count == 0) return null;

        // Auto-synthesize camera-specific Apple keys for iOS Photos "Camera" and "Lens" display.
        // iOS Photos requires these specific keys — make/model alone are not enough.
        atoms.TryGetValue(Atom_Make, out var makeVal);
        atoms.TryGetValue(Atom_Model, out var modelVal);
        if (!string.IsNullOrEmpty(modelVal))
        {
            appleEntries.Add(("com.apple.quicktime.camera.identifier", modelVal));
            appleEntries.Add(("com.apple.quicktime.camera.lens.model",
                $"{makeVal ?? ""} {modelVal}".Trim()));
        }

        // Build hdlr box: handler_type = 'mdta'
        // [4:size=33][4:'hdlr'][4:ver+flags=0][4:pre_defined=0][4:'mdta'][12:reserved=0][1:null]
        var hdlr = new byte[33];
        WriteUInt32BE(hdlr, 0, 33);
        WriteFourCC(hdlr, 4, "hdlr");
        // bytes 8-11: version(0) + flags(0) already zero
        // bytes 12-15: pre_defined = 0 already zero
        WriteFourCC(hdlr, 16, "mdta");
        // bytes 20-31: reserved[3] = 0 already zero
        // byte 32: null terminator (empty handler name)

        // Build keys box: [4:size][4:'keys'][4:ver+flags=0][4:entry_count][entries...]
        var keyEntryBytes = new List<byte[]>();
        int keysPayloadSize = 0;
        foreach (var (key, _) in appleEntries)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            int entrySize = 4 + 4 + keyBytes.Length; // key_size + 'mdta' namespace + key_string
            var entry = new byte[entrySize];
            WriteUInt32BE(entry, 0, (uint)entrySize);
            WriteFourCC(entry, 4, "mdta");
            Array.Copy(keyBytes, 0, entry, 8, keyBytes.Length);
            keyEntryBytes.Add(entry);
            keysPayloadSize += entrySize;
        }

        int keysBoxSize = 16 + keysPayloadSize;
        var keys = new byte[keysBoxSize];
        WriteUInt32BE(keys, 0, (uint)keysBoxSize);
        WriteFourCC(keys, 4, "keys");
        // bytes 8-11: version(0) + flags(0) already zero
        WriteUInt32BE(keys, 12, (uint)appleEntries.Count);
        int keysPos = 16;
        foreach (var entry in keyEntryBytes)
        {
            Array.Copy(entry, 0, keys, keysPos, entry.Length);
            keysPos += entry.Length;
        }

        // Build ilst box: [4:size][4:'ilst'][items...]
        // Each item: [4:item_size][4:1-based-key-index] containing data sub-box
        // Data sub-box: [4:size][4:'data'][4:type=1(UTF-8)][4:locale=0][N:value_bytes]
        var ilstItems = new List<byte[]>();
        int ilstPayloadSize = 0;
        for (int i = 0; i < appleEntries.Count; i++)
        {
            var (_, value) = appleEntries[i];
            var valueBytes = Encoding.UTF8.GetBytes(value);

            // data sub-box
            int dataBoxSize = 16 + valueBytes.Length;
            var dataBox = new byte[dataBoxSize];
            WriteUInt32BE(dataBox, 0, (uint)dataBoxSize);
            WriteFourCC(dataBox, 4, "data");
            WriteUInt32BE(dataBox, 8, 1); // type indicator: 1 = UTF-8
            WriteUInt32BE(dataBox, 12, 0); // locale: 0
            Array.Copy(valueBytes, 0, dataBox, 16, valueBytes.Length);

            // item wrapper box (type = 1-based key index)
            int itemSize = 8 + dataBoxSize;
            var item = new byte[itemSize];
            WriteUInt32BE(item, 0, (uint)itemSize);
            WriteUInt32BE(item, 4, (uint)(i + 1)); // 1-based key index
            Array.Copy(dataBox, 0, item, 8, dataBoxSize);

            ilstItems.Add(item);
            ilstPayloadSize += itemSize;
        }

        int ilstBoxSize = 8 + ilstPayloadSize;
        var ilst = new byte[ilstBoxSize];
        WriteUInt32BE(ilst, 0, (uint)ilstBoxSize);
        WriteFourCC(ilst, 4, "ilst");
        int ilstPos = 8;
        foreach (var item in ilstItems)
        {
            Array.Copy(item, 0, ilst, ilstPos, item.Length);
            ilstPos += item.Length;
        }

        // Build meta full box: [4:size][4:'meta'][4:ver+flags=0][hdlr][keys][ilst]
        int metaBoxSize = 12 + hdlr.Length + keys.Length + ilst.Length;
        var meta = new byte[metaBoxSize];
        WriteUInt32BE(meta, 0, (uint)metaBoxSize);
        WriteFourCC(meta, 4, "meta");
        // bytes 8-11: version(0) + flags(0) already zero (full box)
        int metaPos = 12;
        Array.Copy(hdlr, 0, meta, metaPos, hdlr.Length); metaPos += hdlr.Length;
        Array.Copy(keys, 0, meta, metaPos, keys.Length); metaPos += keys.Length;
        Array.Copy(ilst, 0, meta, metaPos, ilst.Length);

        if (Debug) Super.Log($"[Mp4MetadataInjector] Built Apple meta box: {appleEntries.Count} keys, {metaBoxSize} bytes");
        return meta;
    }

    /// <summary>
    /// Injects or replaces the Apple-format meta box in moov bytes.
    /// The meta box is a direct child of moov (not inside udta).
    /// </summary>
    private static byte[] InjectAppleMetaBoxIntoMoov(byte[] moov, byte[] metaBox)
    {
        int moovSize = moov.Length;
        var existing = FindBox(moov, 8, moovSize, Box_meta);

        if (existing != null)
        {
            // Replace existing meta box
            int delta = metaBox.Length - existing.Value.Size;
            var result = ReplaceRegion(moov, existing.Value.Offset, existing.Value.Size, metaBox);
            UpdateBoxSize(result, 0, moovSize + delta);
            return result;
        }
        else
        {
            // Append meta box at end of moov
            var result = InsertAt(moov, moovSize, metaBox);
            UpdateBoxSize(result, 0, moovSize + metaBox.Length);
            return result;
        }
    }

    #endregion

    #region Atom Building

    private static List<(uint Type, byte[] Data)> BuildAtomList(IReadOnlyDictionary<string, string> atoms)
    {
        var result = new List<(uint, byte[])>();
        foreach (var (fourCC, value) in atoms)
        {
            if (string.IsNullOrEmpty(value) || fourCC.Length != 4) continue;
            result.Add((FourCC(fourCC), BuildTextAtom(fourCC, value)));
        }
        return result;
    }

    private static byte[] BuildTextAtom(string fourCC, string value)
    {
        var valueBytes = Encoding.UTF8.GetBytes(value);
        int size = 8 + 2 + 2 + valueBytes.Length;
        var atom = new byte[size];
        WriteUInt32BE(atom, 0, (uint)size);
        WriteFourCC(atom, 4, fourCC);
        WriteUInt16BE(atom, 8, (ushort)valueBytes.Length);
        WriteUInt16BE(atom, 10, 0x15C7); // language: undetermined
        Array.Copy(valueBytes, 0, atom, 12, valueBytes.Length);
        return atom;
    }

    private static byte[] InsertAt(byte[] source, int offset, byte[] data)
    {
        var result = new byte[source.Length + data.Length];
        if (offset > 0) Array.Copy(source, 0, result, 0, offset);
        Array.Copy(data, 0, result, offset, data.Length);
        if (offset < source.Length)
            Array.Copy(source, offset, result, offset + data.Length, source.Length - offset);
        return result;
    }

    private static byte[] ReplaceRegion(byte[] source, int offset, int length, byte[] data)
    {
        var result = new byte[source.Length - length + data.Length];
        if (offset > 0) Array.Copy(source, 0, result, 0, offset);
        Array.Copy(data, 0, result, offset, data.Length);
        int remaining = source.Length - offset - length;
        if (remaining > 0)
            Array.Copy(source, offset + length, result, offset + data.Length, remaining);
        return result;
    }

    #endregion

    #region ISO 6709

    private static string FormatIso6709(double latitude, double longitude)
    {
        var latSign = latitude >= 0 ? "+" : "";
        var lonSign = longitude >= 0 ? "+" : "";
        return $"{latSign}{latitude:F4}{lonSign}{longitude:F4}/";
    }

    private static bool ParseIso6709(string iso6709, out double latitude, out double longitude)
    {
        latitude = 0; longitude = 0;
        if (string.IsNullOrEmpty(iso6709)) return false;

        var s = iso6709.TrimEnd('/');
        if (s.Length < 3) return false;

        int lonStart = -1;
        for (int i = 1; i < s.Length; i++)
        {
            if (s[i] == '+' || s[i] == '-') { lonStart = i; break; }
        }
        if (lonStart <= 0) return false;

        var latStr = s.Substring(0, lonStart);
        var lonStr = s.Substring(lonStart);

        for (int i = 1; i < lonStr.Length; i++)
        {
            if (lonStr[i] == '+' || lonStr[i] == '-') { lonStr = lonStr.Substring(0, i); break; }
        }

        return double.TryParse(latStr, System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out latitude)
               && double.TryParse(lonStr, System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out longitude);
    }

    #endregion

    #region Binary Helpers

    private static uint ReadUInt32BE(byte[] d, int o) =>
        (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);

    private static void WriteUInt32BE(byte[] d, int o, uint v)
    { d[o] = (byte)(v >> 24); d[o + 1] = (byte)(v >> 16); d[o + 2] = (byte)(v >> 8); d[o + 3] = (byte)v; }

    private static void WriteUInt16BE(byte[] d, int o, ushort v)
    { d[o] = (byte)(v >> 8); d[o + 1] = (byte)v; }

    private static ulong ReadUInt64BE(byte[] d, int o) =>
        ((ulong)d[o] << 56) | ((ulong)d[o + 1] << 48) | ((ulong)d[o + 2] << 40) | ((ulong)d[o + 3] << 32) |
        ((ulong)d[o + 4] << 24) | ((ulong)d[o + 5] << 16) | ((ulong)d[o + 6] << 8) | d[o + 7];

    private static void WriteUInt64BE(byte[] d, int o, ulong v)
    {
        d[o] = (byte)(v >> 56); d[o + 1] = (byte)(v >> 48); d[o + 2] = (byte)(v >> 40); d[o + 3] = (byte)(v >> 32);
        d[o + 4] = (byte)(v >> 24); d[o + 5] = (byte)(v >> 16); d[o + 6] = (byte)(v >> 8); d[o + 7] = (byte)v;
    }

    private static uint FourCC(string s)
    {
        var b = Encoding.Latin1.GetBytes(s);
        return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
    }

    private static string FourCCToString(uint v)
    {
        return Encoding.Latin1.GetString(new[]
        {
            (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v
        });
    }

    private static void WriteFourCC(byte[] d, int o, string s)
    {
        var b = Encoding.Latin1.GetBytes(s);
        d[o] = b[0]; d[o + 1] = b[1]; d[o + 2] = b[2]; d[o + 3] = b[3];
    }

    private static void ReadExact(Stream s, byte[] buf, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = s.Read(buf, offset + total, count - total);
            if (read == 0) throw new EndOfStreamException("Unexpected end of MP4 file");
            total += read;
        }
    }

    private static async Task ReadExactAsync(Stream s, byte[] buf, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = await s.ReadAsync(buf, offset + total, count - total);
            if (read == 0) throw new EndOfStreamException("Unexpected end of MP4 file");
            total += read;
        }
    }

    #endregion
}
