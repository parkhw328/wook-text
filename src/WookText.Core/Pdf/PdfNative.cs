using System.Runtime.InteropServices;

namespace WookText.Core.Pdf;

// PDFium C API, pinned to chromium/8100. Handles never escape the global engine lock.
internal static class PdfNative
{
    [StructLayout(LayoutKind.Sequential)] internal struct Matrix { public float A, B, C, D, E, F; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public float Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct Quad { public float X1, Y1, X2, Y2, X3, Y3, X4, Y4; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int WriteBlock(nint writer, nint data, uint size);
    [StructLayout(LayoutKind.Sequential)] internal struct Writer { public int Version; public nint Callback; }
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDF_InitLibrary();
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint FPDF_LoadMemDocument64(nint data, nuint size, nint password);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDF_CloseDocument(nint doc);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint FPDF_GetLastError();
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDF_GetPageCount(nint doc);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nuint FPDF_GetDocPermissions(nint doc);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDF_GetSignatureCount(nint doc);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint FPDF_LoadPage(nint doc, int index);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDF_ClosePage(nint page);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern float FPDF_GetPageWidthF(nint page);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern float FPDF_GetPageHeightF(nint page);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDF_PageToDevice(nint page, int x, int y, int width, int height, int rotate, double pageX, double pageY, out int deviceX, out int deviceY);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDF_DeviceToPage(nint page, int x, int y, int width, int height, int rotate, int deviceX, int deviceY, out double pageX, out double pageY);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint FPDFBitmap_Create(int width, int height, int alpha);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDFBitmap_Destroy(nint bitmap);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint FPDFBitmap_GetBuffer(nint bitmap);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFBitmap_GetStride(nint bitmap);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDFBitmap_FillRect(nint bitmap, int left, int top, int width, int height, uint color);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDF_RenderPageBitmap(nint bitmap, nint page, int x, int y, int width, int height, int rotate, int flags);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint FPDFText_LoadPage(nint page);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDFText_ClosePage(nint text);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFText_CountChars(nint text);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint FPDFText_GetUnicode(nint text, int index);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFText_GetCharBox(nint text, int index, out double left, out double right, out double bottom, out double top);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFPage_CountObjects(nint page);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint FPDFPage_GetObject(nint page, int index);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFPageObj_GetType(nint obj);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFPageObj_GetBounds(nint obj, out float left, out float bottom, out float right, out float top);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFPageObj_GetMatrix(nint obj, out Matrix matrix);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFPageObj_SetMatrix(nint obj, ref Matrix matrix);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFPageObj_GetFillColor(nint obj, out uint r, out uint g, out uint b, out uint a);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFPageObj_SetFillColor(nint obj, uint r, uint g, uint b, uint a);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFPageObj_SetStrokeColor(nint obj, uint r, uint g, uint b, uint a);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFPageObj_SetStrokeWidth(nint obj, float width);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFPath_SetDrawMode(nint obj, int fill, int stroke);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFPageObj_GetIsActive(nint obj, out int active);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDFPageObj_Transform(nint obj, double a, double b, double c, double d, double e, double f);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFPage_RemoveObject(nint page, nint obj);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDFPageObj_Destroy(nint obj);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDFPage_InsertObject(nint page, nint obj);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFPage_GenerateContent(nint page);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint FPDFPageObj_CreateNewRect(float x, float y, float width, float height);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint FPDFPageObj_CreateTextObj(nint doc, nint font, float size);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint FPDFText_LoadFont(nint doc, byte[] data, uint size, int type, int cid);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDFFont_Close(nint font);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFText_SetText(nint obj, byte[] text);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nuint FPDFTextObj_GetText(nint obj, nint text, byte[] buffer, nuint size);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFTextObj_GetFontSize(nint obj, out float size);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint FPDFTextObj_GetFont(nint obj);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFFont_GetGlyphWidth(nint font, uint unicode, float size, out float width);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFImageObj_SetBitmap(nint pages, int count, nint obj, nint bitmap);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFPage_GetRotation(nint page);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDFPage_SetRotation(nint page, int rotation);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDFPage_Delete(nint doc, int index);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDF_MovePages(nint doc, int[] indices, nuint count, int destination);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDF_ImportPagesByIndex(nint doc, nint source, int[] indices, nuint count, int destination);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint FPDFPage_CreateAnnot(nint page, int subtype);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDFPage_CloseAnnot(nint annotation);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFAnnot_SetRect(nint annotation, ref Rect rect);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFAnnot_SetColor(nint annotation, int type, uint r, uint g, uint b, uint a);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFAnnot_AppendAttachmentPoints(nint annotation, ref Quad quad);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFAnnot_SetStringValue(nint annotation, [MarshalAs(UnmanagedType.LPStr)] string key, byte[] value);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDFAnnot_SetFlags(nint annotation, int flags);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDF_SaveAsCopy(nint doc, ref Writer writer, uint flags);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)] internal static extern int FPDFPage_GetAnnotCount(nint page);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)] internal static extern nint FPDFPage_GetAnnot(nint page, int index);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)] internal static extern int FPDFAnnot_GetSubtype(nint annotation);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)] internal static extern int FPDFAnnot_GetRect(nint annotation, out Rect rect);
    [DllImport("pdfium", CallingConvention = CallingConvention.Cdecl)] internal static extern nuint FPDFAnnot_GetStringValue(nint annotation, [MarshalAs(UnmanagedType.LPStr)] string key, byte[] buffer, nuint size);
}
