using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using DrawnUi.Infrastructure.Enums;

namespace DrawnUi.Views
{

    [ContentProperty("Children")]
    public partial class DrawnView : ContentView, IDrawnBase, IAnimatorsManager, IVisualTreeElement
    {
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
