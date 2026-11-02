using System.Runtime.InteropServices;
using System.Text;
using static WookText.Core.Pdf.PdfNative;

namespace WookText.Core.Pdf;

public readonly record struct PdfBox(double Left, double Bottom, double Right, double Top)
{
    public double Width => Right - Left;
    public double Height => Top - Bottom;
}
public sealed record PdfInfo(int PageCount, bool CanEdit, bool CanCopy, string? ReadOnlyReason);
public sealed record PdfGlyph(int TextOffset, string Text, PdfBox Bounds, PdfBox ScreenBounds);
public sealed record PdfObject(int Index, int Type, PdfBox Bounds, PdfBox ScreenBounds, string Text, float FontSize);
public sealed record PdfNote(PdfBox ScreenBounds, string Text);
public sealed record PdfFrame(int Width, int Height, int Stride, byte[] Pixels, string Text, PdfGlyph[] Glyphs, PdfObject[] Objects, PdfNote[] Notes);
public sealed record PdfSearchHit(int Page, int Offset, int Length);

/// <summary>Serializes all PDFium access. Each edit uses a disposable copy and commits only after serialization succeeds.</summary>
public static class PdfEngine
{
    private static readonly object Gate = new();
    private static bool _initialized;
    private const int MaximumCharacters = 500_000;

    private sealed class Document : IDisposable
    {
        private GCHandle _pin;
        public nint Handle { get; }
        public Document(byte[] bytes)
        {
            if (!_initialized)
            {
                try { FPDF_InitLibrary(); _initialized = true; }
                catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
                { throw new IOException("PDF 엔진을 불러올 수 없습니다. 배포본을 전체 압축 해제하거나 다시 설치하세요.", ex); }
            }
            if (bytes.Length == 0 || bytes.Length > PdfFileService.MaximumBytes) throw new IOException("비어 있거나 32 MiB를 초과한 PDF입니다.");
            _pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Handle = FPDF_LoadMemDocument64(_pin.AddrOfPinnedObject(), (nuint)bytes.Length, 0);
            if (Handle == 0)
            {
                _pin.Free();
                throw new IOException(FPDF_GetLastError() == 4 ? "암호가 필요한 PDF는 지원하지 않습니다. 암호를 해제한 사본을 열어 주세요." : "PDF를 읽을 수 없습니다. 손상되었거나 지원하지 않는 형식입니다.");
            }
        }
        public void Dispose() { FPDF_CloseDocument(Handle); if (_pin.IsAllocated) _pin.Free(); }
    }

    private sealed class Page : IDisposable
    {
        public nint Handle { get; }
        public Page(nint document, int index)
        {
            if (index < 0 || index >= FPDF_GetPageCount(document)) throw new ArgumentOutOfRangeException(nameof(index));
            Handle = FPDF_LoadPage(document, index);
            if (Handle == 0) throw new IOException("PDF 페이지를 읽을 수 없습니다.");
        }
        public void Dispose() => FPDF_ClosePage(Handle);
    }

    private static PdfInfo Info(nint doc)
    {
        int count = FPDF_GetPageCount(doc);
        if (count <= 0 || count > 5000) throw new IOException("PDF는 1~5,000페이지를 지원합니다.");
        nuint permissions = FPDF_GetDocPermissions(doc);
        bool signed = FPDF_GetSignatureCount(doc) > 0;
        bool edit = (permissions & 8) != 0 && (permissions & 32) != 0 && (permissions & 1024) != 0;
        return new(count, edit && !signed, (permissions & 16) != 0,
            signed ? "전자서명 PDF는 서명 보존을 위해 읽기 전용입니다." : !edit ? "이 PDF는 수정 권한이 없습니다." : null);
    }

    public static PdfInfo Inspect(byte[] bytes)
    {
        lock (Gate) { using Document doc = new(bytes); return Info(doc.Handle); }
    }

