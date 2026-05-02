using DrawnUi.Draw;

namespace DrawnUi.Blazor.Views
{
    public class Grid
    {
        public static readonly BindableProperty RowProperty = BindableProperty.CreateAttached("Row",
            typeof(int), typeof(Grid), default(int), validateValue: (bindable, value) => (int)value >= 0);

        public static readonly BindableProperty RowSpanProperty = BindableProperty.CreateAttached("RowSpan",
            typeof(int), typeof(Grid), 1, validateValue: (bindable, value) => (int)value >= 1);

        public static readonly BindableProperty ColumnProperty = BindableProperty.CreateAttached("Column",
            typeof(int), typeof(Grid), default(int), validateValue: (bindable, value) => (int)value >= 0);

        public static readonly BindableProperty ColumnSpanProperty = BindableProperty.CreateAttached("ColumnSpan",
            typeof(int), typeof(Grid), 1, validateValue: (bindable, value) => (int)value >= 1);

        public static int GetColumn(BindableObject bindable) => (int)bindable.GetValue(ColumnProperty);
        public static int GetColumnSpan(BindableObject bindable) => (int)bindable.GetValue(ColumnSpanProperty);
        public static int GetRow(BindableObject bindable) => (int)bindable.GetValue(RowProperty);
        public static int GetRowSpan(BindableObject bindable) => (int)bindable.GetValue(RowSpanProperty);

        public static void SetColumn(BindableObject bindable, int value) => bindable.SetValue(ColumnProperty, value);
        public static void SetColumnSpan(BindableObject bindable, int value) => bindable.SetValue(ColumnSpanProperty, value);
        public static void SetRow(BindableObject bindable, int value) => bindable.SetValue(RowProperty, value);
        public static void SetRowSpan(BindableObject bindable, int value) => bindable.SetValue(RowSpanProperty, value);
    }
}
