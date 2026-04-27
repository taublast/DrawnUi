global using System.Collections.Specialized;
global using AppoMobi.Maui.Gestures;
global using System.ComponentModel;
global using Microsoft.Maui;
global using System.Numerics;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Windows.Input;
global using AppoMobi.Specials;
global using DrawnUi.Draw;
global using DrawnUi.Extensions;
global using DrawnUi.Infrastructure;
global using DrawnUi.Infrastructure.Models;
global using DrawnUi.Models;
global using Microsoft.Maui.ApplicationModel;
global using Microsoft.Maui.Controls;
global using Microsoft.Maui.Devices;
global using Microsoft.Maui.Graphics;
//global using DrawnUi.Views;
global using DrawnUi.Views;
global using SkiaSharp;
//global using SkiaSharp.Views.Maui;
//global using SkiaSharp.Views.Maui.Controls;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrawnUi.Draw
{
    public partial class Super
    {
        public static object App { get; set; }

        public static IServiceProvider Services
        {
            get => _services;
            set
            {
                _services = value;
                _servicesFromHandler = value != null;
            }
        }

        public static object AppContext => null;

        public static void DisplayException(VisualElement view, Exception e)
        {
            Log(e?.ToString() ?? string.Empty, LogLevel.Error);

            if (view == null)
                throw e;

            view.Update();
        }

        public static void Log(string message, LogLevel logLevel = LogLevel.Warning, [System.Runtime.CompilerServices.CallerMemberName] string caller = null)
        {
            if (DrawnExtensions.StartupSettings != null)
            {
                DrawnExtensions.StartupSettings.Logger?.Log(logLevel, message);
            }

            Console.WriteLine(message);
        }

        public static void Log(LogLevel level, string message, [System.Runtime.CompilerServices.CallerMemberName] string caller = null)
        {
            Log(message, level, caller);
        }

        static partial void OnMaxFpsChanged(int fps)
        {
        }





    }
}
