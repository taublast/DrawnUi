using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Windows.Input;
using DrawnUi.Infrastructure.Xaml;

namespace DrawnUi.Draw
{
    /// <summary>
    /// Provides extension methods for fluent API design pattern with DrawnUI controls
    /// </summary>
    public static partial class FluentExtensions
    {
        public static T AssignNative<T>(this T control, out T variable) where T : VisualElement
        {
            variable = control;
            return control;
        }

        #region GRID
        /// <summary>
        /// Sets the Grid.Row attached property for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set the row for</param>
        /// <param name="row">The row index</param>
        /// <returns>The control for chaining</returns>
        public static T WithRow<T>(this T view, int row) where T : SkiaControl
        {
            Grid.SetRow(view, row);
            return view;
        }

        /// <summary>
        /// Sets the Grid.Column attached property for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set the column for</param>
        /// <param name="column">The column index</param>
        /// <returns>The control for chaining</returns>
        public static T WithColumn<T>(this T view, int column) where T : SkiaControl
        {
            Grid.SetColumn(view, column);
            return view;
        }

        /// <summary>
        /// Sets the Grid.RowSpan attached property for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set the row span for</param>
        /// <param name="rowSpan">The number of rows to span</param>
        /// <returns>The control for chaining</returns>
        public static T WithRowSpan<T>(this T view, int rowSpan) where T : SkiaControl
        {
            Grid.SetRowSpan(view, rowSpan);
            return view;
        }

        /// <summary>
        /// Sets the Grid.ColumnSpan attached property for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set the column span for</param>
        /// <param name="columnSpan">The number of columns to span</param>
        /// <returns>The control for chaining</returns>
        public static T WithColumnSpan<T>(this T view, int columnSpan) where T : SkiaControl
        {
            Grid.SetColumnSpan(view, columnSpan);
            return view;
        }

        /// <summary>
        /// Parses a string representation of column definitions and sets them on the grid
        /// </summary>
        /// <param name="grid">The grid to set column definitions for</param>
        /// <param name="columnDefinitions">String in format like "Auto,*,2*,100"</param>
        /// <returns>The grid for chaining</returns>
        /// <exception cref="InvalidOperationException">Thrown if conversion fails</exception>
        public static SkiaLayout WithColumnDefinitions(this SkiaLayout grid, string columnDefinitions)
        {
            var converter = new ColumnDefinitionCollectionTypeConverter();
            if (converter.CanConvertFrom(typeof(string)))
            {
                var columns = (ColumnDefinitionCollection)converter.ConvertFromInvariantString(columnDefinitions);
                grid.ColumnDefinitions = columns;
            }
            else
            {
                throw new InvalidOperationException("ColumnDefinitionCollectionTypeConverter cannot convert from string.");
            }
            return grid;
        }

        /// <summary>
        /// Parses a string representation of points and sets them on the shape
        /// </summary>
        /// <param name="shape"></param>
        /// <param name="columnDefinitions"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static SkiaShape WithPoints(this SkiaShape shape, string columnDefinitions)
        {
            var converter = new SkiaPointCollectionConverter();
            if (converter.CanConvertFrom(typeof(string)))
            {
                var points = (IList<SkiaPoint>)converter.ConvertFromInvariantString(columnDefinitions);
                shape.Points = points;
            }
            else
            {
                throw new InvalidOperationException("SkiaPointCollectionConverter cannot convert from string.");
            }
            return shape;
        }

        /// <summary>
        /// Parses a string representation of row definitions and sets them on the grid
        /// </summary>
        /// <param name="grid">The grid to set row definitions for</param>
        /// <param name="definitions">String in format like "Auto,*,2*,100"</param>
        /// <returns>The grid for chaining</returns>
        /// <exception cref="InvalidOperationException">Thrown if conversion fails</exception>
        public static SkiaLayout WithRowDefinitions(this SkiaLayout grid, string definitions)
        {
            var converter = new RowDefinitionCollectionTypeConverter();
            if (converter.CanConvertFrom(typeof(string)))
            {
                var defs = (RowDefinitionCollection)converter.ConvertFromInvariantString(definitions);
                grid.RowDefinitions = defs;
            }
            else
            {
                throw new InvalidOperationException("RowDefinitionCollectionTypeConverter cannot convert from string.");
            }
            return grid;
        }