    public static PdfFrame Render(byte[] bytes, int index, double zoom)
    {
        lock (Gate)
        {
            using Document doc = new(bytes);
            PdfInfo info = Info(doc.Handle);
            using Page page = new(doc.Handle, index);
            double pw = FPDF_GetPageWidthF(page.Handle), ph = FPDF_GetPageHeightF(page.Handle);
            if (!double.IsFinite(pw) || !double.IsFinite(ph) || pw <= 0 || ph <= 0) throw new IOException("페이지 크기가 올바르지 않습니다.");
            double scale = Math.Clamp(zoom, .25, 4) * 96 / 72;
            scale = Math.Min(scale, Math.Min(4096 / pw, 4096 / ph));
            int width = Math.Max(1, (int)Math.Ceiling(pw * scale)), height = Math.Max(1, (int)Math.Ceiling(ph * scale));
            nint bitmap = FPDFBitmap_Create(width, height, 1);
            if (bitmap == 0) throw new IOException("PDF 화면 메모리를 만들 수 없습니다.");
            try
            {
                FPDFBitmap_FillRect(bitmap, 0, 0, width, height, 0xffffffff);
                FPDF_RenderPageBitmap(bitmap, page.Handle, 0, 0, width, height, 0, 1);
                int stride = FPDFBitmap_GetStride(bitmap);
                byte[] pixels = new byte[checked(stride * height)];
                Marshal.Copy(FPDFBitmap_GetBuffer(bitmap), pixels, 0, pixels.Length);
                nint textPage = FPDFText_LoadPage(page.Handle);
                try
                {
                    (string text, PdfGlyph[] glyphs) = info.CanCopy ? ReadText(textPage, page.Handle, width, height) : ("", []);
                    List<PdfObject> objects = [];
                    int count = FPDFPage_CountObjects(page.Handle);
                    if (count > 100_000) throw new IOException("페이지 개체가 너무 많습니다.");
                    for (int i = 0; i < count; i++)
                    {
                        nint obj = FPDFPage_GetObject(page.Handle, i);
                        int type = FPDFPageObj_GetType(obj);
                        if (type is not (1 or 3) || FPDFPageObj_GetBounds(obj, out float l, out float b, out float r, out float t) == 0) continue;
                        PdfBox bounds = new(l, b, r, t);
                        FPDFTextObj_GetFontSize(obj, out float size);
                        objects.Add(new(i, type, bounds, ToScreen(page.Handle, bounds, width, height),
                            type == 1 && info.CanCopy ? ObjectText(obj, textPage) : "", size));
                    }
                    List<PdfNote> notes = [];
                    for (int i = 0; i < FPDFPage_GetAnnotCount(page.Handle); i++)
                    {
                        nint annotation = FPDFPage_GetAnnot(page.Handle, i);
                        try
                        {
                            if (FPDFAnnot_GetSubtype(annotation) != 1 || FPDFAnnot_GetRect(annotation, out Rect rect) == 0) continue;
                            nuint length = FPDFAnnot_GetStringValue(annotation, "Contents", [], 0);
                            if (length > 200_000) continue;
                            byte[] buffer = new byte[(int)length];
                            FPDFAnnot_GetStringValue(annotation, "Contents", buffer, length);
                            notes.Add(new(ToScreen(page.Handle, new(rect.Left, rect.Bottom, rect.Right, rect.Top), width, height), Encoding.Unicode.GetString(buffer).TrimEnd('\0')));
                        }
                        finally { FPDFPage_CloseAnnot(annotation); }
                    }
                    return new(width, height, stride, pixels, text, glyphs, objects.ToArray(), notes.ToArray());
                }
                finally { if (textPage != 0) FPDFText_ClosePage(textPage); }
            }
            finally { FPDFBitmap_Destroy(bitmap); }
        }
    }

