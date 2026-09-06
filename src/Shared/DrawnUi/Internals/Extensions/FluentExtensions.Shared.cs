using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using DrawnUi.Controls;
using static System.Net.Mime.MediaTypeNames;

namespace DrawnUi.Draw
{
    /// <summary>
    /// Provides extension methods for fluent API design pattern with DrawnUI controls
    /// </summary>
    public static partial class FluentExtensions
    {
        #region LOGIC

        /// <summary>
        /// Assigns the control to a variable and returns the control to continue the fluent chain
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="control">The control to assign</param>
        /// <param name="variable">The out variable to store the reference</param>
        /// <returns>The control for chaining</returns>
        public static T Assign<T>(this T control, out T variable) where T : BindableObject
        {
            variable = control;
            return control;
        }

        /// <summary>
        /// Assigns the control to a parent and returns the control for fluent chaining.
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="control">The control to assign</param>
        /// <param name="parent">The parent control to add to</param>
        /// <returns>The control for chaining</returns>
        public static T AssignParent<T>(this T control, SkiaControl parent) where T : SkiaControl
        {
            parent.AddSubView(control);
            return control;
        }

        /// <summary>
        /// Performs an action on the control and returns it to continue the fluent chain
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to act upon</param>
        /// <param name="action">The action to perform</param>
        /// <returns>The control for chaining</returns>
        public static T Adapt<T>(this T view, Action<T> action) where T : SkiaControl
        {
            try
            {
                action?.Invoke(view);
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            return view;
        }

        /// <summary>
        /// Attaches a gesture handler to a SkiaLayout, allowing custom gesture processing.
        /// You must return this control if you consumed a gesture, return null if not.
        /// The UP gesture should be marked as consumed ONLY for specific scenarios, return null for it if unsure.
        /// </summary>
        /// <typeparam name="T">Type of SkiaLayout</typeparam>
        /// <param name="view">The layout to attach gestures to</param>
        /// <param name="func">A function that returns a gesture listener for the layout</param>
        /// <returns>The layout for chaining</returns>
        public static T WithAccessibility<T>(this T control,
            string role,
            string? label = null,
            string? hint = null,
            bool canInteract = false) where T : SkiaControl
        {
            control.AccessibilityRole        = role;
            control.AccessibilityLabel       = label;
            control.AccessibilityHint        = hint;
            control.AccessibilityCanInteract = canInteract;
            return control;
        }

        public static T WithAccessibility<T>(this T control,
            string role,
            string? label = null,
            bool canInteract = false) where T : SkiaControl
        {
            return control.WithAccessibility(role, label, label, canInteract);
        }

        public static T WithAccessibilityButton<T>(this T control,
            string label,
            string? hint = null) where T : SkiaControl
        {
            return control.WithAccessibility(Aria.RoleButton, label, hint, true);
        }

        public static T WithAccessibilityButton<T>(this T control) where T : SkiaButton
        {
            return control.WithAccessibility(Aria.RoleButton, control.Text, control.Text, true);
        }

        public static T WithAccessibilityButton<T>(this T control,
            string label) where T : SkiaControl
        {
            return control.WithAccessibility(Aria.RoleButton, label, label, true);
        }

        /// <summary>
        /// Marks a control as a live region so Narrator announces its label whenever it changes.
        /// Call after <c>.WithAccessibility(role, ...)</c> or <c>.WithAccessibilityText()</c>.
        /// <paramref name="live"/>: <see cref="Aria.LivePolite"/> (default) or <see cref="Aria.LiveAssertive"/>.
        /// </summary>
        public static T WithAccessibilityLive<T>(this T control, string live = "polite") where T : SkiaControl
        {
            control.AccessibilityLive = live;
            return control;
        }

        public static T WithAccessibilityText<T>(this T control, string text) where T : SkiaControl
        {
            control.AccessibilityRole = Aria.RoleText;
            control.AccessibilityLabel = text;
            return control;
        }

        public static T WithAccessibilityText<T>(this T control) where T : SkiaLabel
        {
            control.AccessibilityRole = Aria.RoleText;
            control.AccessibilityLabel = control.Text;
            return control;
        }

        /// <summary>Sets aria-pressed for a toggle button. null = not a toggle (attribute absent).</summary>
        public static T WithAccessibilityPressed<T>(this T control, bool? pressed) where T : SkiaControl
        {
            control.AccessibilityIsPressed = pressed;
            return control;
        }

        /// <summary>
        /// Marks a SkiaToggle as an accessible toggle button and keeps aria-pressed in sync with IsToggled automatically.
        /// Unsubscribes on control disposal via ExecuteUponDisposal.
        /// </summary>
        public static T WithAccessibilityToggle<T>(this T control, string label, string? hint = null) where T : SkiaToggle
        {
            control.AccessibilityRole = Aria.RoleSwitch;
            control.AccessibilityLabel = label;
            control.AccessibilityHint = hint;
            control.AccessibilityCanInteract = true;
            control.AccessibilityIsPressed = control.IsToggled;
            control.ObserveProperty(control, nameof(SkiaToggle.IsToggled), me => me.AccessibilityIsPressed = me.IsToggled);
            return control;
        }

        public static T WithGestures<T>(this T view, Func<T, SkiaGesturesParameters, GestureEventProcessingInfo, ISkiaGestureListener> func) where T : SkiaLayout
        {
            view.OnGestures = (a, b) =>
            {
                return func(view, a, b);
            };
            return view;
        }

        /// <summary>
        /// Registers a callback to be executed after the control is added to the view tree and initialized.
        /// Use for setup that requires the control to be part of the visual tree.
        /// This is called after the control default content was created and all variables have been assigned inside the fluent chain.
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to initialize</param>
        /// <param name="action">Initialization logic to run</param>
        /// <returns>The control for chaining</returns>
        public static T Initialize<T>(this T view, Action<T> action) where T : SkiaControl
        {
            view.ExecuteAfterCreated[Guid.NewGuid().ToString()] = control => { action.Invoke((T)control); };
            return view;
        }

        /// <summary>
        /// Registers a callback to be executed during the paint phase of the control's rendering.
        /// Called inside the base.Paint(..).
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to attach paint logic to</param>
        /// <param name="action">Paint logic to run</param>
        /// <returns>The control for chaining</returns>
        public static T WhenPaint<T>(this T view, Action<T, DrawingContext> action) where T : SkiaControl
        {
            view.ExecuteOnPaint[Guid.NewGuid().ToString()] = (control, ctx) => { action.Invoke((T)control, ctx); };
            return view;
        }

        /// <summary>
        /// Attaches a post-effects overlay drawn ABOVE the control's content and children,
        /// at the same stage where the ripple renders (after-drawing overlay pass) — unlike
        /// <c>WhenPaint</c>, which runs inside base.Paint BEFORE children. The callback
        /// receives the drawing context and the control; return true to request continuous
        /// repaint (animated overlay), false for a static overlay. Never mutate layout
        /// properties inside — draw on the canvas only.
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to attach the overlay to</param>
        /// <param name="render">Overlay draw callback</param>
        /// <returns>The control for chaining</returns>
        public static T WhenPainted<T>(this T view, Func<DrawingContext, IDrawnBase, bool> render) where T : SkiaControl
        {
            view.PostAnimators.Add(new ActionOverlayEffect(view, render));
            return view;
        }

        #region ANIMATION

        /// <summary>
        /// Starts a frame-rate independent animation driven by the framework animator
        /// (which ticks off FrameTimeNanos), so it runs at the same visual speed on any
        /// device regardless of FPS. Every frame the callback receives: the control, the
        /// animator (call <c>animator.Stop()</c> to end it early), the eased progress of
        /// the current cycle (0..1) and the seconds elapsed since the previous frame
        /// (delta time). The animator auto-unregisters when the control is disposed.
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl.</typeparam>
        /// <param name="control">Control to animate.</param>
        /// <param name="seconds">Duration of one cycle, in seconds.</param>
        /// <param name="onFrame">(control, animator, value 0..1, deltaSeconds) — invoked each frame.</param>
        /// <param name="repeat">-1 = loop forever, N = N extra cycles, 0 = play once.</param>
        /// <param name="easing">Value curve; null = linear.</param>
        /// <param name="pingPong">If true the value bounces 0→1→0 each cycle instead of restarting at 0.</param>
        /// <param name="delaySeconds">Initial delay before starting, in seconds.</param>
        /// <returns>The control for chaining.</returns>
        public static T Animate<T>(this T control,
            double seconds,
            Action<T, SkiaValueAnimator, double, double> onFrame,
            int repeat = 0,
            Easing easing = null,
            bool pingPong = false,
            double delaySeconds = 0) where T : SkiaControl
        {
            if (onFrame == null)
                return control;

            RangeAnimator animator = pingPong ? new PingPongAnimator(control) : new RangeAnimator(control);
            animator.Repeat = repeat;

            long previousNanos = 0;

            void Report(double value)
            {
                // LastFrameTimeNanos is already advanced to the current frame before this
                // callback fires, so the delta is (now - previous). Clamped to swallow
                // stalls and the one-frame blip at a loop boundary.
                var nowNanos = animator.LastFrameTimeNanos;
                double delta = 0;
                if (previousNanos != 0 && nowNanos > previousNanos)
                {
                    delta = (nowNanos - previousNanos) / 1_000_000_000.0;
                    if (delta > 0.1) delta = 0.1;
                }
                previousNanos = nowNanos;
                onFrame(control, animator, value, delta);
            }

            void StartNow()
            {
                previousNanos = 0;
                animator.Start(Report, 0, 1, (uint)(seconds * 1000), easing, (int)(delaySeconds * 1000));
            }

            if (control.IsLayoutReady)
            {
                StartNow();
            }
            else
            {
                // An animator can only tick once the control is in the visual tree, so
                // defer the start until the control has laid out.
                void OnReady(object sender, EventArgs e)
                {
                    control.LayoutIsReady -= OnReady;
                    control.ExecuteUponDisposal.Remove($"Animate_{animator.Uid}");
                    StartNow();
                }
                control.LayoutIsReady += OnReady;
                // Drop the handler if the control is disposed before it ever lays out.
                control.ExecuteUponDisposal[$"Animate_{animator.Uid}"] = () => control.LayoutIsReady -= OnReady;
            }

            return control;
        }

        /// <summary>Animates <see cref="SkiaControl.Rotation"/> from <paramref name="from"/> to <paramref name="to"/> degrees. See <see cref="Animate{T}"/> for the shared parameters.</summary>
        public static T AnimateRotation<T>(this T control, double from, double to, double seconds,
            int repeat = 0, Easing easing = null, bool pingPong = false, double delaySeconds = 0) where T : SkiaControl
            => control.Animate(seconds, (me, _, v, _) => me.Rotation = from + (to - from) * v, repeat, easing, pingPong, delaySeconds);

        /// <summary>Animates uniform <see cref="SkiaControl.Scale"/>. See <see cref="Animate{T}"/> for the shared parameters.</summary>
        public static T AnimateScale<T>(this T control, double from, double to, double seconds,
            int repeat = 0, Easing easing = null, bool pingPong = false, double delaySeconds = 0) where T : SkiaControl
            => control.Animate(seconds, (me, _, v, _) => me.Scale = from + (to - from) * v, repeat, easing, pingPong, delaySeconds);

        /// <summary>Animates <see cref="SkiaControl.Opacity"/>. See <see cref="Animate{T}"/> for the shared parameters.</summary>
        public static T AnimateOpacity<T>(this T control, double from, double to, double seconds,
            int repeat = 0, Easing easing = null, bool pingPong = false, double delaySeconds = 0) where T : SkiaControl
            => control.Animate(seconds, (me, _, v, _) => me.Opacity = from + (to - from) * v, repeat, easing, pingPong, delaySeconds);

        /// <summary>Animates <see cref="SkiaControl.TranslationX"/>. See <see cref="Animate{T}"/> for the shared parameters.</summary>
        public static T AnimateTranslationX<T>(this T control, double from, double to, double seconds,
            int repeat = 0, Easing easing = null, bool pingPong = false, double delaySeconds = 0) where T : SkiaControl
            => control.Animate(seconds, (me, _, v, _) => me.TranslationX = from + (to - from) * v, repeat, easing, pingPong, delaySeconds);

        /// <summary>Animates <see cref="SkiaControl.TranslationY"/>. See <see cref="Animate{T}"/> for the shared parameters.</summary>
        public static T AnimateTranslationY<T>(this T control, double from, double to, double seconds,
            int repeat = 0, Easing easing = null, bool pingPong = false, double delaySeconds = 0) where T : SkiaControl
            => control.Animate(seconds, (me, _, v, _) => me.TranslationY = from + (to - from) * v, repeat, easing, pingPong, delaySeconds);

        /// <summary>
        /// Keeps the control's surface repainting every frame forever, without changing any
        /// property. Needed for visuals that advance on their own each frame from outside the
        /// property system — e.g. a time-driven <c>SkiaShaderEffect</c> (reads <c>iTime</c>) or a
        /// clock drawn in <c>WhenPaint</c>. Without it the control draws once and freezes, since
        /// nothing invalidates it. Implemented as an infinite no-op animator: a running animator
        /// keeps the render loop alive, so the shader/paint re-runs with a fresh frame time.
        /// Auto-stops when the control is disposed. Use only for genuinely animated visuals —
        /// it holds the canvas at full frame rate.
        /// </summary>
        public static T UpdateNonStop<T>(this T control) where T : SkiaControl
            => control.Animate(1.0, (_, _, _, _) => { }, repeat: -1);

        #endregion

        #region KEYBOARD

        /// <summary>
        /// Subscribes to global keyboard key-down while this control is alive. The handler
        /// receives (control, key). Auto-unsubscribes when the control is disposed, so it is
        /// safe to chain at construction. Keyboard events flow whenever the DrawnUI canvas has
        /// focus (Blazor/Wasm/OpenTK). Handy for keyboard-driven games and shortcuts.
        /// </summary>
        public static T OnKeyDown<T>(this T control, Action<T, InputKey> action) where T : SkiaControl
        {
            if (action == null)
                return control;

            void Handler(object sender, InputKey key) => action(control, key);
            KeyboardManager.KeyDown += Handler;
            control.ExecuteUponDisposal[$"OnKeyDown_{Guid.NewGuid()}"] = () => KeyboardManager.KeyDown -= Handler;
            return control;
        }

        /// <summary>
        /// Subscribes to global keyboard key-up while this control is alive. See <see cref="OnKeyDown{T}"/>.
        /// </summary>
        public static T OnKeyUp<T>(this T control, Action<T, InputKey> action) where T : SkiaControl
        {
            if (action == null)
                return control;

            void Handler(object sender, InputKey key) => action(control, key);
            KeyboardManager.KeyUp += Handler;
            control.ExecuteUponDisposal[$"OnKeyUp_{Guid.NewGuid()}"] = () => KeyboardManager.KeyUp -= Handler;
            return control;
        }

        #endregion

        /// <summary>
        /// Fluent subscription to <see cref="SkiaShaderEffect.OnCompilationError"/>: the callback
        /// receives the effect and the SkSL compiler error text whenever <c>ShaderCode</c> /
        /// <c>ShaderSource</c> fails to compile. Without any handler a failed compile throws
        /// (swallowed into a log) — with this wired you can surface the error in your UI or console.
        /// Chainable.
        /// </summary>
        public static T OnShaderError<T>(this T effect, Action<T, string> callback) where T : SkiaShaderEffect
        {
            if (callback != null)
            {
                effect.OnCompilationError += (s, error) => callback((T)s, error);
            }
            return effect;
        }

        /// <summary>
        /// Registers a callback to be executed when the control's BindingContext was set/changed.
        /// Called inside base.ApplyBindingContext().
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="control">The control to observe</param>
        /// <param name="callback">Callback to execute when BindingContext is set</param>
        /// <param name="propertyFilter">Optional property filter</param>
        /// <returns>The control for chaining</returns>
        public static T WhenBindingContextSet<T>(
            this T control,
            Action<T, object> callback,
            string[] propertyFilter = null)
            where T : SkiaControl
        {
            void thisHandler(object? sender, EventArgs args)
            {
                control.ApplyingBindingContext -= thisHandler;
                callback?.Invoke(control, control.BindingContext);
            }

            control.ApplyingBindingContext += thisHandler;

            string subscriptionKey = $"ACTX_{control.GetHashCode()}_{Guid.NewGuid()}";
            control.ExecuteUponDisposal[subscriptionKey] = () => { control.ApplyingBindingContext -= thisHandler; };

            return control;
        }

        /// <summary>
        /// Triggers after a new ItemsSource was set or an observable collection of an existing one was changed for a SkiaLayout
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="control"></param>
        /// <param name="callback"></param>
        /// <param name="propertyFilter"></param>
        /// <returns></returns>
        public static T WhenItemsSourceChangesApplied<T>(
            this T control,
            Action<T> callback,
            string[] propertyFilter = null)
            where T : SkiaLayout
        {
            void handler(object? sender, EventArgs args)
            {
                callback?.Invoke(control);
            }

            control.ItemsSourceChangesApplied += handler;

            string subscriptionKey = $"WSCA_{control.GetHashCode()}_{Guid.NewGuid()}";
            control.ExecuteUponDisposal[subscriptionKey] = () => { control.ItemsSourceChangesApplied -= handler; };

            return control;
        }

        /// <summary>
        /// Subscribes to PropertyChanged of this control, will unsubscribe upon control disposal.
        /// </summary>
        /// <typeparam name="T">Type of the target control (the one being extended)</typeparam>
        /// <typeparam name="TSource">Type of the source control (the one being observed)</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="callback">Callback that receives the property name when changed</param>
        /// <param name="propertyFilter">Optional filter to only trigger on specific properties</param>
        /// <returns>The target control for chaining</returns>
        public static T ObserveSelf<T>(this T view, Action<T, string> action) where T : SkiaControl
        {
            return view.Observe(view, action);
        }

        /// <summary>
        /// Subscribes to property changes on a source control and executes a callback when they occur.
        /// Will unsubscribe upon control disposal.
        /// </summary>
        /// <typeparam name="T">Type of the target control (the one being extended)</typeparam>
        /// <typeparam name="TSource">Type of the source control (the one being observed)</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="target">The control being observed</param>
        /// <param name="callback">Callback that receives the property name when changed</param>
        /// <param name="propertyFilter">Optional filter to only trigger on specific properties</param>
        /// <returns>The target control for chaining</returns>
        public static T Observe<T, TSource>(
            this T control,
            TSource target,
            Action<T, string> callback,
            string[] propertyFilter = null)
            where T : SkiaControl
            where TSource : INotifyPropertyChanged
        {
            // Create a unique key for this subscription
            string subscriptionKey = $"Subscribe_{target.GetHashCode()}_{Guid.NewGuid()}";
            var filterSet = propertyFilter != null ? new HashSet<string>(propertyFilter) : null;

            // Create the handler
            PropertyChangedEventHandler handler = (sender, args) =>
            {
                // If a filter is specified, only proceed if the property is in the filter
                if (filterSet != null && !filterSet.Contains(args.PropertyName))
                    return;

                try
                {
                    callback(control, args.PropertyName);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Subscribe: Error in ViewModel property changed callback: {ex.Message}");
                }
            };

            // Subscribe to the event
            target.PropertyChanged += handler;

            //initial
            handler?.Invoke(target, new PropertyChangedEventArgs("BindingContext"));

            // Will unsubscrbe when control is disposed 
            control.ExecuteUponDisposal[subscriptionKey] = () => { target.PropertyChanged -= handler; };

            return control;
        }

        /// <summary>
        /// Observes specific properties on a dynamically resolved target object using a function selector.
        /// When the parent's properties change, re-evaluates the selector and automatically 
        /// unsubscribes from old target and subscribes to new one.
        /// You can omit BindingContext as it will be added at all times.
        /// </summary>
        /// <typeparam name="T">Type of the control being extended</typeparam>
        /// <typeparam name="TParent">Type of the parent object that contains the dynamic property</typeparam>
        /// <typeparam name="TTarget">Type of the target object to observe</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="parent">The parent object that contains the dynamic property</param>
        /// <param name="targetSelector">Function that selects the target object (e.g., () => CurrentTimer)</param>
        /// <param name="parentPropertyName">Name of the property on parent that affects the target selector</param>
        /// <param name="propertyNames">Names of the properties to observe on the target</param>
        /// <param name="callback">Callback that receives the control when target's specified properties change</param>
        /// <returns>The control for chaining</returns>
        public static T ObservePropertiesOn<T, TParent, TTarget>(
            this T control,
            TParent parent,
            Func<TTarget> targetSelector,
            string parentPropertyName,
            IEnumerable<string> propertyNames,
            Action<T> callback)
            where T : SkiaControl
            where TParent : INotifyPropertyChanged
            where TTarget : class, INotifyPropertyChanged
        {
            // Add BindingContext to the property names if not already included
            var props = propertyNames.Concat(new[] { nameof(BindableObject.BindingContext) }).ToArray();

            // Use ObserveOn with property filter and modified callback
            return control.ObserveOn(
                parent,
                targetSelector,
                parentPropertyName,
                (me, prop) =>
                {
                    callback?.Invoke(me);
                },
                props);
        }

        /// <summary>
        /// Observes a specific property on a dynamically resolved target object using a function selector.
        /// When the parent's properties change, re-evaluates the selector and automatically 
        /// unsubscribes from old target and subscribes to new one.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TParent"></typeparam>
        /// <typeparam name="TTarget"></typeparam>
        /// <param name="control"></param>
        /// <param name="parent"></param>
        /// <param name="targetSelector"></param>
        /// <param name="parentPropertyName"></param>
        /// <param name="propertyName"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public static T ObservePropertyOn<T, TParent, TTarget>(
            this T control,
            TParent parent,
            Func<TTarget> targetSelector,
            string parentPropertyName,
            string propertyName,
            Action<T> callback)
            where T : SkiaControl
            where TParent : INotifyPropertyChanged
            where TTarget : class, INotifyPropertyChanged
        {
            // Add BindingContext to the property names if not already included
            var props =new []{ propertyName, nameof(BindableObject.BindingContext) };

            // Use ObserveOn with property filter and modified callback
            return control.ObserveOn(
                parent,
                targetSelector,
                parentPropertyName,
                (me, prop) =>
                {
                    callback?.Invoke(me);
                },
                props);
        }

