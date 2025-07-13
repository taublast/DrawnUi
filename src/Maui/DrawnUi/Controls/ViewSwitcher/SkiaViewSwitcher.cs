// Deeply inspired by the Sharpnado ViewSwitcher https://github.com/roubachof/Sharpnado.Tabs

using DrawnUi.Infrastructure.Enums;
using Microsoft.Maui.Controls.Shapes;

namespace DrawnUi.Controls
{
    /*
        public interface INavigationTransitionAnimation
        {
            public Task Setup(DoubleViewTransitionType doubleViewTransitionType,
                SkiaControl previousVisibleView, SkiaControl newVisibleView);

            public Task Execute(DoubleViewTransitionType doubleViewTransitionType,
                SkiaControl previousVisibleView, SkiaControl newVisibleView);
        }
        */

    /// <summary>
    /// Display and hide views, eventually animating them
    /// </summary>
    public class SkiaViewSwitcher : SkiaLayout, IDefinesViewport, IVisibilityAware
    {
        public override void ApplyBindingContext()
        {
            if (FillGradient != null)
                FillGradient.BindingContext = BindingContext;

            foreach (var view in this.Views)
            {
                if (view.BindingContext == null)
                    view.SetInheritedBindingContext(BindingContext);
            }
        }

        private NavigationStackEntry _ActiveView;

        public NavigationStackEntry ActiveView
        {
            get { return _ActiveView; }
            set
            {
                if (_ActiveView != value)
                {
                    _ActiveView = value;
                    OnPropertyChanged();
                }
            }
        }

        public SkiaViewSwitcher()
        {
            RowSpacing = 0;
            ColumnSpacing = 0;
        }

        public void SendOnAppearing(SkiaControl view)
        {
            if (view is IVisibilityAware aware)
            {
                aware.OnAppearing();
            }
        }

        public void SendOnAppeared(SkiaControl view)
        {
            if (view is IVisibilityAware aware)
            {
                aware.OnAppeared();
            }
        }

        public void SendOnDisappeared(SkiaControl view)
        {
            if (view is IVisibilityAware aware)
            {
                aware.OnDisappeared();
            }
        }

        public void SendOnLoaded(SkiaControl view)
        {
            if (view is IInsideViewport aware)
            {
                aware.OnLoaded();
            }
        }

        protected override void OnLayoutChanged()
        {
            base.OnLayoutChanged();

            foreach (var view in Views)
            {
                if (view is IInsideViewport viewport)
                {
                    viewport.OnViewportWasChanged(ScaledRect.FromPixels(DrawingRect, RenderingScale));
                }
            }
        }

        protected override void Draw(DrawingContext context)
        {
            IsRendered = true;
            base.Draw(context);
        }

        public bool IsRendered;
        private bool ChildrenInvalidated;

        public static readonly BindableProperty IsAnimatingProperty = BindableProperty.Create(nameof(IsAnimating),
            typeof(bool),
            typeof(SkiaViewSwitcher),
            false, BindingMode.OneWayToSource);

        public bool IsAnimating
        {
            get { return (bool)GetValue(IsAnimatingProperty); }
            set { SetValue(IsAnimatingProperty, value); }
        }

        //public static readonly BindableProperty ClipWithProperty = BindableProperty.Create(nameof(ClipWith), typeof(Geometry), typeof(SkiaViewSwitcher), null);
        //public Geometry ClipWith
        //{
        //    get { return (Geometry)GetValue(ClipWithProperty); }
        //    set { SetValue(ClipWithProperty, value); }
        //}

        #region Tabs

        public override void OnChildAdded(SkiaControl view)
        {
            if (view is IInsideViewport viewport)
            {
                viewport.OnViewportWasChanged(ScaledRect.FromPixels(DrawingRect, RenderingScale));
            }

            base.OnChildAdded(view);

            var previous = Views.Count - 2;
            if (previous >= 0)
                HideView(view as SkiaControl, Views.Count - 1, false);
        }

