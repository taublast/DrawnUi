using System.Windows.Input;

namespace DrawnUi.Controls;

public class SkiaTabsSelector : SkiaLayout
{
    /// <summary>
    /// This is called when processing stack of index changes.
    /// For example, you might have index changed 5 times
    /// during the time you were executing ApplySelectedIndex (playing the animations etc),
    /// so then you just need the latest index to be applied.
    /// At the same time ApplySelectedIndex will not be called again while its already running,
    /// this way you would visually apply only the latest most actual value instead of
    /// maybe freezing the UI for too many heavy to be rendered changes.
    /// </summary>
    public virtual async Task ApplySelectedIndex(int index)
    {
        await ApplySelectedIndex(false, index);
    }

    public async void ApplySelectedIndexInternal()
    {
        if (_isBusyApplyingIndex)
        {
            _needReApplyiIndex = true;
            return;
        }

        try
        {
            await ApplySelectedIndex(SelectedIndex);
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
        }
        finally
        {
            if (_needReApplyiIndex && !_isBusyApplyingIndex)
            {
                _needReApplyiIndex = false;
                Tasks.StartDelayed(TimeSpan.FromMicroseconds(1), async () =>
                {
                    await Task.Run(ApplySelectedIndexInternal);
                });
            }
        }
    }


    private bool _isBusyApplyingIndex;
    private bool _needReApplyiIndex;


    #region PROPERTIES

    public static readonly BindableProperty TabTypeProperty = BindableProperty.Create(
        nameof(TabType),
        typeof(Type),
        typeof(SkiaTabsSelector),
        typeof(SkiaLabel), propertyChanged: OnNeedRebuild);

    private static void OnNeedRebuild(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaTabsSelector control)
        {
            control.Rebuild();
        }
    }

    /// <summary>
    /// Specify the type of the tab to be included, other types will just render and not be treated as tabs. Using this so we can included any elements inside this control to create any design.
    /// </summary>
    public Type TabType
    {
        get { return (Type)GetValue(TabTypeProperty); }
        set { SetValue(TabTypeProperty, value); }
    }


    public static readonly BindableProperty CommandTabReselectedProperty = BindableProperty.Create(nameof(CommandTabReselected), typeof(ICommand),
        typeof(SkiaTabsSelector), null);

    public ICommand CommandTabReselected
    {
        get { return (ICommand)GetValue(CommandTabReselectedProperty); }
        set { SetValue(CommandTabReselectedProperty, value); }
    }

    public static readonly BindableProperty CommandTabSelectedProperty = BindableProperty.Create(nameof(CommandTabSelected), typeof(ICommand),
        typeof(SkiaTabsSelector), null);

    public ICommand CommandTabSelected
    {
        get { return (ICommand)GetValue(CommandTabSelectedProperty); }
        set { SetValue(CommandTabSelectedProperty, value); }
    }

    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex),
        typeof(int),
        typeof(SkiaTabsSelector),
        defaultValue: -1,
        propertyChanged: SelectedIndexPropertyChanged);

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set
        {
            SetValue(SelectedIndexProperty, value);
        }
    }

    private static void SelectedIndexPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        var tabHostView = (SkiaTabsSelector)bindable;
        int selectedIndex = (int)newvalue;
        if (selectedIndex < 0)
        {
            return;
        }
        tabHostView.LastSelectedIndex = (int)oldvalue;
        tabHostView.ApplySelectedIndexInternal();
    }

    #endregion

    public void Rebuild()
    {
        SelectableTabs.Clear();
        foreach (var child in Views)
        {
            AddTab(child);
        }
    }

    private void AddTab(SkiaControl child)
    {
        if (child.GetType() == TabType)
        {
            SelectableTabs.Add(new TabEntry { VIew = child });
            ApplySelectedIndex(true, SelectedIndex);
        }
    }

    private void RemoveTab(SkiaControl child)
    {
        if (child.GetType() == TabType)
        {
            SelectableTabs.RemoveAll(x => x.VIew == child);
            if (!IsDisposed)
                ApplySelectedIndex(true, SelectedIndex);
        }
    }

    public override void OnDisposing()
    {
        base.OnDisposing();

        SelectableTabs.Clear();
    }

    public override void OnChildAdded(SkiaControl child)
    {
        base.OnChildAdded(child);

        AddTab(child as SkiaControl);
    }

    public override void OnChildRemoved(SkiaControl child)
    {
        base.OnChildRemoved(child);

        RemoveTab(child);
    }

    public class TabEntry
    {
        public SkiaControl VIew { get; set; }
        public bool IsSelected { get; set; }
    };

    protected List<TabEntry> SelectableTabs = new();

    private bool _trackingReselection;
    int _lastTapIndex;


    public int LastSelectedIndex
    {
        get
        {
            return _lastSelectedIndex;
        }

        set
        {
            if (_lastSelectedIndex != value)
            {
                _lastSelectedIndex = value;
                OnPropertyChanged();
            }
        }
    }
    int _lastSelectedIndex = -1;

    public ICommand CommandTappedTab
    {
        get
        {
            return new Command(async (context) =>
            {
                var index = $"{context}".ToInteger();
                _trackingReselection = true;

                if (SelectedIndex == index)
                {
                    OnTabReselected();
                }
                else
                    SelectedIndex = index;
            });
        }
    }


    public virtual async Task ApplySelectedIndex(bool tabsChanged, int selectedIndex)
    {

        //clamp
        if (SelectableTabs.Count == 0)
        {
            selectedIndex = 0;
        }
        if (selectedIndex > SelectableTabs.Count)
        {
            selectedIndex = SelectableTabs.Count - 1;
        }

        //set tabs views visual selection state
        for (int index = 0; index < SelectableTabs.Count; index++)
        {
            SelectableTabs[index].IsSelected = selectedIndex == index;
        }

        //selected and reselected events
        if (_lastTapIndex != selectedIndex || selectedIndex == -1)
        {
            _lastTapIndex = selectedIndex;
            await OnTabSelectionChanged(tabsChanged, selectedIndex);

            if (SelectedIndex >= 0 && !tabsChanged)
            {
                for (int i = 0; i < SelectableTabs.Count; i++)
                {
                    var check = SelectableTabs[i].VIew.Opacity;
                }
            }

        }
        else
        {
            if (_trackingReselection)
            {
                OnTabReselected();
            }
        }
    }

    public virtual async Task OnTabSelectionChanged(bool tabsChanged, int index)
    {
        CommandTabSelected?.Execute(index);
    }

    public virtual void OnTabReselected()
    {
        CommandTabReselected?.Execute(SelectedIndex);
    }
}