        /// <summary>
        /// Observes a dynamically resolved target object using a function selector.
        /// When the parent's properties change, re-evaluates the selector and automatically 
        /// unsubscribes from old target and subscribes to new one.
        /// AOT-compatible.
        /// </summary>
        /// <typeparam name="T">Type of the control being extended</typeparam>
        /// <typeparam name="TParent">Type of the parent object that contains the dynamic property</typeparam>
        /// <typeparam name="TTarget">Type of the target object to observe</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="parent">The parent object that contains the dynamic property</param>
        /// <param name="targetSelector">Function that selects the target object (e.g., () => CurrentTimer)</param>
        /// <param name="parentPropertyName">Name of the property on parent that affects the target selector</param>
        /// <param name="callback">Callback that receives the control and property name when target's properties change</param>
        /// <param name="propertyFilter">Optional filter to only trigger on specific properties</param>
        /// <returns>The control for chaining</returns>
        public static T ObserveOn<T, TParent, TTarget>(
            this T control,
            TParent parent,
            Func<TTarget> targetSelector,
            string parentPropertyName,
            Action<T, string> callback,
            string[] propertyFilter = null)
            where T : SkiaControl
            where TParent : INotifyPropertyChanged
            where TTarget : class, INotifyPropertyChanged
        {
            // Track current subscription for cleanup
            string mainKey = $"ObserveDynamic_{parentPropertyName}_{Guid.NewGuid()}";
            TTarget currentTarget = null;
            PropertyChangedEventHandler currentTargetHandler = null;
            var filterSet = propertyFilter != null ? new HashSet<string>(propertyFilter) : null;

            // Helper to clean up current target subscription
            void CleanupCurrentTarget()
            {
                if (currentTarget != null && currentTargetHandler != null)
                {
                    currentTarget.PropertyChanged -= currentTargetHandler;
                    currentTarget = null;
                    currentTargetHandler = null;
                }
            }

            // Helper to setup subscription to a new target
            void SubscribeToTarget(TTarget target)
            {
                // Clean up previous subscription
                CleanupCurrentTarget();

                if (target == null) return;

                currentTarget = target;
                currentTargetHandler = (sender, args) =>
                {
                    // If a filter is specified, only proceed if the property is in the filter
                    if (filterSet != null && !filterSet.Contains(args.PropertyName))
                        return;

                    try
                    {
                        callback(control, args.PropertyName);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ObserveDynamic: Error in target property changed callback: {ex.Message}");
                    }
                };

                target.PropertyChanged += currentTargetHandler;

                // Initial call with BindingContext to trigger initial update
                currentTargetHandler(target, new PropertyChangedEventArgs("BindingContext"));
            }

            // Set up subscription to parent for property changes
            PropertyChangedEventHandler parentHandler = (sender, args) =>
            {
                if (args.PropertyName == parentPropertyName || string.IsNullOrEmpty(args.PropertyName))
                {
                    try
                    {
                        TTarget newTarget = targetSelector();
                        SubscribeToTarget(newTarget);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ObserveDynamic: Error getting new target: {ex.Message}");
                    }
                }
            };

            // Subscribe to parent changes
            parent.PropertyChanged += parentHandler;

            // Initial setup
            try
            {
                TTarget initialTarget = targetSelector();
                SubscribeToTarget(initialTarget);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ObserveDynamic: Error getting initial target: {ex.Message}");
            }

            // Setup cleanup when control is disposed
            control.ExecuteUponDisposal[mainKey] = () =>
            {
                parent.PropertyChanged -= parentHandler;
                CleanupCurrentTarget();
            };

            return control;
        }

