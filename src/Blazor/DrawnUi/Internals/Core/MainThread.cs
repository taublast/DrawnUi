using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace Microsoft.Maui.ApplicationModel
{
    public static class MainThread
    {
        public static bool IsMainThread => true;

        public static void BeginInvokeOnMainThread(Action action)
        {
            action?.Invoke();
        }

        public static Task InvokeOnMainThreadAsync(Action action)
        {
            action?.Invoke();
            return Task.CompletedTask;
        }

        public static async Task InvokeOnMainThreadAsync(Func<Task> action)
        {
            if (action != null)
            {
                await action();
            }
        }
    }
}

namespace Microsoft.Maui
{
}

namespace Microsoft.Maui.Devices
{
}

namespace Microsoft.Maui.Graphics
{
}

namespace Microsoft.Maui.Controls
{
}

