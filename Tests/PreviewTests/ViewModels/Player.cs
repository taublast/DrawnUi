using System.ComponentModel;
using System.Runtime.CompilerServices;
using AppoMobi.Specials;
using Newtonsoft.Json;

namespace PreviewTests
{
    public class Player : INotifyPropertyChanged, IHasStringId, ICanBeSelected//, IPatchableDto
    {

        private string _Id;
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (_Id != value)
                {
                    _Id = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsMe
        {
            get
            {
                return "71980d5c-2659-4ccb-ba79-e4433514fae5" == this.Id;
            }
        }

        public Player()
        {
            Id = Guid.NewGuid().ToString();
        }

        private string _PersonDesc;
        public string PersonDesc
        {
            get { return _PersonDesc; }
            set
            {
                if (_PersonDesc != value)
                {
                    _PersonDesc = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _IsOnline;
        public new bool IsOnline
        {
            get
            {
                return _IsOnline;
            }
            set
            {
                if (_IsOnline != value)
                {
                    _IsOnline = value;
                    OnPropertyChanged();
                    OnPropertyChanged("OnlineStatus");
                    OnPropertyChanged("OnlineDesc");
                }
            }
        }

        private bool _IsDND;
        public new bool IsDND
        {
            get { return _IsDND; }
            set
            {
                if (_IsDND != value)
                {
                    _IsDND = value;
                    OnPropertyChanged();
                    OnPropertyChanged("OnlineStatus");
                    OnPropertyChanged("OnlineDesc");
                }
            }
        }

        [JsonIgnore]
        public int OnlineStatus
        {
            get
            {
                if (IsDND && IsOnline)
                {
                    return 2;
                }

                return IsOnline ? 1 : 0;
            }
            //set
            //{
            //    if (_OnlineStatus != value)
            //    {
            //        _OnlineStatus = value;
            //        OnPropertyChanged();
            //    }
            //}
        }




        private string _ImageMain;
        public string ImageMain
        {
            get { return _ImageMain; }
            set
            {
                if (_ImageMain != value)
                {
                    _ImageMain = value;
                    OnPropertyChanged();
                }
            }
        }



        private string _SpokenLanguagesDesc;
        public string SpokenLanguagesDesc
        {
            get { return _SpokenLanguagesDesc; }
            set
            {
                if (_SpokenLanguagesDesc != value)
                {
                    _SpokenLanguagesDesc = value;
                    OnPropertyChanged();
                }
            }
        }



        private bool _Selected;
        public bool Selected
        {
            get { return _Selected; }
            set
            {
                if (_Selected != value)
                {
                    _Selected = value;
                    OnPropertyChanged("Selected");
                }
            }
        }

        public void RaiseProperties()
        {
            var props = this.GetType().GetProperties();
            foreach (var property in props)
            {
                if (property.CanRead)
                {
                    OnPropertyChanged(property.Name);
                }
            }
        }

        private bool _NeedShowLetter;
        public bool NeedShowLetter
        {
            get { return _NeedShowLetter; }
            set
            {
                if (_NeedShowLetter != value)
                {
                    _NeedShowLetter = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _DisplayLetter;
        public string DisplayLetter
        {
            get { return _DisplayLetter; }
            set
            {
                if (_DisplayLetter != value)
                {
                    _DisplayLetter = value;
                    OnPropertyChanged();
                }
            }
        }


        private decimal _PayAmount;
        public decimal PayAmount
        {
            get { return _PayAmount; }
            set
            {
                if (_PayAmount != value)
                {
                    _PayAmount = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _ManualAmount;
        public bool ManualAmount
        {
            get { return _ManualAmount; }
            set
            {
                if (_ManualAmount != value)
                {
                    _ManualAmount = value;
                    OnPropertyChanged();
                }
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




        #region INotifyPropertyChanged

        public void RaisePropertyChanged(string propertyName)
        {
            var changed = PropertyChanged;
            if (changed == null)
                return;

            changed.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            var changed = PropertyChanged;
            if (changed == null)
                return;

            changed.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        private bool _Notify;
        public bool Notify
        {
            get { return _Notify; }
            set
            {
                if (_Notify != value)
                {
                    _Notify = value;
                    OnPropertyChanged();
                }
            }
        }







    }
}
