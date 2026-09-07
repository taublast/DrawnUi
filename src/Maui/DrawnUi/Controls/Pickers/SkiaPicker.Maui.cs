namespace DrawnUi.Controls;

public partial class SkiaPicker
{
    private async partial Task<int?> ShowSelectionAsyncPlatform(string title, string cancelText, IReadOnlyList<string> options, int selectedIndex)
    {
        // the sheet belongs to the window that owns this picker: with a second window open
        // (a tool/inspector window) the first window is not where the user is looking
        var ownerPage = (Superview as VisualElement)?.Window?.Page;
        var page = GetCurrentPage(ownerPage ?? Application.Current?.Windows.FirstOrDefault()?.Page);
        if (page == null)
        {
            return null;
        }

        var result = await page.DisplayActionSheet(title, cancelText, null, options.ToArray());
        if (string.IsNullOrWhiteSpace(result) || result == cancelText)
        {
            return -1;
        }

        for (var index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index], result, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static Page? GetCurrentPage(Page? page)
    {
        while (page != null)
        {
            switch (page)
            {
                case Shell shell when shell.CurrentPage != null:
                    page = shell.CurrentPage;
                    continue;
                case NavigationPage navigationPage when navigationPage.CurrentPage != null:
                    page = navigationPage.CurrentPage;
                    continue;
                case TabbedPage tabbedPage when tabbedPage.CurrentPage != null:
                    page = tabbedPage.CurrentPage;
                    continue;
                case FlyoutPage flyoutPage when flyoutPage.Detail != null:
                    page = flyoutPage.Detail;
                    continue;
                default:
                    return page;
            }
        }

        return null;
    }
}
