using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace OpenAgent.Tools.Internal;

/// <summary>
/// Minimal truecolour PNG writer. The screenshot tool cannot depend on
/// System.Drawing (Windows-only, extra package) and must not depend on the UI
/// stack, so the capture is encoded here: one deflate stream, one IHDR, no
/// interlacing (spec section 150).
/// </summary>
internal static class PngEncoder
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>Encodes top-down 24-bit RGB pixels as a PNG file.</summary>
    internal static byte[] Encode(byte[] rgb, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(rgb);

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "图像尺寸无效");
        }

        var minimum = (long)width * height * 3;
        if (rgb.Length < minimum)
        {
            throw new ArgumentException($"像素数据不足：需要 {minimum} 字节，实际 {rgb.Length} 字节", nameof(rgb));
        }

        var stride = width * 3;
        var raw = new byte[(stride + 1) * height];
        for (var y = 0; y < height; y++)
        {
            var row = y * (stride + 1);
            raw[row] = 0; // filter type: none
            Buffer.BlockCopy(rgb, y * stride, raw, row + 1, stride);
        }

        using var output = new MemoryStream();
        ReadOnlySpan<byte> signature = stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        output.Write(signature);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8; // bit depth
        header[9] = 2; // colour type: truecolour RGB
        header[10] = 0; // compression: deflate
        header[11] = 0; // filter method: adaptive
        header[12] = 0; // no interlace
        WriteChunk(output, "IHDR", header);
        WriteChunk(output, "IDAT", Deflate(raw));
        WriteChunk(output, "IEND", Array.Empty<byte>());

        return output.ToArray();
    }

    /// <summary>Nearest-neighbour downscale so a 4K capture can be sent to a model cheaply.</summary>
    internal static (byte[] Pixels, int Width, int Height) Downscale(
        byte[] rgb,
        int width,
        int height,
        int maxWidth)
    {
        if (maxWidth <= 0 || maxWidth >= width)
        {
            return (rgb, width, height);
        }

        var newHeight = Math.Max(1, (int)Math.Round((double)height * maxWidth / width));
        var scaled = new byte[maxWidth * newHeight * 3];

        for (var y = 0; y < newHeight; y++)
        {
            var sourceY = Math.Min(height - 1, y * height / newHeight);
            for (var x = 0; x < maxWidth; x++)
            {
                var sourceX = Math.Min(width - 1, x * width / maxWidth);
                var source = (sourceY * width + sourceX) * 3;
                var target = (y * maxWidth + x) * 3;
                scaled[target] = rgb[source];
                scaled[target + 1] = rgb[source + 1];
                scaled[target + 2] = rgb[source + 2];
            }
        }

        return (scaled, maxWidth, newHeight);
    }

    private static byte[] Deflate(byte[] raw)
    {
        using var buffer = new MemoryStream();
        using (var zlib = new ZLibStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw, 0, raw.Length);
        }

        return buffer.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        stream.Write(length);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes, 0, typeBytes.Length);

        if (data.Length > 0)
        {
            stream.Write(data, 0, data.Length);
        }

        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typeBytes, data));
        stream.Write(crc);
    }

    private static uint Crc32(byte[] first, byte[] second)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var value in first)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        foreach (var value in second)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < 256; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