    private static (string Text, PdfGlyph[] Glyphs) ReadText(nint textPage, nint page, int width, int height)
    {
        int count = FPDFText_CountChars(textPage);
        if (count > MaximumCharacters) throw new IOException("페이지 텍스트가 500,000자를 초과합니다.");
        StringBuilder text = new();
        List<PdfGlyph> glyphs = [];
        for (int i = 0; i < count; i++)
        {
            uint code = FPDFText_GetUnicode(textPage, i);
            if (code == 0xA0) code = 32; // PDF font cmap space aliases: normalize extracted whitespace.
            string value = code == 0 ? "\uFFFD" : Rune.IsValid((int)code) ? char.ConvertFromUtf32((int)code) : "\uFFFD";
            int offset = text.Length;
            text.Append(value);
            if (FPDFText_GetCharBox(textPage, i, out double l, out double r, out double b, out double t) != 0)
            {
                PdfBox bounds = new(l, b, r, t);
                glyphs.Add(new(offset, value, bounds, ToScreen(page, bounds, width, height)));
            }
        }
        return (text.ToString(), glyphs.ToArray());
    }

    private static string ObjectText(nint obj, nint textPage)
    {
        nuint length = FPDFTextObj_GetText(obj, textPage, [], 0);
        if (length > MaximumCharacters * 2) throw new IOException("텍스트 개체가 너무 큽니다.");
        byte[] buffer = new byte[(int)length];
        if (length == 0) return "";
        FPDFTextObj_GetText(obj, textPage, buffer, length);
        return Encoding.Unicode.GetString(buffer).TrimEnd('\0').Replace('\u00a0', ' ');
    }

