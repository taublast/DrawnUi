using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using DrawnUi.Infrastructure.Enums;
using Microsoft.Maui.HotReload;

namespace DrawnUi.Views
{

    [ContentProperty("Children")]
    public partial class DrawnView : ContentView, IDrawnBase, IAnimatorsManager, IVisualTreeElement
    {
        private void Init()
        {
            if (!_initialized)
            {
                _initialized = true;

                HorizontalOptions = LayoutOptions.Start;
                VerticalOptions = LayoutOptions.Start;
                Padding = new Thickness(0);

                SizeChanged += ViewSizeChanged;

                //bug this creates garbage on aandroid on every frame
                // DeviceDisplay.Current.MainDisplayInfoChanged += OnMainDisplayInfoChanged;
                InitFramework(true);


                SurfaceCacheManager = new(this);
            }
        }

        public virtual void OnHotReload()
        {

        }

        protected virtual void InitFramework(bool subscribe)
        {
            if (subscribe)
            {
                Super.HotReload -= SuperOnHotReload;
                Super.HotReload += SuperOnHotReload;
            }
            else
            {
                Super.HotReload -= SuperOnHotReload;
            }
        }

        private void SuperOnHotReload(Type[] obj)
        {
            OnHotReload();
        }
    }
}
