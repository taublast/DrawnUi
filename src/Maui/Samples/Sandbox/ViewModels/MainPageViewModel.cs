using AppoMobi.Specials;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Sandbox
{
    class MainPageViewModel : BindableObject
    {

        private Color _selectedColor = Colors.Lime;
        public Color SelectedColor
        {
            get
            {
                return _selectedColor;
            }
            set
            {
                if (_selectedColor != value)
                {
                    _selectedColor = value;
                    OnPropertyChanged();
                    //Debug.WriteLine($"Tint {value}");
                }
            }
        }


        private string _Search = "";
        public string Search
        {
            get
            {
                return _Search;
            }
            set
            {
                if (_Search != value)
                {
                    _Search = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _ProgressSpeed = 400;
        public double ProgressSpeed
        {
            get
            {
                return _ProgressSpeed;
            }
            set
            {
                if (_ProgressSpeed != value)
                {
                    _ProgressSpeed = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _Progress;
        public double Progress
        {
            get
            {
                return _Progress;
            }
            set
            {
                if (_Progress != value)
                {
                    _Progress = value;
                    OnPropertyChanged();
                }
            }
        }

        public MainPageViewModel()
        {
            Tasks.StartDelayed(TimeSpan.FromSeconds(2), () =>
            {
                CommandProgressStart.Execute(null);
            });
        }

        public ICommand CommandProgressStart
        {
            get
            {
                return new Command(async (object context) =>
                {
                    ProgressSpeed = 0;
                    Progress = 0;
                    await Task.Delay(500);

                    ProgressSpeed = 3000;
                    Progress = 30;
                });
            }
        }

        public ICommand CommandProgressAnimated
        {
            get
            {
                return new Command(async (object context) =>
                {
                    if (SkiaControl.AreEqual(Progress, 30, 1))
                    {
                        ProgressSpeed = 5000;
                        Progress = 45;
                    }
                    else
                    if (SkiaControl.AreEqual(Progress, 45, 1))
                    {
                        ProgressSpeed = 2000;
                        Progress = 90;
                    }
                    else
                    if (SkiaControl.AreEqual(Progress, 90, 1))
                    {
                        ProgressSpeed = 5000;
                        Progress = 100;
                    }
                });
            }
        }



        public ICommand CommandSearch
        {
            get
            {
                return new Command(async (object context) =>
                {

                    Console.WriteLine($"[TEXT] {context}");
                });
            }
        }


        public Command CommandTest => new Command(async () =>
        {
            if (CheckLockAndSet())
                return;

            //for drawer
            IsOpen = !IsOpen;

            //for carousel
            if (SelectedIndex < 3)
                SelectedIndex++;
            else
                SelectedIndex = 0;
        });

        public double OffsetMap
        {
            get
            {
                return DrawerHeaderSize - 20; //move down for rounded corners
            }
        }

        public double DrawerHeaderSize
        {
            get
            {
                return Super.Screen.BottomInset + 60;
            }
        }

        public double BottomInsets
        {
            get
            {
                return Super.Screen.BottomInset;
            }
        }

        private bool _IsOpen;
        public bool IsOpen
        {
            get
            {
                return _IsOpen;
            }
            set
            {
                if (_IsOpen != value)
                {
                    _IsOpen = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _SelectedIndex;
        public int SelectedIndex
        {
            get
            {
                return _SelectedIndex;
            }
            set
            {
                if (_SelectedIndex != value)
                {
                    _SelectedIndex = value;
                    OnPropertyChanged();
                }
            }
        }


        #region TAP LOCKS

        protected bool CheckLocked(string uid)
        {
            if (TapLocks.ContainsKey(uid))
            {
                return TapLocks[uid];
            }
            return false;
        }

        protected bool CheckLockAndSet([CallerMemberName] string uid = null, int ms = 250)
        {
            if (CheckLocked(uid))
                return true;

            TapLocks[uid] = true;
            Tasks.StartTimerAsync(TimeSpan.FromMilliseconds(ms), async () =>
            {
                TapLocks[uid] = false;
                return false;
            });

            return false;
        }

        public static Dictionary<string, bool> TapLocks = new();

        #endregion

        private double _Value1 = 1;
        public double Value1
        {
            get
            {
                return _Value1;
            }
            set
            {
                if (_Value1 != value)
                {
                    _Value1 = value;
                    OnPropertyChanged();
                    //Super.Log($"[VAL1] {value}");
                }
            }
        }

        private double _Value2 = 1;
        public double Value2
        {
            get
            {
                return _Value2;
            }
            set
            {
                if (_Value2 != value)
                {
                    _Value2 = value;
                    OnPropertyChanged();
                    //Super.Log($"[VAL2] {value}");
                }
            }
        }


        private double _Value3 = 0.05;
        public double Value3
        {
            get
            {
                return _Value3;
            }
            set
            {
                if (_Value3 != value)
                {
                    _Value3 = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _Value4 = 1;
        public double Value4
        {
            get
            {
                return _Value4;
            }
            set
            {
                if (_Value4 != value)
                {
                    _Value4 = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _Value5 = 1;
        public double Value5
        {
            get
            {
                return _Value5;
            }
            set
            {
                if (_Value5 != value)
                {
                    _Value5 = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _Value6 = 1;
        public double Value6
        {
            get
            {
                return _Value6;
            }
            set
            {
                if (_Value6 != value)
                {
                    _Value6 = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _Value7 = 1;
        public double Value7
        {
            get
            {
                return _Value7;
            }
            set
            {
                if (_Value7 != value)
                {
                    _Value7 = value;
                    OnPropertyChanged();
                }
            }
        }


        private bool _ValueSwitch = true;
        public bool ValueSwitch
        {
            get
            {
                return _ValueSwitch;
            }
            set
            {
                if (_ValueSwitch != value)
                {
                    _ValueSwitch = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _IsLooped = true;
        public bool IsLooped
        {
            get
            {
                return _IsLooped;
            }
            set
            {
                if (_IsLooped != value)
                {
                    _IsLooped = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _Bounces = true;
        public bool Bounces
        {
            get
            {
                return _Bounces;
            }
            set
            {
                if (_Bounces != value)
                {
                    _Bounces = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _SwipeSpeed = 1.0;
        public double SwipeSpeed
        {
            get
            {
                return _SwipeSpeed;
            }
            set
            {
                if (_SwipeSpeed != value)
                {
                    _SwipeSpeed = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand CommandToggleLoop { get; set; }
        public ICommand CommandToggleBounces { get; set; }
        public ICommand CommandGoPrev { get; set; }
        public ICommand CommandGoNext { get; set; }
        public ICommand CommandSetSpeed05 { get; set; }
        public ICommand CommandSetSpeed10 { get; set; }
        public ICommand CommandSetSpeed20 { get; set; }

    }
}