    private static PdfBox ToScreen(nint page, PdfBox box, int width, int height)
    {
        var corners = new[] { (box.Left, box.Bottom), (box.Left, box.Top), (box.Right, box.Bottom), (box.Right, box.Top) };
        var points = corners.Select(p =>
        {
            Check(FPDF_PageToDevice(page, 0, 0, width, height, 0, p.Item1, p.Item2, out int x, out int y));
            return (X: x, Y: y);
        }).ToArray();
        // Screen boxes also use Bottom=min Y, Top=max Y; this avoids mixing native/page coordinates in UI.
        return new(points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
    }

    public static PdfBox FromScreen(byte[] bytes, int index, PdfBox screen, int width, int height)
    {
        lock (Gate)
        {
            using Document doc = new(bytes);
            using Page page = new(doc.Handle, index);
            Check(FPDF_DeviceToPage(page.Handle, 0, 0, width, height, 0, (int)screen.Left, (int)screen.Bottom, out double x1, out double y1));
            Check(FPDF_DeviceToPage(page.Handle, 0, 0, width, height, 0, (int)screen.Right, (int)screen.Top, out double x2, out double y2));
            return new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2));
        }
    }

    public static PdfSearchHit[] Search(byte[] bytes, string query, bool matchCase, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(query)) return [];
        lock (Gate)
        {
            using Document doc = new(bytes);
            PdfInfo info = Info(doc.Handle);
            if (!info.CanCopy) throw new IOException("이 PDF는 텍스트 추출 권한이 없습니다.");
            List<PdfSearchHit> hits = [];
            for (int index = 0; index < info.PageCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using Page page = new(doc.Handle, index);
                nint textPage = FPDFText_LoadPage(page.Handle);
                try
                {
                    string text = ReadText(textPage, page.Handle, 1, 1).Text;
                    int offset = 0;
                    while ((offset = text.IndexOf(query, offset, matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)) >= 0)
                    {
                        hits.Add(new(index, offset, query.Length));
                        if (hits.Count >= 10_000) throw new IOException("검색 결과가 10,000개 이상입니다. 더 구체적인 검색어를 입력하세요.");
                        offset += query.Length;
                    }
                }
                finally { if (textPage != 0) FPDFText_ClosePage(textPage); }
            }
            return hits.ToArray();
        }
    }

    public static byte[] Rotate(byte[] bytes, int index) => Edit(bytes, doc =>
    {
        using Page page = new(doc, index);
        FPDFPage_SetRotation(page.Handle, (FPDFPage_GetRotation(page.Handle) + 1) % 4);
    });

    public static byte[] DeletePage(byte[] bytes, int index) => Edit(bytes, doc =>
    {
        if (FPDF_GetPageCount(doc) <= 1) throw new IOException("마지막 페이지는 삭제할 수 없습니다.");
        using (Page page = new(doc, index)) { }
        FPDFPage_Delete(doc, index);
    });

    public static byte[] MovePage(byte[] bytes, int index, int destination) => Edit(bytes, doc =>
    {
        int count = FPDF_GetPageCount(doc);
        if (index < 0 || index >= count || destination < 0 || destination >= count) throw new ArgumentOutOfRangeException(nameof(destination));
        Check(FPDF_MovePages(doc, [index], 1, destination));
    });

    public static byte[] Merge(byte[] bytes, byte[] sourceBytes) => Edit(bytes, doc =>
    {
        using Document source = new(sourceBytes);
        PdfInfo sourceInfo = Info(source.Handle);
        if (!sourceInfo.CanEdit || !sourceInfo.CanCopy) throw new IOException("서명 또는 권한 제한이 있는 PDF는 병합할 수 없습니다.");
        int count = FPDF_GetPageCount(doc);
        if (count + sourceInfo.PageCount > 5000) throw new IOException("병합 결과가 5,000페이지를 초과합니다.");
        Check(FPDF_ImportPagesByIndex(doc, source.Handle, Enumerable.Range(0, sourceInfo.PageCount).ToArray(), (nuint)sourceInfo.PageCount, count));
    });

    public static byte[] AddHighlight(byte[] bytes, int index, PdfBox[] boxes) => EditPage(bytes, index, (_, page) =>
    {
        if (boxes.Length == 0) throw new IOException("먼저 텍스트를 선택하세요.");
        nint annotation = FPDFPage_CreateAnnot(page, 9);
        if (annotation == 0) throw new IOException("형광펜 주석을 만들 수 없습니다.");
        try
        {
            Rect rect = new() { Left = (float)boxes.Min(b => b.Left), Bottom = (float)boxes.Min(b => b.Bottom), Right = (float)boxes.Max(b => b.Right), Top = (float)boxes.Max(b => b.Top) };
            Check(FPDFAnnot_SetRect(annotation, ref rect));
            Check(FPDFAnnot_SetColor(annotation, 0, 255, 210, 0, 110));
            Check(FPDFAnnot_SetFlags(annotation, 4));
            foreach (PdfBox b in boxes)
            {
                Quad quad = new() { X1 = (float)b.Left, Y1 = (float)b.Top, X2 = (float)b.Right, Y2 = (float)b.Top, X3 = (float)b.Left, Y3 = (float)b.Bottom, X4 = (float)b.Right, Y4 = (float)b.Bottom };
                Check(FPDFAnnot_AppendAttachmentPoints(annotation, ref quad));
            }
        }
        finally { FPDFPage_CloseAnnot(annotation); }
    });

    public static byte[] AddNote(byte[] bytes, int index, PdfBox box, string text) => EditPage(bytes, index, (_, page) =>
    {
        nint annotation = FPDFPage_CreateAnnot(page, 1);
        if (annotation == 0) throw new IOException("메모를 만들 수 없습니다.");
        try
        {
            Rect rect = new() { Left = (float)box.Left, Bottom = (float)box.Bottom, Right = (float)box.Left + 24, Top = (float)box.Bottom + 24 };
            Check(FPDFAnnot_SetRect(annotation, ref rect));
            Check(FPDFAnnot_SetStringValue(annotation, "Contents", Wide(text)));
            Check(FPDFAnnot_SetColor(annotation, 0, 255, 170, 40, 255));
            Check(FPDFAnnot_SetFlags(annotation, 4));
        }
        finally { FPDFPage_CloseAnnot(annotation); }
    });

    public static byte[] AddRectangle(byte[] bytes, int index, PdfBox box) => EditPage(bytes, index, (_, page) =>
    {
        nint obj = FPDFPageObj_CreateNewRect((float)box.Left, (float)box.Bottom, (float)box.Width, (float)box.Height);
        if (obj == 0) throw new IOException("도형을 만들 수 없습니다.");
        try
        {
            Check(FPDFPageObj_SetStrokeColor(obj, 240, 110, 20, 255));
            Check(FPDFPageObj_SetStrokeWidth(obj, 1.5f));
            Check(FPDFPath_SetDrawMode(obj, 0, 1));
            FPDFPage_InsertObject(page, obj); obj = 0;
        }
        finally { if (obj != 0) FPDFPageObj_Destroy(obj); }
    });

    public static byte[] AddText(byte[] bytes, int index, PdfBox box, string text, float size, byte[] fontBytes) => EditPage(bytes, index, (doc, page) =>
    {
        Matrix matrix = new() { A = 1, D = 1, E = (float)box.Left, F = (float)box.Bottom };
        InsertText(doc, page, text, size, fontBytes, matrix, (0, 0, 0, 255));
    });

    public static byte[] ReplaceText(byte[] bytes, int index, int objectIndex, string text, byte[]? replacementFont = null) => EditPage(bytes, index, (doc, page) =>
    {
        nint obj = GetObject(page, objectIndex, 1);
        ValidateText(text);
        if (replacementFont is null)
        {
            nint font = FPDFTextObj_GetFont(obj);
            foreach (Rune rune in text.EnumerateRunes())
                if (FPDFFont_GetGlyphWidth(font, (uint)rune.Value, 12, out _) == 0)
                    throw new IOException("원래 글꼴에 없는 문자입니다. 원본 글꼴 무시를 켜서 기본 글꼴을 사용하세요.");
            Check(FPDFText_SetText(obj, Wide(text)));
            nint textPage = FPDFText_LoadPage(page);
            try
            {
                if (ObjectText(obj, textPage) != text) throw new IOException("원래 글꼴로 문자를 보존할 수 없습니다. 원본 글꼴 무시를 켜서 기본 글꼴을 사용하세요.");
            }
            finally { FPDFText_ClosePage(textPage); }
        }
        else
        {
            Check(FPDFPageObj_GetMatrix(obj, out Matrix matrix));
            Check(FPDFTextObj_GetFontSize(obj, out float size));
            Check(FPDFPageObj_GetFillColor(obj, out uint r, out uint g, out uint b, out uint a));
            InsertText(doc, page, text, size, replacementFont, matrix, (r, g, b, a));
            Check(FPDFPage_RemoveObject(page, obj));
            FPDFPageObj_Destroy(obj);
        }
    });

    private static void InsertText(nint doc, nint page, string text, float size, byte[] fontBytes, Matrix matrix, (uint R, uint G, uint B, uint A) color)
    {
        ValidateText(text);
        if (!float.IsFinite(size) || size < 1 || size > 300) throw new IOException("글자 크기는 1~300pt입니다.");
        foreach (Rune rune in text.EnumerateRunes())
            if (!PdfFontCoverage.Supports(fontBytes, rune.Value)) throw new IOException("글꼴이 지원하지 않는 문자가 있습니다. 다른 문자를 사용하세요.");
        int fontType = fontBytes.AsSpan().StartsWith("OTTO"u8) ? 1 : 2;
        nint font = FPDFText_LoadFont(doc, fontBytes, (uint)fontBytes.Length, fontType, 1);
        if (font == 0) throw new IOException("글꼴을 불러올 수 없습니다.");
        nint obj = 0;
        try
        {
            foreach (Rune rune in text.EnumerateRunes())
                if (FPDFFont_GetGlyphWidth(font, (uint)rune.Value, size, out _) == 0) throw new IOException("글꼴이 지원하지 않는 문자가 있습니다.");
            obj = FPDFPageObj_CreateTextObj(doc, font, size);
            if (obj == 0) throw new IOException("텍스트 개체를 만들 수 없습니다.");
            Check(FPDFText_SetText(obj, Wide(text)));
            Check(FPDFPageObj_SetMatrix(obj, ref matrix));
            Check(FPDFPageObj_SetFillColor(obj, color.R, color.G, color.B, color.A));
            nint inserted = obj;
            FPDFPage_InsertObject(page, obj); obj = 0;
            nint textPage = FPDFText_LoadPage(page);
            try
            {
                if (ObjectText(inserted, textPage) != text.Replace('\u00a0', ' '))
                    throw new IOException("글꼴의 문자 매핑을 보존할 수 없습니다. 편집 전 내용을 유지합니다.");
            }
            finally { FPDFText_ClosePage(textPage); }
        }
        finally { if (obj != 0) FPDFPageObj_Destroy(obj); FPDFFont_Close(font); }
    }

    public static byte[] ReplaceImage(byte[] bytes, int index, int objectIndex, byte[] bgra, int width, int height) => EditPage(bytes, index, (_, page) =>
    {
        if (width < 1 || height < 1 || width > 4096 || height > 4096 || bgra.Length != checked(width * height * 4)) throw new IOException("이미지는 최대 4,096 × 4,096 픽셀입니다.");
        nint obj = GetObject(page, objectIndex, 3);
        nint bitmap = FPDFBitmap_Create(width, height, 1);
        if (bitmap == 0) throw new IOException("이미지를 만들 수 없습니다.");
        try
        {
            int stride = FPDFBitmap_GetStride(bitmap);
            for (int row = 0; row < height; row++) Marshal.Copy(bgra, row * width * 4, FPDFBitmap_GetBuffer(bitmap) + row * stride, width * 4);
            Check(FPDFImageObj_SetBitmap(0, 0, obj, bitmap));
        }
        finally { FPDFBitmap_Destroy(bitmap); }
    });

    public static byte[] DeleteObject(byte[] bytes, int index, int objectIndex) => EditPage(bytes, index, (_, page) =>
    {
        nint obj = GetObject(page, objectIndex, 0);
        Check(FPDFPage_RemoveObject(page, obj));
        FPDFPageObj_Destroy(obj);
    });

    private static nint GetObject(nint page, int index, int type)
    {
        if (index < 0 || index >= FPDFPage_CountObjects(page)) throw new IOException("개체 선택이 만료되었습니다. 다시 선택하세요.");
        nint obj = FPDFPage_GetObject(page, index);
        int actual = FPDFPageObj_GetType(obj);
        if (type != 0 && actual != type || actual is not (1 or 3)) throw new IOException("이 개체는 편집할 수 없습니다.");
        return obj;
    }

    private static byte[] EditPage(byte[] bytes, int index, Action<nint, nint> action) => Edit(bytes, doc =>
    {
        using Page page = new(doc, index);
        action(doc, page.Handle);
        Check(FPDFPage_GenerateContent(page.Handle));
    });

    private static byte[] Edit(byte[] bytes, Action<nint> action)
    {
        lock (Gate)
        {
            using Document doc = new(bytes);
            PdfInfo info = Info(doc.Handle);
            if (!info.CanEdit) throw new IOException(info.ReadOnlyReason);
            action(doc.Handle);
            using MemoryStream output = new();
            WriteBlock callback = (_, data, size) =>
            {
                // Never propagate managed exceptions through the native callback.
                try
                {
                    if (output.Length + size > PdfFileService.MaximumBytes) return 0;
                    byte[] buffer = new byte[checked((int)size)];
                    Marshal.Copy(data, buffer, 0, buffer.Length);
                    output.Write(buffer);
                    return 1;
                }
                catch { return 0; }
            };
            Writer writer = new() { Version = 1, Callback = Marshal.GetFunctionPointerForDelegate(callback) };
            int success = FPDF_SaveAsCopy(doc.Handle, ref writer, 2);
            GC.KeepAlive(callback);
            if (success == 0) throw new IOException("PDF 저장 데이터를 만들 수 없습니다. 32 MiB 한도를 확인하세요. 편집 전 내용을 유지했습니다.");
            byte[] result = output.ToArray();
            using Document validated = new(result);
            _ = Info(validated.Handle);
            return result;
        }
    }

    private static void ValidateText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > 10_000 || text.Any(char.IsControl))
            throw new IOException("텍스트 개체에는 줄바꿈 없이 1~10,000자를 입력하세요. 삭제는 개체 삭제를 사용하세요.");
        _ = new UnicodeEncoding(false, false, true).GetBytes(text);
    }
    private static byte[] Wide(string text) => new UnicodeEncoding(false, false, true).GetBytes(text + "\0");
    private static void Check(int result) { if (result == 0) throw new IOException("PDF 작업을 적용할 수 없습니다. 편집 전 내용을 유지했습니다."); }
}
