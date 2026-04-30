using System;

namespace System.Windows.Input
{
    public sealed class Command : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public Command(Action execute)
            : this(_ => execute(), null)
        {
        }

        public Command(Action execute, Func<bool> canExecute)
            : this(_ => execute(), _ => canExecute())
        {
        }

        public Command(Action<object?> execute)
            : this(execute, null)
        {
        }

        public Command(Action<object?> execute, Func<object?, bool>? canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        public void ChangeCanExecute()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public sealed class Command<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public Command(Action<T?> execute)
            : this(execute, null)
        {
        }

        public Command(Action<T?> execute, Func<T?, bool>? canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke(ConvertParameter(parameter)) ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute(ConvertParameter(parameter));
        }

        public void ChangeCanExecute()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        private static T? ConvertParameter(object? parameter)
        {
            if (parameter is null)
            {
                return default;
            }

            if (parameter is T value)
            {
                return value;
            }

            return (T?)parameter;
        }
    }
}