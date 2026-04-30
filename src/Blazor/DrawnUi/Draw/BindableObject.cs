using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;

namespace DrawnUi.Draw
{
    public class BindableObject : LayoutComponentBase, INotifyPropertyChanged
    {
        private readonly Dictionary<BindableProperty, object> _values = new();
        private object _bindingContext;

        public event PropertyChangingEventHandler PropertyChanging;

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

        protected virtual void OnPropertyChanging([CallerMemberName] string propertyName = "")
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
        }
        #endregion

        public object BindingContext
        {
            get => _bindingContext;
            set
            {
                if (ReferenceEquals(_bindingContext, value))
                    return;

                _bindingContext = value;
                OnPropertyChanged();
                OnBindingContextChanged();
            }
        }

        protected virtual void OnBindingContextChanged()
        {

        }



        protected virtual void InvalidateMeasure()
        {

        }

        public object GetValue(BindableProperty property)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            if (_values.TryGetValue(property, out var value))
                return value;

            var defaultValue = property.GetDefaultValue(this);
            if (defaultValue != null || property.HasExplicitDefaultValue)
            {
                _values[property] = defaultValue;
            }

            return defaultValue;
        }

        public void SetValue(BindableProperty property, object value)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            if (!property.IsValid(this, value))
                throw new ArgumentException($"Invalid value for property {property.PropertyName}.", nameof(value));

            value = property.Coerce(this, value);

            var hadExistingValue = _values.TryGetValue(property, out var oldValue);
            if (!hadExistingValue)
            {
                oldValue = property.GetDefaultValue(this);
            }

            if (Equals(oldValue, value))
                return;

            OnPropertyChanging(property.PropertyName);
            _values[property] = value;
            property.PropertyChanged?.Invoke(this, oldValue, value);
            OnPropertyChanged(property.PropertyName);
        }

        public static void SetInheritedBindingContext(BindableObject bindable, object value)
        {
            if (bindable != null)
            {
                bindable.BindingContext = value;
            }
        }
    }

    public sealed class BindableProperty
    {
        private BindableProperty(
            string propertyName,
            Type returnType,
            Type declaringType,
            object defaultValue,
            Func<BindableObject, object> defaultValueCreator,
            Func<BindableObject, object, bool> validateValue,
            Action<BindableObject, object, object> propertyChanged,
            Func<BindableObject, object, object> coerceValue)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
            DeclaringType = declaringType ?? throw new ArgumentNullException(nameof(declaringType));
            DefaultValue = defaultValue;
            DefaultValueCreator = defaultValueCreator;
            ValidateValue = validateValue;
            PropertyChanged = propertyChanged;
            CoerceValue = coerceValue;
            HasExplicitDefaultValue = defaultValueCreator != null || defaultValue != null;
        }

        public string PropertyName { get; }

        public Type ReturnType { get; }

        public Type DeclaringType { get; }

        public object DefaultValue { get; }

        public bool HasExplicitDefaultValue { get; }

        internal Func<BindableObject, object> DefaultValueCreator { get; }

        internal Func<BindableObject, object, bool> ValidateValue { get; }

        internal Action<BindableObject, object, object> PropertyChanged { get; }

        internal Func<BindableObject, object, object> CoerceValue { get; }

        public static BindableProperty Create(
            string propertyName,
            Type returnType,
            Type declaringType,
            object defaultValue = null,
            Func<BindableObject, object> defaultValueCreator = null,
            Func<BindableObject, object, bool> validateValue = null,
            Action<BindableObject, object, object> propertyChanged = null,
            Func<BindableObject, object, object> coerceValue = null)
        {
            return new BindableProperty(
                propertyName,
                returnType,
                declaringType,
                defaultValue,
                defaultValueCreator,
                validateValue,
                propertyChanged,
                coerceValue);
        }

        public static BindableProperty Create(
            string propertyName,
            Type returnType,
            Type declaringType,
            object defaultValue,
            BindingMode defaultBindingMode,
            Func<BindableObject, object> defaultValueCreator = null,
            Func<BindableObject, object, bool> validateValue = null,
            Action<BindableObject, object, object> propertyChanged = null,
            Func<BindableObject, object, object> coerceValue = null)
        {
            return Create(
                propertyName,
                returnType,
                declaringType,
                defaultValue,
                defaultValueCreator,
                validateValue,
                propertyChanged,
                coerceValue);
        }

        internal object GetDefaultValue(BindableObject bindable)
        {
            return DefaultValueCreator?.Invoke(bindable) ?? DefaultValue;
        }

        internal bool IsValid(BindableObject bindable, object value)
        {
            return ValidateValue?.Invoke(bindable, value) ?? true;
        }

        internal object Coerce(BindableObject bindable, object value)
        {
            return CoerceValue?.Invoke(bindable, value) ?? value;
        }    

        public static BindableProperty CreateAttached(
            string propertyName,
            Type returnType,
            Type declaringType,
            object defaultValue = null,
            Func<object, object, bool> validateValue = null,
            Action<BindableObject, object, object> propertyChanged = null)
        {
            return new BindableProperty(
                propertyName,
                returnType,
                declaringType,
                defaultValue,
                null,
                (bindable, value) => validateValue?.Invoke(bindable, value) ?? true,
                propertyChanged,
                null);
        }
    }
}