        /// <summary>
        /// Sets the Grid row and column in a single call
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set the grid position for</param>
        /// <param name="column">The column index</param>
        /// <param name="row">The row index</param>
        /// <returns>The control for chaining</returns>
        public static T SetGrid<T>(this T view, int column, int row) where T : SkiaControl
        {
            Grid.SetRow(view, row);
            Grid.SetColumn(view, column);
            return view;
        }

        /// <summary>
        /// Sets the Grid row, column, rowspan and columnspan in a single call
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set the grid position for</param>
        /// <param name="row">The row index</param>
        /// <param name="column">The column index</param>
        /// <param name="rowSpan">The number of rows to span</param>
        /// <param name="columnSpan">The number of columns to span</param>
        /// <returns>The control for chaining</returns>
        public static T SetGrid<T>(this T view, int column, int row,
            int columnSpan, int rowSpan) where T : SkiaControl
        {
            Grid.SetRow(view, row);
            Grid.SetColumn(view, column);
            Grid.SetRowSpan(view, rowSpan);
            Grid.SetColumnSpan(view, columnSpan);
            return view;
        }


        #endregion

        #region BINDING

        /// <summary>
        /// Sets up a simple non-compiled binding for a property
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <typeparam name="TProperty">Type of the property</typeparam>
        /// <param name="view">The control to set the binding for</param>
        /// <param name="targetProperty">The target property</param>
        /// <param name="path">The binding path</param>
        /// <param name="mode">The binding mode</param>
        /// <returns>The control for chaining</returns>
        public static T BindProperty<T>(this T view, BindableProperty property, string path, BindingMode mode = BindingMode.Default) where T : SkiaControl
        {
            view.SetBinding(property, path, mode);

            return view;
        }

        /// <summary>
        /// Binds a property of a view to a source property using a specified path and binding mode.
        /// </summary>
        /// <typeparam name="T">Represents a type that extends SkiaControl, allowing for binding operations on UI elements.</typeparam>
        /// <param name="view">The UI element that will have its property bound to a source property.</param>
        /// <param name="targetProperty">The property of the view that will receive the binding.</param>
        /// <param name="source">The object that implements property change notifications and serves as the data source.</param>
        /// <param name="path">The path to the property on the source object that will be bound to the target property.</param>
        /// <param name="mode">Specifies the binding mode, determining how the source and target properties interact.</param>
        /// <returns>Returns the view after setting up the binding.</returns>
        public static T BindProperty<T>(this T view,
            BindableProperty targetProperty,
            INotifyPropertyChanged source,
            string path,
            BindingMode mode = BindingMode.Default)
            where T : SkiaControl
        {
            view.SetBinding(targetProperty, new Binding { Path = path, Mode = mode, Source = source });

            return view;
        }

        /// <summary>
        /// Sets up a simple binding for a property with a converter
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <typeparam name="TProperty">Type of the property</typeparam>
        /// <param name="view">The control to set the binding for</param>
        /// <param name="targetProperty">The target property</param>
        /// <param name="path">The binding path</param>
        /// <param name="converter">The value converter</param>
        /// <param name="converterParameter">The converter parameter</param>
        /// <param name="mode">The binding mode</param>
        /// <returns>The control for chaining</returns>
        public static T BindProperty<T, TProperty>(this T view, BindableProperty targetProperty, string path, IValueConverter converter, object converterParameter = null, BindingMode mode = BindingMode.Default) where T : SkiaControl
        {
            view.SetBinding(targetProperty, new Binding(path, mode, converter, converterParameter));
            return view;
        }

        #endregion


        #region THICKNESS

        public static Thickness WithTop(this Thickness existing, double value)
        {
            return new Thickness
            {
                Top = value,
                Bottom = existing.Bottom,
                Left = existing.Left,
                Right = existing.Right
            };
        }
        public static Thickness WithBottom(this Thickness existing, double value)
        {
            return new Thickness
            {
                Top = existing.Top,
                Bottom = value,
                Left = existing.Left,
                Right = existing.Right
            };
        }
        public static Thickness WithLeft(this Thickness existing, double value)
        {
            return new Thickness
            {
                Top = existing.Top,
                Bottom = existing.Bottom,
                Left = value,
                Right = existing.Right
            };
        }
        public static Thickness WithRight(this Thickness existing, double value)
        {
            return new Thickness
            {
                Top = existing.Top,
                Bottom = existing.Bottom,
                Left = existing.Left,
                Right = value
            };
        }

        #endregion

    }


}