        /// <summary>
        /// Subscribes to one specific property changes on a source control and executes a callback when they occur.
        /// Will unsubscribe upon control disposal.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="control"></param>
        /// <param name="target"></param>
        /// <param name="propertyName"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public static T ObserveProperty<T, TSource>(
            this T control,
            TSource target,
            string propertyName,
            Action<T> callback)
            where T : SkiaControl
            where TSource : INotifyPropertyChanged
        {
            return control.Observe(target, (me, prop) =>
            {
                callback?.Invoke(me);
            }, new[] { nameof(BindableObject.BindingContext), propertyName });
        }

        /// <summary>
        /// Extracts the property name from a member access expression (e.g. <c>x => x.Foo</c>).
        /// Tree inspection only, never compiles the expression — safe under iOS/NativeAOT.
        /// </summary>
        private static string GetMemberName<TSource, TProp>(Expression<Func<TSource, TProp>> expression)
        {
            var body = expression.Body;
            if (body is UnaryExpression unary)
                body = unary.Operand;

            if (body is MemberExpression member)
                return member.Member.Name;

            throw new ArgumentException($"Expression '{expression}' must be a property access (e.g. x => x.Foo).");
        }

        /// <summary>
        /// Compiled-property-name overload of <see cref="ObserveProperty{T, TSource}(T, TSource, string, Action{T})"/>:
        /// pass the property as a lambda instead of a string, rename-safe.
        /// Will unsubscribe upon control disposal.
        /// </summary>
        /// <typeparam name="T">Type of the control being extended</typeparam>
        /// <typeparam name="TSource">Type of the source object being observed</typeparam>
        /// <typeparam name="TProp">Type of the observed property</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="target">The source object being observed</param>
        /// <param name="property">Lambda selecting the source property, e.g. <c>x => x.Foo</c></param>
        /// <param name="callback">Callback executed when the property changes</param>
        /// <returns>The control for chaining</returns>
        public static T ObserveProperty<T, TSource, TProp>(
            this T control,
            TSource target,
            Expression<Func<TSource, TProp>> property,
            Action<T> callback)
            where T : SkiaControl
            where TSource : INotifyPropertyChanged
        {
            return control.ObserveProperty(target, GetMemberName(property), callback);
        }

