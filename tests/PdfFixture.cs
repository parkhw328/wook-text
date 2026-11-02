using System.Text;

namespace WookText.Testing;

public static class PdfFixture
{
    public static byte[] Sample(int pages = 2)
    {
        List<string> objects = ["<< /Type /Catalog /Pages 2 0 R >>", ""];
        for (int i = 0; i < pages; i++)
        {
            int stream = objects.Count + 2;
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /Contents {stream} 0 R >>");
            string content = $"BT /F1 18 Tf 30 330 Td (Page {i + 1} hello world) Tj ET";
            objects.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
        }
        objects[1] = $"<< /Type /Pages /Count {pages} /Kids [{string.Join(" ", Enumerable.Range(0, pages).Select(i => $"{3 + i * 2} 0 R"))}] >>";
        return Assemble(objects);
    }

    public static byte[] Assemble(List<string> objects)
    {
        StringBuilder pdf = new("%PDF-1.7\n");
        List<int> offsets = [];
        for (int i = 0; i < objects.Count; i++) { offsets.Add(pdf.Length); pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        int xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (int offset in offsets) pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

}
