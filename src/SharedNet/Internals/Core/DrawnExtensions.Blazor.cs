namespace DrawnUi.Extensions;

public static class DrawnExtensionsBlazor
{
    public static bool IsSameAs(this string strA, string strB)
    {
        return string.Compare(strA, strB, StringComparison.Ordinal) == 0;
    }

    public static bool IsSameAs(this Uri strA, Uri strB)
    {
        if (strA == null || strB == null)
        {
            return strA == strB;
        }

        return string.Compare(strA.AbsoluteUri, strB.AbsoluteUri, StringComparison.Ordinal) == 0;
    }
}