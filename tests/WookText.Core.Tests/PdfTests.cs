using static WookText.Testing.PdfFixture;
using WookText.Core.Pdf;

namespace WookText.Core.Tests;

public sealed class PdfTests
{
    [Fact]
    public void Render_TextPage_ProducesPixelsSelectionAndSearch()
    {
        byte[] bytes = Sample();
        Assert.Equal(2, PdfEngine.Inspect(bytes).PageCount);
        PdfFrame frame = PdfEngine.Render(bytes, 0, 1);
        Assert.Contains("Page 1 hello world", frame.Text);
        Assert.NotEmpty(frame.Glyphs);
        Assert.NotEmpty(frame.Objects);
        Assert.Equal(frame.Height * frame.Stride, frame.Pixels.Length);
        Assert.Equal(2, PdfEngine.Search(bytes, "HELLO", false).Length);
        Assert.Empty(PdfEngine.Search(bytes, "HELLO", true));
    }

    [Fact]
    public void PageOperations_Reopen_PreserveOrderAndContent()
    {
        byte[] bytes = Sample(3);
        bytes = PdfEngine.MovePage(bytes, 0, 2);
        Assert.Contains("Page 2", PdfEngine.Render(bytes, 0, 1).Text);
        Assert.Contains("Page 1", PdfEngine.Render(bytes, 2, 1).Text);
        PdfFrame original = PdfEngine.Render(bytes, 0, 1);
        bytes = PdfEngine.Rotate(bytes, 0);
        PdfFrame rotated = PdfEngine.Render(bytes, 0, 1);
        Assert.Equal(original.Width, rotated.Height);
        Assert.Equal(original.Height, rotated.Width);
        bytes = PdfEngine.DeletePage(bytes, 1);
        bytes = PdfEngine.Merge(bytes, Sample(1));
        Assert.Equal(3, PdfEngine.Inspect(bytes).PageCount);
        Assert.Contains("Page 1", PdfEngine.Render(bytes, 2, 1).Text);
    }

    [Fact]
    public void EditText_OriginalObject_ReplacesUnderlyingText()
    {
        byte[] original = Sample(1);
        byte[] edited = PdfEngine.ReplaceText(original, 0, 0, "Changed text");
        PdfFrame frame = PdfEngine.Render(edited, 0, 1);
        Assert.Contains("Changed text", frame.Text);
        Assert.DoesNotContain("hello", frame.Text);
        Assert.Contains("hello", PdfEngine.Render(original, 0, 1).Text);
        Assert.DoesNotContain("Changed", PdfEngine.Render(PdfEngine.DeleteObject(edited, 0, 0), 0, 1).Text);
    }

    [Fact]
    public void AddMarkup_Reopen_ChangesPixelsAndKeepsText()
    {
        byte[] bytes = Sample(1);
        PdfFrame original = PdfEngine.Render(bytes, 0, 1);
        bytes = PdfEngine.AddHighlight(bytes, 0, original.Glyphs.Take(4).Select(g => g.Bounds).ToArray());
        bytes = PdfEngine.AddNote(bytes, 0, new(10, 10, 34, 34), "검토 메모");
        bytes = PdfEngine.AddRectangle(bytes, 0, new(10, 100, 100, 150));
        PdfFrame edited = PdfEngine.Render(bytes, 0, 1);
        Assert.Equal(original.Text, edited.Text);
        Assert.Equal("검토 메모", Assert.Single(edited.Notes).Text);
        Assert.NotEqual(original.Pixels, edited.Pixels);
    }