        public override void SetChildren(IEnumerable<SkiaControl> views)
        {
            base.SetChildren(views);

            UpdateSelectedView(SelectedIndex, false);
        }

        #endregion

        public List<NavigationStackEntry> GetNavigationStack(int index)
        {
            var stack = new List<NavigationStackEntry>();
            try
            {
                if (NavigationStacks.TryGetValue(index, out var getStack))
                {
                    return getStack;
                }

                NavigationStacks[index] = stack;
                return stack;
            }
            catch (Exception e)
            {
                //Trace.WriteLine(e);
                NavigationStacks[index] = stack;
                return stack;
            }
        }

        /// <summary>
        /// Get tab view or tab top subview if has navigation stack
        /// </summary>
        /// <param name="selectedIndex"></param>
        /// <returns></returns>
        public NavigationStackEntry GetTopView(int selectedIndex)
        {
            try
            {
                var stack = GetNavigationStack(selectedIndex);
                var view = stack.LastOrDefault();
                if (view != null)
                    return stack.Last();

                return new NavigationStackEntry(Views[selectedIndex] as SkiaControl, false, false);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }

            return null;
        }

        public NavigationStackEntry GetRootView(int selectedIndex)
        {
            try
            {
                return new NavigationStackEntry(Views[selectedIndex] as SkiaControl, false, false);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }

            return null;
        }

