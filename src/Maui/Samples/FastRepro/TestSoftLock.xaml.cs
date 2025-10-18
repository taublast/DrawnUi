using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Sandbox
{
    public partial class TestSoftLock : ContentPage, INotifyPropertyChanged
    {
        public TestSoftLock()
        {
            try
            {
                InitializeComponent();

                BindingContext = this;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
         
    }
}
