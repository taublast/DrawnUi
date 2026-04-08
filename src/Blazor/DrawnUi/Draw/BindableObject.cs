using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DrawnUi.Draw
{
    public class BindableObject : INotifyPropertyChanged
    {

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
#if DEBUG
            //       dynamic value = Reflection.GetPropertyValueFor(this, propertyName);
            //       Console.WriteLine($"[PropertyChanged] BasePage {propertyName} = {value}");
#endif
            var changed = PropertyChanged;
            if (changed == null)
                return;

            changed.Invoke(this, new PropertyChangedEventArgs(propertyName));

        }
        #endregion

        public object GetValue(BindableProperty clearColorProperty)
        {
            throw new NotImplementedException();
        }
        public void SetValue(BindableProperty clearColorProperty, object value)
        {
            throw new NotImplementedException();
        }
    }

    public class BindableProperty           
    {
        public static BindableProperty Create(string clearColorName, Type type, Type type1, object defaultValue,
            Action<BindableObject, object, object> propertyChanged)
        {
            throw new NotImplementedException();
        }
    }
}
