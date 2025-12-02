using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Sandbox
{
    public partial class TestRandom : ContentPage, INotifyPropertyChanged
    {
        public TestRandom()
        {
            try
            {
                InitializeComponent();

                BindingContext = this;
            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
            }
        }
         
    }
}