        /// <summary>
        /// Subscribes to one specific property changes on a source and a target control, executing callbacks in both
        /// directions while guarding against re-entrant loops.
        /// Use this to simulate MAUI XAML two-way property binding semantics in fluent C# code.
        /// Will unsubscribe upon control disposal.
        /// </summary>
        /// <typeparam name="T">Type of the target control (the one being extended)</typeparam>
        /// <typeparam name="TSource">Type of the source object being observed</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="target">The source object being observed</param>
        /// <param name="targetPropertyName">Name of the source property to observe</param>
        /// <param name="targetChanged">Callback executed when the source property changes</param>
        /// <param name="controlPropertyName">Name of the control property to observe for reverse sync</param>
        /// <param name="controlChanged">Callback executed when the control property changes</param>
        /// <returns>The target control for chaining</returns>
        public static T ObservePropertyTwoWay<T, TSource>(
            this T control,
            TSource target,
            string targetPropertyName,
            Action<T> targetChanged,
            string controlPropertyName,
            Action<TSource, T> controlChanged)
            where T : SkiaControl, INotifyPropertyChanged
            where TSource : INotifyPropertyChanged
        {
            string subscriptionKey = $"Subscribe2W_{target.GetHashCode()}_{Guid.NewGuid()}";
            bool isSyncing = false;

            void ExecuteGuarded(Action action)
            {
                if (isSyncing)
                    return;

                try
                {
                    isSyncing = true;
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ObservePropertyTwoWay: Error in callback: {ex.Message}");
                }
                finally
                {
                    isSyncing = false;
                }
            }

            PropertyChangedEventHandler sourceHandler = (sender, args) =>
            {
                if (args.PropertyName != targetPropertyName && args.PropertyName != nameof(BindableObject.BindingContext))
                    return;

                ExecuteGuarded(() => targetChanged?.Invoke(control));
            };

            PropertyChangedEventHandler controlHandler = (sender, args) =>
            {
                if (args.PropertyName != controlPropertyName && args.PropertyName != nameof(BindableObject.BindingContext))
                    return;

                ExecuteGuarded(() => controlChanged?.Invoke(target, control));
            };

            target.PropertyChanged += sourceHandler;
            control.PropertyChanged += controlHandler;

            sourceHandler(target, new PropertyChangedEventArgs(nameof(BindableObject.BindingContext)));

            control.ExecuteUponDisposal[subscriptionKey] = () =>
            {
                target.PropertyChanged -= sourceHandler;
                control.PropertyChanged -= controlHandler;
            };

            return control;
        }

        /// <summary>
        /// Compiled-property-name overload of <see cref="ObservePropertyTwoWay{T, TSource}"/>:
        /// pass both properties as lambdas instead of strings, rename-safe.
        /// Will unsubscribe upon control disposal.
        /// </summary>
        /// <typeparam name="T">Type of the target control (the one being extended)</typeparam>
        /// <typeparam name="TSource">Type of the source object being observed</typeparam>
        /// <typeparam name="TTargetProp">Type of the observed source property</typeparam>
        /// <typeparam name="TControlProp">Type of the observed control property</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="target">The source object being observed</param>
        /// <param name="targetProperty">Lambda selecting the source property, e.g. <c>vm => vm.Foo</c></param>
        /// <param name="targetChanged">Callback executed when the source property changes</param>
        /// <param name="controlProperty">Lambda selecting the control property, e.g. <c>me => me.Bar</c></param>
        /// <param name="controlChanged">Callback executed when the control property changes</param>
        /// <returns>The target control for chaining</returns>
        public static T ObservePropertyTwoWay<T, TSource, TTargetProp, TControlProp>(
            this T control,
            TSource target,
            Expression<Func<TSource, TTargetProp>> targetProperty,
            Action<T> targetChanged,
            Expression<Func<T, TControlProp>> controlProperty,
            Action<TSource, T> controlChanged)
            where T : SkiaControl, INotifyPropertyChanged
            where TSource : INotifyPropertyChanged
        {
            return control.ObservePropertyTwoWay(
                target,
                GetMemberName(targetProperty),
                targetChanged,
                GetMemberName(controlProperty),
                controlChanged);
        }