    [Fact]
    public void AddText_KoreanFont_ReopensWithUnicode()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        byte[] font = File.ReadAllBytes(Path.Combine(root, "src/WookText.App/Assets/Fonts/NotoSansKR-Regular.otf"));
        Assert.Throws<IOException>(() => PdfEngine.AddText(Sample(1), 0, new(20, 200, 200, 230), "😀", 16, font));
        byte[] bytes = PdfEngine.AddText(Sample(1), 0, new(20, 200, 200, 230), "한글 PDF 추가", 16, font);
        Assert.Contains("한글 PDF 추가", PdfEngine.Render(bytes, 0, 1).Text);
        bytes = PdfEngine.ReplaceText(bytes, 0, 0, "한글 교체", font);
        Assert.Contains("한글 교체", PdfEngine.Render(bytes, 0, 1).Text);
        Assert.DoesNotContain("hello", PdfEngine.Render(bytes, 0, 1).Text);
    }

    [Fact]
    public void ReplaceText_DefaultFont_ChangesOnlySelectedObjectAndPreservesOtherPage()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        byte[] font = File.ReadAllBytes(Path.Combine(root, "src/WookText.App/Assets/Fonts/NotoSansKR-Regular.otf"));
        byte[] original = Sample();
        PdfFrame untouched = PdfEngine.Render(original, 1, 1);
        Assert.Throws<IOException>(() => PdfEngine.ReplaceText(original, 0, 0, "한글 수정"));
        byte[] edited = PdfEngine.ReplaceText(original, 0, 0, "한글 수정", font);
        Assert.Contains("한글 수정", PdfEngine.Render(edited, 0, 1).Text);
        Assert.Equal(untouched.Text, PdfEngine.Render(edited, 1, 1).Text);
        Assert.Equal(untouched.Pixels, PdfEngine.Render(edited, 1, 1).Pixels);
        Assert.Contains("hello", PdfEngine.Render(original, 0, 1).Text);
    }

    [Fact]
    public async Task Save_ExternalChange_LeavesOriginalUntouched()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        try
        {
            await File.WriteAllBytesAsync(path, Sample());
            LoadedPdfFile read = await PdfFileService.ReadAsync(path);
            byte[] external = Sample(1);
            await File.WriteAllBytesAsync(path, external);
            await Assert.ThrowsAsync<FileConflictException>(() => PdfFileService.SaveAsync(path, PdfEngine.Rotate(read.Bytes, 0), read.Fingerprint));
            Assert.Equal(external, await File.ReadAllBytesAsync(path));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".*.tmp"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidOperations_Reject_WithoutChangingInput()
    {
        byte[] bytes = Sample(1), copy = bytes.ToArray();
        Assert.Throws<IOException>(() => PdfEngine.DeletePage(bytes, 0));
        Assert.Throws<IOException>(() => PdfEngine.ReplaceText(bytes, 0, 0, "line\nbreak"));
        Assert.Throws<IOException>(() => PdfEngine.Inspect("invalid"u8.ToArray()));
        Assert.Equal(copy, bytes);
    }

    [Fact]
    public void ReplaceImage_Reopen_ChangesPixelsAndPreservesPlacement()
    {
        const string content = "q 100 0 0 100 20 50 cm /Im1 Do Q";
        byte[] bytes = Assemble([
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Resources << /XObject << /Im1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode /Length 7 >>\nstream\nFF0000>\nendstream"
        ]);
        PdfFrame original = PdfEngine.Render(bytes, 0, 1);
        PdfObject image = Assert.Single(original.Objects);
        Assert.Equal(3, image.Type);
        byte[] changed = PdfEngine.ReplaceImage(bytes, 0, image.Index, [255, 0, 0, 255], 1, 1);
        PdfFrame replaced = PdfEngine.Render(changed, 0, 1);
        Assert.Equal(image.Bounds, Assert.Single(replaced.Objects).Bounds);
        Assert.NotEqual(original.Pixels, replaced.Pixels);
        Assert.Empty(PdfEngine.Render(PdfEngine.DeleteObject(changed, 0, image.Index), 0, 1).Objects);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Coordinates_RotatedPage_RoundTripsSelection(int rotations)
    {
        byte[] bytes = Sample(1);
        for (int i = 0; i < rotations; i++) bytes = PdfEngine.Rotate(bytes, 0);
        PdfFrame frame = PdfEngine.Render(bytes, 0, 2);
        PdfGlyph glyph = frame.Glyphs.First(g => g.Text == "h");
        PdfBox actual = PdfEngine.FromScreen(bytes, 0, glyph.ScreenBounds, frame.Width, frame.Height);
        Assert.InRange(Math.Abs(actual.Left - glyph.Bounds.Left), 0, .5);
        Assert.InRange(Math.Abs(actual.Top - glyph.Bounds.Top), 0, .5);
    }

    [Fact]
    public async Task Session_PdfAndMarkdown_RoundTripsWithoutTextDecoding()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            using SessionStore store = new(path);
            byte[] bytes = PdfEngine.Rotate(Sample(), 0);
            SessionState state = new() { Documents = [new() { Kind = "pdf", PdfBytes = bytes, FilePath = Path.GetFullPath("test.pdf"), PdfPage = 1, PdfZoom = 1.5, IsModified = true }, new() { Text = "# 제목", MarkdownPreview = true }] };
            await store.SaveAsync(state);
            SessionLoadResult loaded = await store.LoadAsync();
            Assert.Equal(2, loaded.State.FormatVersion);
            Assert.Equal(bytes, loaded.State.Documents[0].PdfBytes);
            Assert.True(loaded.State.Documents[0].IsModified);
            Assert.Equal(1, loaded.State.Documents[0].PdfPage);
            Assert.True(loaded.State.Documents[1].MarkdownPreview);
            Assert.Equal(2, PdfEngine.Inspect(loaded.State.Documents[0].PdfBytes!).PageCount);
        }
        finally { File.Delete(path); File.Delete(path + ".bak"); }
    }

    [Fact]
    public void SignedPdf_Edit_IsRejected()
    {
        byte[] bytes = Assemble([
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R] >> >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Contents 4 0 R >>",
            "<< /Length 0 >>\nstream\n\nendstream",
            "<< /FT /Sig /T (signature) /V 6 0 R >>",
            "<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 1 2 3] /Contents <0000> >>"
        ]);
        Assert.False(PdfEngine.Inspect(bytes).CanEdit);
        Assert.Throws<IOException>(() => PdfEngine.Rotate(bytes, 0));
    }
}
