namespace RumLang;

public class StringHelpers
{
    public static string AlignTo(string str, int length, bool alignLeft = false, char padding = ' ')
    {
        if (str.Length >= length)
            return str;

        if (alignLeft)
            return str.PadLeft(length, padding);
        else
            return str.PadRight(length, padding);
    }

    public static string Repeat(string p0, int p1)
    {
        return String.Join(p0, new string[p1 + 1]);
    }
}