        /// <summary>
        /// Subscribes to one specific property changes on a source control obtained via lambda expression and executes a callback when they occur.
        /// Will unsubscribe upon control disposal.
        /// </summary>
        /// <typeparam name="T">Type of the control being extended</typeparam>
        /// <typeparam name="TSource">Type of the source control being observed</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="targetSelector">Lambda expression that returns the target control (e.g., () => clickLabel)</param>
        /// <param name="propertyName">Name of the property to observe</param>
        /// <param name="callback">Callback to execute when the property changes</param>
        /// <returns>The control for chaining</returns>
        public static T ObserveProperty<T, TSource>(
            this T control,
            Func<TSource> targetSelector,
            string propertyName,
            Action<T> callback)
            where T : SkiaControl
            where TSource : INotifyPropertyChanged
        {
            return control.Initialize(me =>
            {
                // Track current subscription for cleanup
                string mainKey = $"ObserveProperty_{propertyName}_{Guid.NewGuid()}";
                TSource currentTarget = default(TSource);
                PropertyChangedEventHandler currentTargetHandler = null;

                // Helper to clean up current target subscription
                void CleanupCurrentTarget()
                {
                    if (currentTarget != null && currentTargetHandler != null)
                    {
                        currentTarget.PropertyChanged -= currentTargetHandler;
                        currentTarget = default(TSource);
                        currentTargetHandler = null;
                    }
                }

                // Helper to setup subscription to a new target
                void SubscribeToTarget(TSource target)
                {
                    // Clean up previous subscription
                    CleanupCurrentTarget();

                    if (target == null) return;

                    currentTarget = target;
                    currentTargetHandler = (sender, args) =>
                    {
                        // Only trigger on the specific property or BindingContext
                        if (args.PropertyName == propertyName || args.PropertyName == nameof(BindableObject.BindingContext))
                        {
                            try
                            {
                                callback(me);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"ObserveProperty: Error in callback: {ex.Message}");
                            }
                        }
                    };

                    target.PropertyChanged += currentTargetHandler;

                    // Initial call with BindingContext to trigger initial update
                    currentTargetHandler(target, new PropertyChangedEventArgs("BindingContext"));
                }

                // Re-evaluate target selector and subscribe
                void RefreshTarget()
                {
                    try
                    {
                        TSource newTarget = targetSelector();
                        SubscribeToTarget(newTarget);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ObserveProperty: Error getting target: {ex.Message}");
                    }
                }

                // Initial setup
                RefreshTarget();

                // Setup cleanup when control is disposed
                me.ExecuteUponDisposal[mainKey] = () =>
                {
                    CleanupCurrentTarget();
                };
            });
        }

        /// <summary>
        /// Compiled-property-name overload of <see cref="ObserveProperty{T, TSource}(T, Func{TSource}, string, Action{T})"/>:
        /// pass the property as a lambda instead of a string, rename-safe.
        /// </summary>
        /// <typeparam name="T">Type of the control being extended</typeparam>
        /// <typeparam name="TSource">Type of the source control being observed</typeparam>
        /// <typeparam name="TProp">Type of the observed property</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="targetSelector">Lambda expression that returns the target control (e.g., () => Model)</param>
        /// <param name="property">Lambda selecting the source property, e.g. <c>x => x.Foo</c></param>
        /// <param name="callback">Callback to execute when the property changes</param>
        /// <returns>The control for chaining</returns>
        public static T ObserveProperty<T, TSource, TProp>(
            this T control,
            Func<TSource> targetSelector,
            Expression<Func<TSource, TProp>> property,
            Action<T> callback)
            where T : SkiaControl
            where TSource : INotifyPropertyChanged
        {
            return control.ObserveProperty(targetSelector, GetMemberName(property), callback);
        }

        /// <summary>
        /// Subscribes to specific properties changes on a source control and executes a callback when they occur.
        /// 
        /// Will unsubscribe upon control disposal.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="control"></param>
        /// <param name="target"></param>
        /// <param name="propertyNames"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public static T ObserveProperties<T, TSource>(
            this T control,
            TSource target,
            IEnumerable<string> propertyNames,
            Action<T> callback)
            where T : SkiaControl
            where TSource : INotifyPropertyChanged
        {
            var names = propertyNames as string[] ?? propertyNames.ToArray();
            if (names.Length == 0)
                throw new ArgumentException(
                    "ObserveProperties: no properties to observe, callback would fire only once at attach. Pass at least one property name.",
                    nameof(propertyNames));

            var props = names.Concat(new[] { nameof(BindableObject.BindingContext) }).ToArray();
            return control.Observe(target, (me, prop) =>
            {
                callback?.Invoke(me);
            }, props);
        }

        /// <summary>
        /// Compiled-property-name overload of <see cref="ObserveProperties{T, TSource}(T, TSource, IEnumerable{string}, Action{T})"/>:
        /// pass properties as lambdas instead of strings, rename-safe. Note: callback comes before the property
        /// lambdas here (params must be the last parameter).
        /// </summary>
        /// <typeparam name="T">Type of the control being extended</typeparam>
        /// <typeparam name="TSource">Type of the source control being observed</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="target">The source object being observed</param>
        /// <param name="callback">Callback to execute when any of the properties change</param>
        /// <param name="property">First observed property, e.g. <c>x => x.Foo</c>. Required: observing nothing is always a bug.</param>
        /// <param name="moreProperties">Additional properties, e.g. <c>x => x.Bar, x => x.Baz</c></param>
        /// <returns>The control for chaining</returns>
        public static T ObserveProperties<T, TSource>(
            this T control,
            TSource target,
            Action<T> callback,
            Expression<Func<TSource, object>> property,
            params Expression<Func<TSource, object>>[] moreProperties)
            where T : SkiaControl
            where TSource : INotifyPropertyChanged
        {
            return control.ObserveProperties(target,
                moreProperties.Prepend(property).Select(GetMemberName), callback);
        }

        /// <summary>
        /// Subscribes to specific properties changes on a source control obtained via lambda expression and executes a callback when they occur.
        /// Will unsubscribe upon control disposal.
        /// </summary>
        /// <typeparam name="T">Type of the control being extended</typeparam>
        /// <typeparam name="TSource">Type of the source control being observed</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="targetSelector">Lambda expression that returns the target control (e.g., () => clickLabel)</param>
        /// <param name="propertyNames">Names of the properties to observe</param>
        /// <param name="callback">Callback to execute when properties change</param>
        /// <returns>The control for chaining</returns>
        public static T ObserveProperties<T, TSource>(
            this T control,
            Func<TSource> targetSelector,
            IEnumerable<string> propertyNames,
            Action<T> callback)
            where T : SkiaControl
            where TSource : INotifyPropertyChanged
        {
            var names = propertyNames as string[] ?? propertyNames.ToArray();
            if (names.Length == 0)
                throw new ArgumentException(
                    "ObserveProperties: no properties to observe, callback would fire only once at attach. Pass at least one property name.",
                    nameof(propertyNames));
            propertyNames = names;

            return control.Initialize(me =>
            {
                // Track current subscription for cleanup
                string mainKey = $"ObserveProperties_{Guid.NewGuid()}";
                TSource currentTarget = default(TSource);
                PropertyChangedEventHandler currentTargetHandler = null;
                var props = new HashSet<string>(propertyNames.Concat(new[] { nameof(BindableObject.BindingContext) }));

                // Helper to clean up current target subscription
                void CleanupCurrentTarget()
                {
                    if (currentTarget != null && currentTargetHandler != null)
                    {
                        currentTarget.PropertyChanged -= currentTargetHandler;
                        currentTarget = default(TSource);
                        currentTargetHandler = null;
                    }
                }

                // Helper to setup subscription to a new target
                void SubscribeToTarget(TSource target)
                {
                    // Clean up previous subscription
                    CleanupCurrentTarget();

                    if (target == null) return;

                    currentTarget = target;
                    currentTargetHandler = (sender, args) =>
                    {
                        // Only trigger on the specific properties or BindingContext
                        if (props.Contains(args.PropertyName))
                        {
                            try
                            {
                                callback(me);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"ObserveProperties: Error in callback: {ex.Message}");
                            }
                        }
                    };

                    target.PropertyChanged += currentTargetHandler;

                    // Initial call with BindingContext to trigger initial update
                    currentTargetHandler(target, new PropertyChangedEventArgs("BindingContext"));
                }

                // Re-evaluate target selector and subscribe
                void RefreshTarget()
                {
                    try
                    {
                        TSource newTarget = targetSelector();
                        SubscribeToTarget(newTarget);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ObserveProperties: Error getting target: {ex.Message}");
                    }
                }

                // Initial setup
                RefreshTarget();

                // Setup cleanup when control is disposed
                me.ExecuteUponDisposal[mainKey] = () =>
                {
                    CleanupCurrentTarget();
                };
            });
        }

        /// <summary>
        /// Compiled-property-name overload of <see cref="ObserveProperties{T, TSource}(T, Func{TSource}, IEnumerable{string}, Action{T})"/>:
        /// pass properties as lambdas instead of strings, rename-safe. Note: callback comes before the property
        /// lambdas here (params must be the last parameter).
        /// </summary>
        /// <typeparam name="T">Type of the control being extended</typeparam>
        /// <typeparam name="TSource">Type of the source control being observed</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="targetSelector">Lambda expression that returns the target control (e.g., () => Model)</param>
        /// <param name="callback">Callback to execute when any of the properties change</param>
        /// <param name="property">First observed property, e.g. <c>x => x.Foo</c>. Required: observing nothing is always a bug.</param>
        /// <param name="moreProperties">Additional properties, e.g. <c>x => x.Bar, x => x.Baz</c></param>
        /// <returns>The control for chaining</returns>
        public static T ObserveProperties<T, TSource>(
            this T control,
            Func<TSource> targetSelector,
            Action<T> callback,
            Expression<Func<TSource, object>> property,
            params Expression<Func<TSource, object>>[] moreProperties)
            where T : SkiaControl
            where TSource : INotifyPropertyChanged
        {
            return control.ObserveProperties(targetSelector,
                moreProperties.Prepend(property).Select(GetMemberName), callback);
        }

        /// <summary>
        /// Observes a control that will be assigned later in the initialization process.
        /// </summary>
        /// <typeparam name="T">Type of the target control (the one being extended)</typeparam>
        /// <typeparam name="TSource">Type of the source control (the one that will be observed)</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="sourceFetcher">Function that will retrieve the source control when needed</param>
        /// <param name="callback">Callback that receives the control instance and property name when changed</param>
        /// <param name="propertyFilter">Optional filter to only trigger on specific properties</param>
        /// <returns>The target control for chaining</returns>
        public static T Observe<T, TSource>(
            this T control,
            Func<TSource> sourceFetcher,
            Action<T, string> callback,
            string[] propertyFilter = null)
            where T : SkiaControl
            where TSource : SkiaControl, INotifyPropertyChanged
        {
            control.Initialize(me =>
            {
                // Track current subscription for cleanup
                string mainKey = $"Observe_{Guid.NewGuid()}";
                TSource currentSource = null;
                PropertyChangedEventHandler currentSourceHandler = null;
                var filterSet = propertyFilter != null ? new HashSet<string>(propertyFilter) : null;

                // Helper to clean up current source subscription
                void CleanupCurrentSource()
                {
                    if (currentSource != null && currentSourceHandler != null)
                    {
                        currentSource.PropertyChanged -= currentSourceHandler;
                        currentSource = null;
                        currentSourceHandler = null;
                    }
                }

                // Helper to setup subscription to a new source
                void SubscribeToSource(TSource source)
                {
                    // Clean up previous subscription
                    CleanupCurrentSource();

                    if (source == null) return;

                    currentSource = source;
                    currentSourceHandler = (sender, args) =>
                    {
                        // If a filter is specified, only proceed if the property is in the filter
                        if (filterSet != null && !filterSet.Contains(args.PropertyName))
                            return;

                        try
                        {
                            callback(me, args.PropertyName);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Observe: Error in callback: {ex.Message}");
                        }
                    };

                    source.PropertyChanged += currentSourceHandler;

                    // Initial call with BindingContext to trigger initial update
                    currentSourceHandler(source, new PropertyChangedEventArgs("BindingContext"));
                }

                // Re-evaluate source fetcher and subscribe
                void RefreshSource()
                {
                    try
                    {
                        TSource newSource = sourceFetcher();
                        SubscribeToSource(newSource);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Observe: Error getting source: {ex.Message}");
                    }
                }

                // Initial setup
                RefreshSource();

                // Setup cleanup when control is disposed
                me.ExecuteUponDisposal[mainKey] = () =>
                {
                    CleanupCurrentSource();
                };
            });

            return control;
        }

        /// <summary>
        /// Observes a control that will be assigned later in the initialization process.
        /// Simplified version that doesn't pass the property name to the callback.
        /// </summary>
        /// <typeparam name="T">Type of the target control (the one being extended)</typeparam>
        /// <typeparam name="TSource">Type of the source control (the one that will be observed)</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="sourceFetcher">Function that will retrieve the source control when needed</param>
        /// <param name="callback">Callback that receives only the control instance when changed</param>
        /// <param name="propertyFilter">Optional filter to only trigger on specific properties</param>
        /// <returns>The target control for chaining</returns>
        public static T Observe<T, TSource>(
            this T control,
            Func<TSource> sourceFetcher,
            Action<T> callback,
            string[] propertyFilter = null) 
            where T : SkiaControl
            where TSource : SkiaControl, INotifyPropertyChanged
        {
            return control.Observe(sourceFetcher, (me, prop) => callback?.Invoke(me), propertyFilter);
        }

        /// <summary>
        /// Observes a source control with a simplified callback that doesn't include the property name.
        /// Will unsubscribe upon control disposal.
        /// </summary>
        /// <typeparam name="T">Type of the target control (the one being extended)</typeparam>
        /// <typeparam name="TSource">Type of the source control (the one being observed)</typeparam>
        /// <param name="control">The control subscribing to changes</param>
        /// <param name="target">The control being observed</param>
        /// <param name="callback">Callback that receives only the control instance when changed</param>
        /// <param name="propertyFilter">Optional filter to only trigger on specific properties</param>
        /// <returns>The target control for chaining</returns>
        public static T Observe<T, TSource>(
            this T control,
            TSource target,
            Action<T> callback,
            string[] propertyFilter = null)
            where T : SkiaControl
            where TSource : INotifyPropertyChanged
        {
            return control.Observe(target, (me, prop) => callback?.Invoke(me), propertyFilter);
        }

        /// <summary>
        /// Watches for property changes on the control's BindingContext of type TSource.
        /// Works with both immediate and delayed BindingContext assignment scenarios.
        /// </summary>
        /// <typeparam name="T">Type of the control</typeparam>
        /// <typeparam name="TSource">Expected type of the BindingContext</typeparam>
        /// <param name="control">The control to watch</param>
        /// <param name="callback">Callback executed when properties change, receiving the control, the typed BindingContext, and the property name</param>
        /// <param name="debugTypeMismatch">Whether to log a warning when the actual BindingContext type doesn't match TSource (default: true)</param>
        /// <returns>The control for chaining</returns>
        /// <remarks>
        /// This method handles two scenarios:
        /// 1. The BindingContext is already set when the method is called
        /// 2. The BindingContext will be set sometime after the method is called
        /// 
        /// The callback will be invoked immediately after subscription with an empty property name,
        /// allowing initialization based on the current state.
        /// </remarks>
        public static T ObserveBindingContext<T, TSource>(
            this T control,
            Action<T, TSource, string> callback,
            bool debugTypeMismatch = true)
            where T : SkiaControl
            where TSource : INotifyPropertyChanged
        {
            // Local method to handle subscription and initial call
            void SubscribeToViewModel(TSource tvm)
            {
                Observe(control, tvm, (me, prop) => { InvokeCallback(me, tvm, prop); });
            }

            // Local method to safely invoke the callback
            void InvokeCallback(T ctrl, TSource vm, string prop)
            {
                try
                {
                    callback.Invoke(ctrl, vm, prop);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"WatchBindingContext: Error in ViewModel property changed callback: {ex.Message}");
                }
            }

            // Check if BindingContext is already set
            if (control.BindingContext is TSource tvm)
            {
                SubscribeToViewModel(tvm);
            }
            else if (control.BindingContext != null && debugTypeMismatch)
            {
                // BindingContext exists but is of the wrong type - log a warning
                Trace.WriteLine(
                    $"[WARNING] ObserveBindingContext: Expected BindingContext type {typeof(TSource).Name} but got {control.BindingContext.GetType().Name} for control {control.GetType().Name}");
            }

            // Set up subscription for when BindingContext changes
            string subscriptionKey = $"watch_{Guid.NewGuid()}";

            void ControlOnApplyingBindingContext(object sender, EventArgs e)
            {
                if (control.BindingContext is TSource tvm)
                {
                    // Set up the actual subscription
                    SubscribeToViewModel(tvm);
                }
                else if (control.BindingContext != null && debugTypeMismatch)
                {
                    // BindingContext changed but is still the wrong type - log a warning
                    Trace.WriteLine(
                        $"[WARNING] ObserveBindingContext: Expected BindingContext type {typeof(TSource).Name} but got {control.BindingContext.GetType().Name} for control {control.GetType().Name}");
                }
            }

            // Register the temporary event handler and its cleanup
            control.ApplyingBindingContext += ControlOnApplyingBindingContext;
            control.ExecuteUponDisposal[subscriptionKey] = () =>
            {
                control.ApplyingBindingContext -= ControlOnApplyingBindingContext;
            };

            return control;
        }

         

        /// <summary>
        /// Watches for property changes on another control's BindingContext of type TSource.
        /// </summary>
        /// <typeparam name="T">Type of the control being extended</typeparam>
        /// <typeparam name="TTarget">Type of the target control whose BindingContext we're watching</typeparam>
        /// <typeparam name="TSource">Expected type of the target control's BindingContext</typeparam>
        /// <param name="control">The control to extend</param>
        /// <param name="target">The target control whose BindingContext to watch</param>
        /// <param name="callback">Callback executed when properties change, receiving the control, the target control, the typed BindingContext, and the property name</param>
        /// <param name="debugTypeMismatch">Whether to log a warning when the actual BindingContext type doesn't match TSource (default: true)</param>
        /// <returns>The original control for chaining</returns>
        /// <remarks>
        /// This method handles two scenarios:
        /// 1. The target's BindingContext is already set when the method is called
        /// 2. The target's BindingContext will be set sometime after the method is called
        /// </remarks>
        public static T ObserveBindingContextOn<T, TTarget, TSource>(
            this T control,
            TTarget target,
            Action<T, TTarget, TSource, string> callback,
            bool debugTypeMismatch = true)
            where T : SkiaControl
            where TTarget : SkiaControl
            where TSource : INotifyPropertyChanged
        {
            // Local method to handle subscription and initial call
            void SubscribeToViewModel(TSource tvm)
            {
                // Subscribe directly
                Observe(control, tvm, (me, prop) => { InvokeCallback(me, target, tvm, prop); });

                // Initial call with empty property name
                InvokeCallback(control, target, tvm, nameof(SkiaControl.BindingContext));
            }

            // Local method to safely invoke the callback
            void InvokeCallback(T ctrl, TTarget tgt, TSource vm, string prop)
            {
                try
                {
                    callback.Invoke(ctrl, tgt, vm, prop);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"WatchOtherBindingContext: Error in ViewModel property changed callback: {ex.Message}");
                }
            }

            // Check if the target's BindingContext is already set
            if (target.BindingContext is TSource tvm)
            {
                SubscribeToViewModel(tvm);
            }
            else if (target.BindingContext != null && debugTypeMismatch)
            {
                // BindingContext exists but is of the wrong type - log a warning
                Trace.WriteLine(
                    $"[WARNING] ObserveBindingContextOn: Expected BindingContext type {typeof(TSource).Name} but got {target.BindingContext.GetType().Name} for target control {target.GetType().Name}");
            }

            // Set up subscription for when the target's BindingContext changes
            string subscriptionKey = $"watch_other_{Guid.NewGuid()}";

            void TargetOnApplyingBindingContext(object sender, EventArgs e)
            {
                if (target.BindingContext is TSource tvm)
                {
                    // Clean up the temporary event handler
                    target.ApplyingBindingContext -= TargetOnApplyingBindingContext;
                    control.ExecuteUponDisposal.Remove(subscriptionKey);

                    // Set up the actual subscription
                    SubscribeToViewModel(tvm);
                }
                else if (target.BindingContext != null && debugTypeMismatch)
                {
                    // BindingContext changed but is still the wrong type - log a warning
                    Trace.WriteLine(
                        $"[WARNING] ObserveBindingContextOn: Expected BindingContext type {typeof(TSource).Name} but got {target.BindingContext.GetType().Name} for target control {target.GetType().Name}");
                }
            }

            // Register the temporary event handler and its cleanup
            target.ApplyingBindingContext += TargetOnApplyingBindingContext;
            control.ExecuteUponDisposal[subscriptionKey] = () =>
            {
                target.ApplyingBindingContext -= TargetOnApplyingBindingContext;
            };

            return control;
        }

        #endregion

        #region GESTURES


        /// <summary>
        /// Uses an `AddGestures.SetCommandTapped` with this control, will invoke code in passed callback when tapped.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="view"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static T OnTapped<T>(this T view, Action<T> action) where T : SkiaControl
        {
            try
            {
                void onTapped(object s, ControlTappedEventArgs a)
                {
                    action?.Invoke(view);
                }
                view.Tapped += onTapped;
                string subscriptionKey = $"tapped_{Guid.NewGuid()}";
                view.ExecuteUponDisposal[subscriptionKey] = () =>
                {
                    view.Tapped -= onTapped;
                };
                //not using effect anymore since they are invoked by parents and we want this to be directly invoked on this view
                //AddGestures.SetCommandTapped(view, new Command((ctx) => { action?.Invoke(view); }));
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            return view;
        }


        /// <summary>
        /// Attaches a handler to the TextChanged event with automatic cleanup on disposal.
        /// </summary>
        /// <remarks>The event subscription is automatically removed when the view is disposed to prevent
        /// memory leaks.</remarks>
        /// <typeparam name="T">The type of the view, constrained to SkiaEditor or its derived types.</typeparam>
        /// <param name="view">The editor control to attach the event handler to.</param>
        /// <param name="action">The callback to invoke when the text changes, receiving the new text value.</param>
        /// <returns>The same view instance for fluent chaining.</returns>
        public static T OnTextChanged<T>(this T view, Action<string> action) where T : SkiaEditor
        {
            try
            {
                void onText(object? s, string s1)
                {
                    action?.Invoke(s1);
                }
                view.TextChanged += onText;
                string subscriptionKey = $"text_changed_{Guid.NewGuid()}";
                view.ExecuteUponDisposal[subscriptionKey] = () =>
                {
                    view.TextChanged -= onText;
                };
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            return view;
        }

        /// <summary>
        /// Attaches a handler to the FocusChanged event with automatic cleanup on disposal.
        /// </summary>
        /// <typeparam name="T">The type of the view, constrained to SkiaEditor or its derived types.</typeparam>
        /// <param name="view">The editor control to attach the event handler to.</param>
        /// <param name="action">The callback to invoke when focus changes, receiving the editor and its new focus state.</param>
        /// <returns>The same view instance for fluent chaining.</returns>
        public static T OnFocusChanged<T>(this T view, Action<T, bool> action) where T : SkiaEditor
        {
            try
            {
                void onFocus(object? s, bool focused)
                {
                    action?.Invoke(view, focused);
                }
                view.FocusChanged += onFocus;
                string subscriptionKey = $"focus_changed_{Guid.NewGuid()}";
                view.ExecuteUponDisposal[subscriptionKey] = () =>
                {
                    view.FocusChanged -= onFocus;
                };
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            return view;
        }

        public static T OnTapped<T>(this T view, Action<T, ControlTappedEventArgs> action) where T : SkiaControl
        {
            try
            {
                void onTapped(object s, ControlTappedEventArgs a)
                {
                    action?.Invoke(view, a);
                }
                view.Tapped += onTapped;
                string subscriptionKey = $"tapped_{Guid.NewGuid()}";
                view.ExecuteUponDisposal[subscriptionKey] = () =>
                {
                    view.Tapped -= onTapped;
                };
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            return view;
        }


        /// <summary>
        /// Attaches a handler to the ContextMenu event (right click / long press / Menu key on the web heads) with
        /// automatic cleanup on disposal. Set <c>e.Handled = true</c> inside to suppress the browser's own menu.
        /// </summary>
        public static T OnContextMenu<T>(this T view, Action<T, ContextMenuEventArgs> action) where T : SkiaControl
        {
            try
            {
                void onContextMenu(object s, ContextMenuEventArgs a)
                {
                    action?.Invoke(view, a);
                }
                view.ContextMenu += onContextMenu;
                string subscriptionKey = $"contextmenu_{Guid.NewGuid()}";
                view.ExecuteUponDisposal[subscriptionKey] = () =>
                {
                    view.ContextMenu -= onContextMenu;
                };
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            return view;
        }

        #endregion

        #region UI

        /// <summary>
        /// Adds multiple child controls to a layout
        /// </summary>
        /// <typeparam name="T">Type of SkiaLayout</typeparam>
        /// <param name="view">The layout to add children to</param>
        /// <param name="children">The children to add</param>
        /// <returns>The layout for chaining</returns>
        public static T WithChildren<T>(this T view, params SkiaControl[] children) where T : SkiaLayout
        {
            foreach (SkiaControl child in children)
            {
                view.AddSubView(child);
            }

            return view;
        }

        /// <summary>
        /// Sets the content of a container that implements IWithContent
        /// </summary>
        /// <typeparam name="T">Type implementing IWithContent</typeparam>
        /// <param name="view">The container</param>
        /// <param name="child">The content to set</param>
        /// <returns>The container for chaining</returns>
        public static T WithContent<T>(this T view, SkiaControl child) where T : IWithContent
        {
            view.Content = child;
            return view;
        }

        /// <summary>
        /// Adds the control to a parent and returns the control for further chaining
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to add</param>
        /// <param name="parent">The parent to add the control to</param>
        /// <returns>The control for chaining</returns>
        public static T WithParent<T>(this T view, IDrawnBase parent) where T : SkiaControl
        {
            parent.AddSubView(view);
            return view;
        }

        #region LAYOUT

        /// <summary>
        /// Sets the control's horizontal options to center
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to center horizontally</param>
        /// <returns>The control for chaining</returns>
        public static T CenterX<T>(this T view) where T : SkiaControl
        {
            view.HorizontalOptions = LayoutOptions.Center;
            return view;
        }

        /// <summary>
        /// Sets the control's vertical options to center
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to center vertically</param>
        /// <returns>The control for chaining</returns>
        public static T CenterY<T>(this T view) where T : SkiaControl
        {
            view.VerticalOptions = LayoutOptions.Center;
            return view;
        }

        /// <summary>
        /// Fills in both directions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="view"></param>
        /// <returns></returns>
        public static T Fill<T>(this T view) where T : SkiaControl
        {
            view.HorizontalOptions = LayoutOptions.Fill;
            view.VerticalOptions = LayoutOptions.Fill;
            return view;
        }

        /// <summary>
        /// Fills horizontally
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="view"></param>
        /// <returns></returns>
        public static T FillX<T>(this T view) where T : SkiaControl
        {
            view.HorizontalOptions = LayoutOptions.Fill;
            return view;
        }

        public static T EndX<T>(this T view) where T : SkiaControl
        {
            view.HorizontalOptions = LayoutOptions.End;
            return view;
        }

        public static T EndY<T>(this T view) where T : SkiaControl
        {
            view.VerticalOptions = LayoutOptions.End;
            return view;
        }

        public static T StartX<T>(this T view) where T : SkiaControl
        {
            view.HorizontalOptions = LayoutOptions.Start;
            return view;
        }

        public static T StartY<T>(this T view) where T : SkiaControl
        {
            view.VerticalOptions = LayoutOptions.Start;
            return view;
        }


        /// <summary>
        /// Fills vertically
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="view"></param>
        /// <returns></returns>
        public static T FillY<T>(this T view) where T : SkiaControl
        {
            view.VerticalOptions = LayoutOptions.Fill;
            return view;
        }

        /// <summary>
        /// Centers the control both horizontally and vertically
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to center</param>
        /// <returns>The control for chaining</returns>
        public static T Center<T>(this T view) where T : SkiaControl
        {
            view.HorizontalOptions = LayoutOptions.Center;
            view.VerticalOptions = LayoutOptions.Center;
            return view;
        }

        /// <summary>
        /// Centers the label's TEXT horizontally (<see cref="SkiaLabel.HorizontalTextAlignment"/> = Center).
        /// Alignment of text inside the label, not of the label inside its parent — for that use <see cref="CenterX{T}"/>.
        /// </summary>
        public static T CenterTextX<T>(this T label) where T : SkiaLabel
        {
            label.HorizontalTextAlignment = DrawTextAlignment.Center;
            return label;
        }

        /// <summary>
        /// Centers the label's TEXT vertically (<see cref="SkiaLabel.VerticalTextAlignment"/> = Center).
        /// Alignment of text inside the label, not of the label inside its parent — for that use <see cref="CenterY{T}"/>.
        /// </summary>
        public static T CenterTextY<T>(this T label) where T : SkiaLabel
        {
            label.VerticalTextAlignment = TextAlignment.Center;
            return label;
        }

        /// <summary>
        /// Centers the label's TEXT in both directions. See <see cref="CenterTextX{T}"/> / <see cref="CenterTextY{T}"/>.
        /// </summary>
        public static T CenterText<T>(this T label) where T : SkiaLabel
        {
            label.HorizontalTextAlignment = DrawTextAlignment.Center;
            label.VerticalTextAlignment = TextAlignment.Center;
            return label;
        }

        #endregion

        /// <summary>
        /// Sets the margin for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set margin for</param>
        /// <param name="uniformMargin">The uniform margin to apply to all sides</param>
        /// <returns>The control for chaining</returns>
        public static T WithMargin<T>(this T view, double uniformMargin) where T : SkiaControl
        {
            view.Margin = new Thickness(uniformMargin);
            return view;
        }

        /// <summary>
        /// Sets the margin for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set margin for</param>
        /// <param name="horizontal">The left and right margin</param>
        /// <param name="vertical">The top and bottom margin</param>
        /// <returns>The control for chaining</returns>
        public static T WithMargin<T>(this T view, double horizontal, double vertical) where T : SkiaControl
        {
            view.Margin = new Thickness(horizontal, vertical);
            return view;
        }

        /// <summary>
        /// Sets the margin for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set margin for</param>
        /// <param name="left">The left margin</param>
        /// <param name="top">The top margin</param>
        /// <param name="right">The right margin</param>
        /// <param name="bottom">The bottom margin</param>
        /// <returns>The control for chaining</returns>
        public static T WithMargin<T>(this T view, double left, double top, double right, double bottom)
            where T : SkiaControl
        {
            view.Margin = new Thickness(left, top, right, bottom);
            return view;
        }

        /// <summary>
        /// Sets the padding for a layout control
        /// </summary>
        /// <typeparam name="T">Type of SkiaLayout</typeparam>
        /// <param name="view">The layout to set padding for</param>
        /// <param name="uniformPadding">The uniform padding to apply to all sides</param>
        /// <returns>The layout for chaining</returns>
        public static T WithPadding<T>(this T view, double uniformPadding) where T : SkiaLayout
        {
            view.Padding = new Thickness(uniformPadding);
            return view;
        }

        /// <summary>
        /// Sets the padding for a layout control
        /// </summary>
        /// <typeparam name="T">Type of SkiaLayout</typeparam>
        /// <param name="view">The layout to set padding for</param>
        /// <param name="horizontal">The left and right padding</param>
        /// <param name="vertical">The top and bottom padding</param>
        /// <returns>The layout for chaining</returns>
        public static T WithPadding<T>(this T view, double horizontal, double vertical) where T : SkiaLayout
        {
            view.Padding = new Thickness(horizontal, vertical);
            return view;
        }

        /// <summary>
        /// Sets the padding for a layout control
        /// </summary>
        /// <typeparam name="T">Type of SkiaLayout</typeparam>
        /// <param name="view">The layout to set padding for</param>
        /// <param name="left">The left padding</param>
        /// <param name="top">The top padding</param>
        /// <param name="right">The right padding</param>
        /// <param name="bottom">The bottom padding</param>
        /// <returns>The layout for chaining</returns>
        public static T WithPadding<T>(this T view, double left, double top, double right, double bottom)
            where T : SkiaLayout
        {
            view.Padding = new Thickness(left, top, right, bottom);
            return view;
        }

        /// <summary>
        /// Sets the Tag property for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set the Tag for</param>
        /// <param name="tag">The tag value</param>
        /// <returns>The control for chaining</returns>
        public static T WithTag<T>(this T view, string tag) where T : SkiaControl
        {
            view.Tag = tag;
            return view;
        }

        #endregion

        #region IMPROVED FLUENT EXTENSIONS

        /// <summary>
        /// Sets the cache type for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set cache for</param>
        /// <param name="cacheType">The cache type</param>
        /// <returns>The control for chaining</returns>
        public static T WithCache<T>(this T view, SkiaCacheType cacheType) where T : SkiaControl
        {
            view.UseCache = cacheType;
            return view;
        }

        /// <summary>
        /// Sets the background color for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set background color for</param>
        /// <param name="color">The background color</param>
        /// <returns>The control for chaining</returns>
        public static T WithBackgroundColor<T>(this T view, Color color) where T : SkiaControl
        {
            view.BackgroundColor = color;
            return view;
        }

        /// <summary>
        /// Sets the horizontal options for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set horizontal options for</param>
        /// <param name="options">The horizontal layout options</param>
        /// <returns>The control for chaining</returns>
        public static T WithHorizontalOptions<T>(this T view, LayoutOptions options) where T : SkiaControl
        {
            view.HorizontalOptions = options;
            return view;
        }

        /// <summary>
        /// Sets the vertical options for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set vertical options for</param>
        /// <param name="options">The vertical layout options</param>
        /// <returns>The control for chaining</returns>
        public static T WithVerticalOptions<T>(this T view, LayoutOptions options) where T : SkiaControl
        {
            view.VerticalOptions = options;
            return view;
        }

        /// <summary>
        /// Sets the height request for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set height for</param>
        /// <param name="height">The height request</param>
        /// <returns>The control for chaining</returns>
        public static T WithHeight<T>(this T view, double height) where T : SkiaControl
        {
            view.HeightRequest = height;
            return view;
        }

        /// <summary>
        /// Sets the width request for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set width for</param>
        /// <param name="width">The width request</param>
        /// <returns>The control for chaining</returns>
        public static T WithWidth<T>(this T view, double width) where T : SkiaControl
        {
            view.WidthRequest = width;
            return view;
        }

        /// <summary>
        /// Sets the margin for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set margin for</param>
        /// <param name="margin">The margin thickness</param>
        /// <returns>The control for chaining</returns>
        public static T WithMargin<T>(this T view, Thickness margin) where T : SkiaControl
        {
            view.Margin = margin;
            return view;
        }

        public static T WithVisibility<T>(this T view, bool value) where T : SkiaControl
        {
            view.IsVisible =value;
            return view;
        }

        #endregion

        #region SKIASHAPE EXTENSIONS

        /// <summary>
        /// Sets the shape type for SkiaShape
        /// </summary>
        /// <typeparam name="T">Type of SkiaShape</typeparam>
        /// <param name="shape">The shape to set type for</param>
        /// <param name="shapeType">The shape type</param>
        /// <returns>The shape for chaining</returns>
        public static T WithShapeType<T>(this T shape, ShapeType shapeType) where T : SkiaShape
        {
            shape.Type = shapeType;
            return shape;
        }

        /// <summary>
        /// Sets the shape type for SkiaShape (shorter alias)
        /// </summary>
        /// <typeparam name="T">Type of SkiaShape</typeparam>
        /// <param name="shape">The shape to set type for</param>
        /// <param name="shapeType">The shape type</param>
        /// <returns>The shape for chaining</returns>
        public static T Shape<T>(this T shape, ShapeType shapeType) where T : SkiaShape
        {
            shape.Type = shapeType;
            return shape;
        }

        #endregion

        #region SKIAIMAGE EXTENSIONS

        /// <summary>
        /// Sets the aspect for SkiaImage
        /// </summary>
        /// <typeparam name="T">Type of SkiaImage</typeparam>
        /// <param name="image">The image to set aspect for</param>
        /// <param name="aspect">The transform aspect</param>
        /// <returns>The image for chaining</returns>
        public static T WithAspect<T>(this T image, TransformAspect aspect) where T : SkiaImage
        {
            image.Aspect = aspect;
            return image;
        }

        #endregion

        #region SKIALABEL EXTENSIONS

        /// <summary>
        /// Sets the font size for SkiaLabel
        /// </summary>
        /// <typeparam name="T">Type of SkiaLabel</typeparam>
        /// <param name="label">The label to set font size for</param>
        /// <param name="fontSize">The font size</param>
        /// <returns>The label for chaining</returns>
        public static T WithFontSize<T>(this T label, double fontSize) where T : SkiaLabel
        {
            label.FontSize = fontSize;
            return label;
        }

        /// <summary>
        /// Sets the text color for SkiaLabel
        /// </summary>
        /// <typeparam name="T">Type of SkiaLabel</typeparam>
        /// <param name="label">The label to set text color for</param>
        /// <param name="color">The text color</param>
        /// <returns>The label for chaining</returns>
        public static T WithTextColor<T>(this T label, Color color) where T : SkiaLabel
        {
            label.TextColor = color;
            return label;
        }

        /// <summary>
        /// Sets the horizontal text alignment for SkiaLabel
        /// </summary>
        /// <typeparam name="T">Type of SkiaLabel</typeparam>
        /// <param name="label">The label to set alignment for</param>
        /// <param name="alignment">The horizontal text alignment</param>
        /// <returns>The label for chaining</returns>
        public static T WithHorizontalTextAlignment<T>(this T label, DrawTextAlignment alignment) where T : SkiaLabel
        {
            label.HorizontalTextAlignment = alignment;
            return label;
        }

        #endregion


        /// <summary>
        /// Registers a callback to be executed when the text of a SkiaLabel changes.
        /// </summary>
        /// <param name="control">The label control to observe</param>
        /// <param name="action">Callback receiving the label and new text</param>
        /// <returns>The label control for chaining</returns>
        public static SkiaLabel OnTextChanged(this SkiaLabel control, Action<SkiaLabel, string> action)
        {
            control.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SkiaLabel.Text))
                {
                    action?.Invoke(control, control.Text);
                }
            };

            return control;
        }


        /// <summary>
        /// Sets the Text property for SkiaLabel
        /// </summary>
        public static T WithText<T>(this T label, string text) where T : SkiaLabel
        {
            label.Text = text;
            return label;
        }

        /// <summary>
        /// Sets the ItemsSource property for controls that have it
        /// </summary>
        public static T WithItemsSource<T>(this T control, IList itemsSource) where T : SkiaLayout
        {
            if (control is SkiaLayout itemsControl)
            {
                itemsControl.ItemsSource = itemsSource;
            }
            return control;
        }

        /// <summary>
        /// Sets the IsEnabled property
        /// </summary>
        public static T WithEnabled<T>(this T control, bool enabled) where T : SkiaControl
        {
            control.IsEnabled = enabled;
            return control;
        }

        /// <summary>
        /// Sets the Opacity property
        /// </summary>
        public static T WithOpacity<T>(this T control, double opacity) where T : SkiaControl
        {
            control.Opacity = opacity;
            return control;
        }

        /// <summary>
        /// Sets the Rotation property
        /// </summary>
        public static T WithRotation<T>(this T control, double rotation) where T : SkiaControl
        {
            control.Rotation = rotation;
            return control;
        }

        /// <summary>
        /// Sets the Scale property
        /// </summary>
        public static T WithScale<T>(this T control, double scale) where T : SkiaControl
        {
            control.Scale = scale;
            return control;
        }

        /// <summary>
        /// Sets separate X and Y scale values
        /// </summary>
        public static T WithScale<T>(this T control, double scaleX, double scaleY) where T : SkiaControl
        {
            control.ScaleX = scaleX;
            control.ScaleY = scaleY;
            return control;
        }

        /// <summary>
        /// Sets the Type property for SkiaLayout
        /// </summary>
        public static T WithType<T>(this T layout, LayoutType type) where T : SkiaLayout
        {
            layout.Type = type;
            return layout;
        }

        /// <summary>
        /// Sets the Spacing property for SkiaLayout
        /// </summary>
        public static T WithSpacing<T>(this T layout, double spacing) where T : SkiaLayout
        {
            layout.Spacing = spacing;
            return layout;
        }

        /// <summary>
        /// Sets the ColumnSpacing property for SkiaGrid
        /// </summary>
        public static T WithColumnSpacing<T>(this T grid, double spacing) where T : SkiaGrid
        {
            grid.ColumnSpacing = spacing;
            return grid;
        }

        /// <summary>
        /// Sets the RowSpacing property for SkiaGrid
        /// </summary>
        public static T WithRowSpacing<T>(this T grid, double spacing) where T : SkiaGrid
        {
            grid.RowSpacing = spacing;
            return grid;
        }

    }
}
