using System.Text;

namespace WookText.Core;

public enum TextFileEncoding
{
    Utf8,
    Utf8Bom,
    Utf16LittleEndian,
    Utf16BigEndian,
    Utf32LittleEndian,
    Utf32BigEndian,
    Korean949
}

public static class TextEncodings
{
    static TextEncodings() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static Encoding GetEncoding(this TextFileEncoding kind) => kind switch
    {
        TextFileEncoding.Utf8 => new UTF8Encoding(false, true),
        TextFileEncoding.Utf8Bom => new UTF8Encoding(true, true),
        TextFileEncoding.Utf16LittleEndian => new UnicodeEncoding(false, true, true),
        TextFileEncoding.Utf16BigEndian => new UnicodeEncoding(true, true, true),
        TextFileEncoding.Utf32LittleEndian => new UTF32Encoding(false, true, true),
        TextFileEncoding.Utf32BigEndian => new UTF32Encoding(true, true, true),
        TextFileEncoding.Korean949 => Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static string DisplayName(this TextFileEncoding kind) => kind switch
    {
        TextFileEncoding.Utf8 => "UTF-8",
        TextFileEncoding.Utf8Bom => "UTF-8 BOM",
        TextFileEncoding.Utf16LittleEndian => "UTF-16 LE",
        TextFileEncoding.Utf16BigEndian => "UTF-16 BE",
        TextFileEncoding.Utf32LittleEndian => "UTF-32 LE",
        TextFileEncoding.Utf32BigEndian => "UTF-32 BE",
        TextFileEncoding.Korean949 => "CP949",
        _ => kind.ToString()
    };
}
