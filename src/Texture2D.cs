using System.Numerics;

namespace Vergeo2D.Mesh;

public sealed class Texture2D
{
    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path);
    public int Width { get; }
    public int Height { get; }

    public Texture2D(string path, int width, int height)
    {
        Path = path;
        Width = width;
        Height = height;
    }

    public static Texture2D LoadFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        var (width, height) = ReadDimensions(stream);
        return new Texture2D(path, width, height);
    }

    public Vector2 PixelToUV(Vector2 pixel)
    {
        return Width == 0 || Height == 0
            ? Vector2.Zero
            : new Vector2(pixel.X / Width, pixel.Y / Height);
    }

    public Vector2 UVToPixel(Vector2 uv)
    {
        return new Vector2(uv.X * Width, uv.Y * Height);
    }

    private static (int Width, int Height) ReadDimensions(Stream stream)
    {
        var header = new byte[8];
        ReadExact(stream, header, 8);

        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return ReadPngDimensions(stream);

        if (header[0] == 0xFF && header[1] == 0xD8)
            return ReadJpegDimensions(stream, header);

        if (header[0] == 0x42 && header[1] == 0x4D)
            return ReadBmpDimensions(stream);

        if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46)
            return ReadGifDimensions(stream, header);

        throw new NotSupportedException("Unsupported image format.");
    }

    private static (int Width, int Height) ReadPngDimensions(Stream stream)
    {
        var buffer = new byte[16];
        ReadExact(stream, buffer, 16);
        var width = (int)ReadUInt32BigEndian(buffer, 8);
        var height = (int)ReadUInt32BigEndian(buffer, 12);
        return (width, height);
    }

    private static (int Width, int Height) ReadBmpDimensions(Stream stream)
    {
        var buffer = new byte[18];
        ReadExact(stream, buffer, 18);
        var width = ReadInt32LittleEndian(buffer, 10);
        var height = ReadInt32LittleEndian(buffer, 14);
        return (width, Math.Abs(height));
    }

    private static (int Width, int Height) ReadGifDimensions(Stream stream, byte[] header)
    {
        var width = header[6] | header[7] << 8;
        var heightBuffer = new byte[2];
        ReadExact(stream, heightBuffer, 2);
        var height = heightBuffer[0] | heightBuffer[1] << 8;
        return (width, height);
    }

    private static (int Width, int Height) ReadJpegDimensions(Stream stream, byte[] header)
    {
        var reader = new PrefixedByteReader(stream, new[] { header[2], header[3], header[4], header[5], header[6], header[7] });

        while (true)
        {
            var marker = reader.ReadByte();
            if (marker == -1) throw new NotSupportedException("JPEG dimensions not found.");
            if (marker != 0xFF) continue;

            int code;
            do
            {
                code = reader.ReadByte();
            } while (code == 0xFF);

            if (code == -1 || code == 0xD9) throw new NotSupportedException("JPEG dimensions not found.");
            if (code == 0x01 || code >= 0xD0 && code <= 0xD7) continue;

            var lengthHigh = reader.ReadByte();
            var lengthLow = reader.ReadByte();
            var length = lengthHigh << 8 | lengthLow;

            var isStartOfFrame = code >= 0xC0 && code <= 0xCF && code != 0xC4 && code != 0xC8 && code != 0xCC;

            if (isStartOfFrame)
            {
                reader.ReadByte();
                var heightHigh = reader.ReadByte();
                var heightLow = reader.ReadByte();
                var widthHigh = reader.ReadByte();
                var widthLow = reader.ReadByte();
                return (widthHigh << 8 | widthLow, heightHigh << 8 | heightLow);
            }

            reader.Skip(length - 2);
        }
    }

    private static void ReadExact(Stream stream, byte[] buffer, int count)
    {
        var offset = 0;
        while (offset < count)
        {
            var read = stream.Read(buffer, offset, count - offset);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    private static uint ReadUInt32BigEndian(byte[] buffer, int offset)
    {
        return (uint)buffer[offset] << 24 | (uint)buffer[offset + 1] << 16 | (uint)buffer[offset + 2] << 8 | buffer[offset + 3];
    }

    private static int ReadInt32LittleEndian(byte[] buffer, int offset)
    {
        return buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16 | buffer[offset + 3] << 24;
    }

    private sealed class PrefixedByteReader
    {
        private readonly Stream _stream;
        private readonly byte[] _prefix;
        private int _prefixIndex;

        public PrefixedByteReader(Stream stream, byte[] prefix)
        {
            _stream = stream;
            _prefix = prefix;
        }

        public int ReadByte()
        {
            if (_prefixIndex < _prefix.Length) return _prefix[_prefixIndex++];
            return _stream.ReadByte();
        }

        public void Skip(int count)
        {
            for (var i = 0; i < count; i++) ReadByte();
        }
    }
}

