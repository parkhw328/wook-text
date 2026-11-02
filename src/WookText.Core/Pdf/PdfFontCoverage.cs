using System.Buffers.Binary;

namespace WookText.Core.Pdf;

// Read Unicode cmap entries before embedding a bundled SFNT font. A missing glyph
// must not silently become .notdef even when PDFium accepts the string.
internal static class PdfFontCoverage
{
    public static bool Supports(byte[] data, int unicode)
    {
        try
        {
            int tables = U16(data, 4), cmap = -1;
            for (int i = 0; i < tables; i++)
            {
                int entry = 12 + i * 16;
                if (U32(data, entry) == 0x636d6170) { cmap = checked((int)U32(data, entry + 8)); break; }
            }
            if (cmap < 0) return false;
            int count = U16(data, cmap + 2);
            for (int i = 0; i < count; i++)
            {
                int entry = cmap + 4 + i * 8;
                int platform = U16(data, entry), encoding = U16(data, entry + 2);
                if (platform != 0 && !(platform == 3 && encoding is 1 or 10)) continue;
                int table = checked(cmap + (int)U32(data, entry + 4));
                int format = U16(data, table);
                if (format == 12)
                {
                    int groups = checked((int)U32(data, table + 12));
                    for (int j = 0; j < groups; j++)
                    {
                        int group = table + 16 + j * 12;
                        uint first = U32(data, group), last = U32(data, group + 4);
                        if (unicode >= first && unicode <= last) return U32(data, group + 8) + unicode - first != 0;
                    }
                }
                else if (format == 4 && unicode <= 0xffff)
                {
                    int segments = U16(data, table + 6) / 2;
                    int ends = table + 14, starts = ends + segments * 2 + 2, deltas = starts + segments * 2, ranges = deltas + segments * 2;
                    for (int j = 0; j < segments; j++)
                    {
                        int end = U16(data, ends + j * 2), start = U16(data, starts + j * 2);
                        if (unicode < start || unicode > end) continue;
                        int delta = U16(data, deltas + j * 2), offset = U16(data, ranges + j * 2);
                        if (offset == 0) return ((unicode + delta) & 0xffff) != 0;
                        int glyph = U16(data, ranges + j * 2 + offset + (unicode - start) * 2);
                        return glyph != 0 && ((glyph + delta) & 0xffff) != 0;
                    }
                }
            }
            return false;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or OverflowException) { return false; }
    }

    private static ushort U16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));
    private static uint U32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
}
