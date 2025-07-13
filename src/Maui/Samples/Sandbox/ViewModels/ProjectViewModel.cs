namespace Sandbox.ViewModels
{
    public class ProjectViewModel : BindableObject
    {
        public bool IsDebug
        {
            get
            {
#if DEBUG
                return true;
#endif
                return false;
            }
        }

        private bool _IsBusy;
        public bool IsBusy
        {
            get { return _IsBusy; }
            set
            {
                if (_IsBusy != value)
                {
                    _IsBusy = value;
                    OnPropertyChanged();
                }
            }
        }

        bool _disposed;
        public void Dispose()
        {
            if (_disposed)
                return;

            Subscribe(false);

            OnDisposing();

            _disposed = true;
        }

        public virtual void OnDisposing()
        {

        }

        public virtual void OnSubscribing(bool subscribe)
        {

        }

        void Subscribe(bool subscribe = true)
        {
            OnSubscribing(subscribe);

        }

    }
}