        public async void PushView(SkiaControl view, bool animated, bool preserve = false, int tab = -1)
        {
            if (NavigationBusy)
                return;

            NavigationBusy = true;

            try
            {
                if (view == null)
                {
                    throw new Exception("AttachControl is null");
                }

                if (tab >= 0)
                {
                    SelectedIndex = tab;
                }

                try
                {
                    var index = tab;
                    if (index < 0)
                        index = SelectedIndex;

                    if (index < 0)
                        index = 0;

                    var stack = GetNavigationStack(index);
                    stack.Add(new NavigationStackEntry(view, animated, preserve));

                    if (AnimatePages)
                    {
                        view.IsVisible = false;
                    }

                    AddSubView(view);

                    IsPushing = true; //will be set to false after transition completes

                    UpdateSelectedView(index, animated);
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }
            finally
            {
                NavigationBusy = false;
            }
        }

        public SkiaControl GetCurrentPage(int tab = -1)
        {
            List<NavigationStackEntry> stack;
            try
            {
                if (tab < 0)
                    tab = SelectedIndex;

                stack = GetNavigationStack(tab);
                var subView = stack.LastOrDefault();
                if (subView != null)
                {
                    return subView.View;
                }

                return null;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                //NavigationStacks[SelectedIndex] = stack;
            }

            return null;
        }

        public T FindPageInStack<T>(int tab = -1) where T : SkiaControl
        {
            List<NavigationStackEntry> stack;
            try
            {
                if (tab < 0)
                    tab = SelectedIndex;

                stack = GetNavigationStack(tab);
                if (stack.FirstOrDefault(x => x.View.GetType() == typeof(T)) is T subView)
                {
                    return subView;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                //NavigationStacks[SelectedIndex] = stack;
            }

            return default(T);
        }

        public record NavigationStackEntry(SkiaControl View, bool Animated, bool Preserve);

        public void RemovePageFromStack<T>(int tab = -1) where T : SkiaControl
        {
            List<NavigationStackEntry> stack;
            try
            {
                if (tab < 0)
                    tab = SelectedIndex;

                stack = GetNavigationStack(tab);
                var foundEntry = stack.FirstOrDefault(x => x.View is T);

                if (foundEntry != null)
                {
                    stack.Remove(foundEntry);
                    UpdateSelectedView(tab, AnimatePages);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        public bool BringPageToFront(Type type, int tab = -1)
        {
            List<NavigationStackEntry> stack;
            try
            {
                if (tab < 0)
                    tab = SelectedIndex;

                stack = GetNavigationStack(tab);
                var subView = stack.FirstOrDefault(x => x.GetType() == type);
                if (subView != null)
                {
                    if (stack.IndexOf(subView) != stack.Count - 1)
                    {
                        stack.Remove(subView);
                        stack.Add(subView);
                        //todo make visible
                        UpdateSelectedView(tab, false);
                    }

                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                //NavigationStacks[SelectedIndex] = stack;
            }

            return false;
        }

        public void PopModal(bool animated)
        {
        }

        public async Task PopPage(int tab = -1)
        {
            //animation off

            await SemaphoreNavigationStack.WaitAsync();

            if (tab < 0)
                tab = SelectedIndex;

            List<NavigationStackEntry> stack;
            try
            {
                stack = GetNavigationStack(tab);

                var subView = stack.Last();

                //todo send disappearing

                stack.Remove(subView);

                NavigationStacks[tab] = stack;

                //show view under

                IsPopping = true;

                UpdateSelectedView(tab, AnimatePages);
            }
            catch (Exception e)
            {
                stack = new List<NavigationStackEntry>();
                NavigationStacks[tab] = stack;
            }
            finally
            {
                SemaphoreNavigationStack.Release();
            }
        }

        public int GetCurrentTabNavigationIndex()
        {
            var index = 0;
            List<NavigationStackEntry> stack;
            try
            {
                stack = GetNavigationStack(SelectedIndex);
                index = stack.Count;
            }
            catch (Exception e)
            {
                stack = new List<NavigationStackEntry>();
                NavigationStacks[SelectedIndex] = stack;
            }

            return index;
        }

        /// <summary>
        /// Set IsVisible, reset transforms and opacity and send OnAppeared event
        /// </summary>
        /// <param name="newVisibleView"></param>
        protected virtual void RevealNavigationView(NavigationStackEntry newVisibleView)
        {
            if (newVisibleView != null)
            {
                newVisibleView.View.BlockGesturesBelow = true;
                newVisibleView.View.TranslationX = 0;
                newVisibleView.View.TranslationY = 0;
                newVisibleView.View.Opacity = 1.0;
                ChangeViewVisibility(newVisibleView.View, true);
                SendOnAppeared(newVisibleView.View);
            }
        }

        /// <summary>
        /// Must be launched on main thread only !!!
        /// </summary>
        public async Task PopTabToRoot()
        {
            while (IsApplyingIdex)
            {
                await Task.Delay(10);
            }

            await SemaphoreNavigationStack.WaitAsync();

            List<NavigationStackEntry> stack;
            try
            {
                //show root view, hopefully behind the stack :)
                RevealNavigationView(GetRootView(SelectedIndex));

                stack = GetNavigationStack(SelectedIndex);
                foreach (var subView in stack.ToList())
                {
                    //todo send disappearing
                    SendOnDisappearing(subView.View);
                    RemoveSubView(subView.View);
                    if (!subView.Preserve)
                        subView.View.Dispose();
                }

                stack.Clear();

                if (SelectedIndex < 0)
                {
                    Super.Log("SelectedIndex is -1");
                    return;
                }

                NavigationStacks[SelectedIndex] = stack;
            }
            catch (Exception e)
            {
                stack = new List<NavigationStackEntry>();
                NavigationStacks[SelectedIndex] = stack;
            }
            finally
            {
                SemaphoreNavigationStack.Release();
            }
        }

        public void PopAllTabsToRoot()
        {
            //todo
            PopTabToRoot();
        }

        /// <summary>
        /// for navigation inside pseudo tabs
        /// </summary>
        public Dictionary<int, List<NavigationStackEntry>> NavigationStacks = new();

        public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
            nameof(SelectedIndex),
            typeof(int),
            typeof(SkiaViewSwitcher),
            defaultValue: -1,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: SelectedIndexPropertyChanged);

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        public static readonly BindableProperty DisplayingIndexProperty = BindableProperty.Create(
            nameof(DisplayingIndex),
            typeof(int),
            typeof(SkiaViewSwitcher),
            defaultValue: -1);

        public int DisplayingIndex
        {
            get => (int)GetValue(DisplayingIndexProperty);
            set => SetValue(DisplayingIndexProperty, value);
        }

        public bool AnimatePages { get; set; } = true;
        public bool AnimateTabs { get; set; } = false;

        public void Reset()
        {
            foreach (var key in NavigationStacks.Keys.ToList())
            {
                NavigationStacks.Remove(key);
            }

            ClearChildren();

            PreviousVisibleViewIndex = -1;
            PreviousVisibleView = null;
        }

        public override void OnAppearing()
        {
            base.OnAppearing();

            if (TopView is IVisibilityAware aware)
            {
                aware.OnAppearing();
            }
        }

        public override void OnDisappearing()
        {
            base.OnDisappearing();

            if (TopView is IVisibilityAware aware)
            {
                aware.OnDisappearing();
            }
        }

        public override void OnAppeared()
        {
            base.OnAppeared();

            if (TopView is IVisibilityAware aware)
            {
                aware.OnAppeared();
            }
        }

        private static void SelectedIndexPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaViewSwitcher viewSwitcher)
            {
                viewSwitcher.UpdateSelectedView((int)newvalue, null);
            }
        }

        public bool IsPushing { get; protected set; }
        public bool IsPopping { get; protected set; }

        //-------------------------------------------------------------
        // NavigationBusy
        //-------------------------------------------------------------
        private const string nameNavigationBusy = "NavigationBusy";

        public static readonly BindableProperty NavigationBusyProperty = BindableProperty.Create(nameNavigationBusy,
            typeof(bool), typeof(SkiaViewSwitcher), false, BindingMode.OneWayToSource);

        public bool NavigationBusy
        {
            get { return (bool)GetValue(NavigationBusyProperty); }
            set { SetValue(NavigationBusyProperty, value); }
        }

        public static readonly BindableProperty TabsAnimationSpeedProperty = BindableProperty.Create(
            nameof(TabsAnimationSpeed),
            typeof(int), typeof(SkiaViewSwitcher), 150);

        public int TabsAnimationSpeed
        {
            get { return (int)GetValue(TabsAnimationSpeedProperty); }
            set { SetValue(TabsAnimationSpeedProperty, value); }
        }

        public static readonly BindableProperty PagesAnimationSpeedProperty = BindableProperty.Create(
            nameof(PagesAnimationSpeed),
            typeof(int), typeof(SkiaViewSwitcher), 200);

        public int PagesAnimationSpeed
        {
            get { return (int)GetValue(PagesAnimationSpeedProperty); }
            set { SetValue(PagesAnimationSpeedProperty, value); }
        }

        public static readonly BindableProperty TabsAnimationEasingProperty = BindableProperty.Create(
            nameof(TabsAnimationEasing),
            typeof(Easing), typeof(SkiaViewSwitcher), Custom);

        public Easing TabsAnimationEasing
        {
            get { return (Easing)GetValue(TabsAnimationEasingProperty); }
            set { SetValue(TabsAnimationEasingProperty, value); }
        }

        public static readonly BindableProperty PagesAnimationEasingProperty = BindableProperty.Create(
            nameof(PagesAnimationEasing),
            typeof(Easing), typeof(SkiaViewSwitcher), Easing.Linear);

        public Easing PagesAnimationEasing
        {
            get { return (Easing)GetValue(PagesAnimationEasingProperty); }
            set { SetValue(PagesAnimationEasingProperty, value); }
        }

        public static readonly Easing Custom =
            new Easing((x) => (x - 1) * (x - 1) * ((_sideCoeff + 1) * (x - 1) + _sideCoeff) + 1);

        protected int PreviousVisibleViewIndex = -1;
        public NavigationStackEntry PreviousVisibleView { get; set; }

        protected void LauchTimerOnLock(int seconds, (int index, bool animate) args)
        {
            if (TimerUpdateLocked == null)
            {
                TimerUpdateLocked = new RestartingTimer<(int, bool)>(TimeSpan.FromSeconds(seconds),
                    (args) => { UpdateSelectedView(args.Item1, args.Item2); });
                TimerUpdateLocked.Start(args);
            }
            else
            {
                TimerUpdateLocked.Restart(args);
            }
        }

        protected RestartingTimer<(int, bool)> TimerUpdateLocked;
        public SkiaControl TopView { get; protected set; }

        protected void ChangeViewVisibility(SkiaControl view, bool state)
        {
            if (view.IsVisible != state)
            {
                if (state && view is IInsideViewport viewport)
                {
                    viewport.OnViewportWasChanged(ScaledRect.FromPixels(DrawingRect, RenderingScale));
                }

                view.IsVisible = state;
            }
        }

        private object lockIdex = new();
        private int lastSelectedIndex = -1;

        //protected CancellationTokenSource CancelLastNavigation;
        private readonly LimitedQueue<OrderedIndex> _queue = new();
        private bool _processing = false;

        public void PushIndex(OrderedIndex index)
        {
            //Debug.WriteLine($"[INDEX] Push {index.Index}");

            _queue.Push(index);

            if (!_processing)
            {
                _processing = true;
                ProcessIndexBufferAsync().ConfigureAwait(false);
            }
        }

        protected SemaphoreSlim SemaphoreNavigationStack = new(1, 1);

        public async void DebugAction()
        {
            var newVisibleView = GetTopView(1);
            var previousVisibleView = PreviousVisibleView;

            newVisibleView.View.IsVisible = true;
            newVisibleView.View.Opacity = 1;

            previousVisibleView.View.TranslationX = 150;
            newVisibleView.View.TranslationX = -150;
        }

        public async Task ProcessIndexBufferAsync()
        {
            await SemaphoreNavigationStack.WaitAsync();

            if (IsApplyingIdex)
                return;

            IsApplyingIdex = true;

            try
            {
                while (NavigationBusy)
                {
                    await Task.Delay(20);
                }

                NavigationBusy = true;

                OrderedIndex applyMe = null;
                var index = new OrderedIndex(-1, false);
                while (index != null)
                {
                    try
                    {
                        index = _queue.Pop();

                        if (index != null && index.Index >= 0)
                        {
                            applyMe = index;
                            //await Dispatcher.DispatchAsync(() => ApplySelectedIndex(index));
                            //    .WithCancellation(cancel.Token);
                        }
                    }
                    catch (Exception e)
                    {
                        IsApplyingIdex = false;
                        Trace.WriteLine(e);
                    }
                    finally
                    {
                        IsApplyingIdex = false;
                    }
                }

                if (applyMe != null)
                {
                    await ApplySelectedIndex(applyMe);
                }

                applyMe = null;
            }
            catch (Exception e)
            {
                Super.Log(e);
            }
            finally
            {
                NavigationBusy = false;
                _processing = false;
                SemaphoreNavigationStack.Release();
            }
        }

        //void FadeInFromRight(SKPaint paint, SKRect destination)
        //{
        //	var shader = SKShader.CreateLinearGradient(
        //		new SKPoint(0, 0),
        //		new SKPoint(destination.Width, 0),
        //		new SKColor[] { SKColors.White.WithAlpha((byte)(255 / this._fadeOpacity)), SKColors.White },
        //		null,
        //		SKShaderTileMode.Clamp);

        //	// Apply the shader to the paint object
        //	paint.Shader = shader;
        //}

        //void FadeInFromLeft(SKPaint paint, SKRect destination)
        //{
        //	var shader = SKShader.CreateLinearGradient(
        //		new SKPoint(0, 0),
        //		new SKPoint(destination.Width, 0),
        //		new SKColor[] { SKColors.White, SKColors.White.WithAlpha((byte)(255 / this._fadeOpacity)) },
        //		null,
        //		SKShaderTileMode.Clamp);

        //	// Apply the shader to the paint object
        //	paint.Shader = shader;
        //}

        protected virtual async Task SetupTransitionAnimation(
            DoubleViewTransitionType doubleViewTransitionType,
            SkiaControl previousVisibleView, SkiaControl newVisibleView)
        {
            var translateTo = this.Width;

            switch (doubleViewTransitionType)
            {
                case DoubleViewTransitionType.Pop:
                    //from left to right
                    break;

                case DoubleViewTransitionType.Push:
                    previousVisibleView.ZIndex = -1;
                    newVisibleView.ZIndex = 0;

                    //from right to left
                    translateTo = this.Width;
                    newVisibleView.Opacity = 0.001;
                    newVisibleView.TranslationX = (float)translateTo;
                    break;

                case DoubleViewTransitionType.SelectRightTab:
                    translateTo = this.Width;

                    newVisibleView.ZIndex = 1;
                    previousVisibleView.ZIndex = 0;
                    newVisibleView.Opacity = 0.001;
                    newVisibleView.TranslationX = (float)translateTo * 0.75;
                    break;

                case DoubleViewTransitionType.SelectLeftTab:
                    translateTo = -this.Width;

                    newVisibleView.ZIndex = 1;
                    previousVisibleView.ZIndex = 0;
                    newVisibleView.Opacity = 0.001;
                    newVisibleView.TranslationX = (float)translateTo * 0.75;
                    break;

                default:
                    newVisibleView.TranslationX = 0;
                    newVisibleView.TranslationY = 0;
                    newVisibleView.Opacity = 1;
                    break;
            }
        }

        protected virtual async Task ExecuteTransitionAnimation(
            DoubleViewTransitionType doubleViewTransitionType,
            SkiaControl? previousVisibleView, SkiaControl newVisibleView)
        {
            int speed = TabsAnimationSpeed;
            Easing easing = TabsAnimationEasing;
            var translateTo = this.Width;

            var cancelAnimation = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            switch (doubleViewTransitionType)
            {
                case DoubleViewTransitionType.Pop:
                    //from left to right
                    easing = PagesAnimationEasing;
                    speed = PagesAnimationSpeed;

                    if (previousVisibleView != null)
                    {
                        Task animateOld1 = previousVisibleView.TranslateToAsync(translateTo, 0, speed, easing);
                        Task animateOld2 = previousVisibleView.FadeToAsync(0.9, speed, easing);
                        try
                        {
                            await Task.WhenAll(animateOld1, animateOld2).WithCancellation(cancelAnimation.Token);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                        }
                    }

                    break;

                case DoubleViewTransitionType.Push:
                    easing = PagesAnimationEasing;
                    speed = PagesAnimationSpeed;
                    //from right to left
                    Task in1 = newVisibleView.TranslateToAsync(0, 0, speed, easing);
                    Task in2 = newVisibleView.FadeToAsync(1.0, speed, easing);
                    try
                    {
                        await Task.WhenAll(in1, in2).WithCancellation(cancelAnimation.Token)
                            .WithCancellation(cancelAnimation.Token);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                    }

                    break;

                case DoubleViewTransitionType.SelectLeftTab:
                    translateTo = -this.Width;

                    Task animateNewM = newVisibleView.TranslateToAsync(0, 0, (uint)speed, easing);
                    Task animateNewM1 = newVisibleView.FadeToAsync(1.0, (uint)speed, Easing.Linear);

                    if (previousVisibleView != null)
                    {
                        Task animateOldM = previousVisibleView.TranslateToAsync(-translateTo, 0, (uint)speed, easing);
                        try
                        {
                            await Task.WhenAll(animateOldM, animateNewM, animateNewM1)
                                .WithCancellation(cancelAnimation.Token);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                        }
                    }
                    else
                    {
                        try
                        {
                            await Task.WhenAll(animateNewM, animateNewM1).WithCancellation(cancelAnimation.Token);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                        }
                    }

                    break;

                case DoubleViewTransitionType.SelectRightTab:
                    translateTo = this.Width;

                    animateNewM = newVisibleView.TranslateToAsync(0, 0, (uint)speed, easing);
                    animateNewM1 = newVisibleView.FadeToAsync(1.0, (uint)speed, Easing.Linear);

                    if (previousVisibleView != null)
                    {
                        Task animateOldM = previousVisibleView.TranslateToAsync(-translateTo, 0, (uint)speed, easing);
                        try
                        {
                            await Task.WhenAll(animateOldM, animateNewM, animateNewM1)
                                .WithCancellation(cancelAnimation.Token);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                        }
                    }
                    else
                    {
                        try
                        {
                            await Task.WhenAll(animateNewM, animateNewM1).WithCancellation(cancelAnimation.Token);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                        }
                    }

                    break;
            }
        }

        protected bool FirstViewAppeared;
        protected bool IsApplyingIdex;

        protected virtual async Task ApplySelectedIndex(OrderedIndex index)
        {
            try
            {
                var selectedIndex = index.Index;

                DisplayingIndex = selectedIndex;

                if (selectedIndex < 0 || Superview == null)
                {
                    return;
                }

                //TouchEffect.CloseKeyboard(); todo maybe add ???

                Superview.FocusedChild = null;

                lastSelectedIndex = selectedIndex;

                var newVisibleView = GetTopView(selectedIndex);

                if (newVisibleView == null || TopView == newVisibleView.View)
                {
                    //seems we have not created view yet
                    return;
                }

                bool shown = false;
                bool isAnimating = false;

                NavigationStackEntry? previousVisibleView = PreviousVisibleView;
                TopView = newVisibleView.View;

                void OnViewLoaded(SkiaControl loadedView)
                {
                    if (IsRendered || !FirstViewAppeared)
                    {
                        FirstViewAppeared = true;
                        SendOnAppearing(loadedView);
                    }
                }

                if (previousVisibleView?.View != newVisibleView.View)
                {
                    void Reveal()
                    {
                        RevealNavigationView(newVisibleView);

                        if (previousVisibleView != null)
                        {
                            ChangeViewVisibility(previousVisibleView.View, false);
                            SendOnDisappeared(previousVisibleView.View);
                        }
                    }

                    //Show view without animation. For first view etc
                    void SetNewVisibleViewAsOneVisible()
                    {
                        OnViewLoaded(newVisibleView.View);
                        ActiveView = newVisibleView;
                        Reveal();
                    }

                    bool needAnimate = index.Animate.GetValueOrDefault();
                    if (IsPushing || IsPopping)
                    {
                        if (index.Animate == null)
                            needAnimate = AnimatePages;
                    }
                    else
                    {
                        if (index.Animate == null)
                            needAnimate = AnimateTabs;
                    }

                    if (previousVisibleView != null && !previousVisibleView.View.CanDraw)
                    {
                        previousVisibleView = null;
                    }

                    if (needAnimate)
                    {
                        if (needAnimate && //base flag
                            ((IsPushing || IsPopping) // navigating
                             || (PreviousVisibleViewIndex != selectedIndex &&
                                 PreviousVisibleViewIndex > -1))) //switching tabs
                        {
                            isAnimating = true;

                            try
                            {
                                var transitionType = DoubleViewTransitionType.Push;
                                if (IsPopping)
                                    transitionType = DoubleViewTransitionType.Pop;
                                else if (!IsPushing)
                                {
                                    if (selectedIndex > PreviousVisibleViewIndex)
                                    {
                                        transitionType = DoubleViewTransitionType.SelectRightTab;
                                    }
                                    else
                                    {
                                        transitionType = DoubleViewTransitionType.SelectLeftTab;
                                    }
                                }

                                //prepare
                                if (previousVisibleView != null)
                                {
                                    await SetupTransitionAnimation(transitionType, previousVisibleView.View,
                                        newVisibleView.View);
                                }

                                ChangeViewVisibility(newVisibleView.View, true);
                                SendOnLoaded(newVisibleView.View);

                                //animate
                                var previousView = previousVisibleView != null ? previousVisibleView.View : null;
                                await ExecuteTransitionAnimation(transitionType, previousView, newVisibleView.View);

                                SetNewVisibleViewAsOneVisible();

                                if (IsPopping)
                                {
                                    RemoveSubView(previousVisibleView.View);
                                    PreviousVisibleView = null;
                                }

                                UnloadView(previousVisibleView);
                            }
                            catch (Exception e)
                            {
                                Trace.WriteLine(e);
                            }
                            finally
                            {
                                RevealNavigationView(newVisibleView);

                                if (previousVisibleView != null)
                                {
                                    ChangeViewVisibility(previousVisibleView.View, false);
                                }

                                PreviousVisibleViewIndex = selectedIndex;
                                PreviousVisibleView = newVisibleView;

                                IsPushing = false;
                                IsPopping = false;
                            }
                        }
                        else
                        {
                            SetNewVisibleViewAsOneVisible();
                        }

                        foreach (var view in Views)
                        {
                            if (isAnimating && view == previousVisibleView?.View)
                                continue;

                            if (view == newVisibleView?.View)
                                continue;

                            UnloadView(new NavigationStackEntry(view, false, false));
                        }
                    }
                    else
                    {
                        //no previous view, just show top view
                        SetNewVisibleViewAsOneVisible();
                    }


                    PreviousVisibleView = newVisibleView;
                    PreviousVisibleViewIndex = selectedIndex;
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }
            finally
            {
                Invalidate();

                NavigationBusy = false; //can be still animating!

            }
        }

        private async void UpdateSelectedView(int selectedIndex, bool? animate = null)
        {
            if (selectedIndex >= 0)
            {
                PushIndex(new OrderedIndex(selectedIndex, animate));
            }
        }

        public override void OnDisposing()
        {
            base.OnDisposing();

            BusyChanged = null;
            LoadedLazyView = null;
        }

        public event EventHandler<bool> BusyChanged;
        public event EventHandler<SkiaControl> LoadedLazyView;
        private static readonly double _sideCoeff = 0.55;

        private readonly Easing _easing =
            new Easing((x) => (x - 1) * (x - 1) * ((_sideCoeff + 1) * (x - 1) + _sideCoeff) + 1);

        public void SendOnDisappearing(SkiaControl view)
        {
            if (view is IVisibilityAware aware)
            {
                aware.OnDisappearing();
            }
        }

        protected void UnloadView(NavigationStackEntry view)
        {
            if (view == null)
                return;

            ChangeViewVisibility(view.View, false);

            if (ActiveView == view)
            {
                ActiveView = null;
            }

            if (IsPopping)
            {
                SendOnDisappearing(view.View);
                if (!view.Preserve && view.View is IDisposable disposable)
                {
                    DisposeObject(disposable);
                }
            }
            else
            {
                Task.Run(() => { SendOnDisappearing(view.View); }).ConfigureAwait(false);
            }
        }

        private void HideView(SkiaControl view, int viewIndex, bool animate)
        {
            ChangeViewVisibility(view, false);
        }

        #region IDefinesViewport

        public ScaledRect Viewport { get; }

        public void UpdateVisibleIndex()
        {
        }

        public RelativePositionType TrackIndexPosition { get; }

        public void ScrollTo(float x, float y, float maxTimeSecs)
        {
        }

        #endregion
    }
}
