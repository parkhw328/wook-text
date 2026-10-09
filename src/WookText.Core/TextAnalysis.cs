namespace WookText.Core;

public static class TextAnalysis
{
    public static string LineEndings(string text)
    {
        bool crlf = false, lf = false, cr = false;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') { crlf = true; i++; }
                else cr = true;
            }
            else if (text[i] == '\n') lf = true;
        }
        if ((crlf ? 1 : 0) + (lf ? 1 : 0) + (cr ? 1 : 0) > 1) return "혼합 줄바꿈";
        return crlf ? "CRLF" : lf ? "LF" : cr ? "CR" : "줄바꿈 없음";
    }

    public static int FindNext(string text, string query, int start, bool matchCase)
    {
        if (string.IsNullOrEmpty(query)) return -1;
        StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        start = Math.Clamp(start, 0, text.Length);
        int index = text.IndexOf(query, start, comparison);
        return index >= 0 ? index : text.IndexOf(query, 0, comparison);
    }
}
