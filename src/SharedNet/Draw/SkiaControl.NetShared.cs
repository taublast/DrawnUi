using System.Collections.Concurrent;

namespace DrawnUi.Draw
{
    public partial class SkiaControl
    {
        #region STYLES

        public static readonly BindableProperty StyleProperty = BindableProperty.Create(
            nameof(Style),
            typeof(Style),
            typeof(SkiaControl),
            null,
            propertyChanged: (b, _, _) =>
            {
                if (b is SkiaControl control)
                    control.ApplyStyles();
            });

        public new Style Style
        {
            get => (Style)GetValue(StyleProperty);
            set => SetValue(StyleProperty, value);
        }

        public void ApplyStyles()
        {
            UpdateLocks++;
            LockUpdate(true);
            ApplyStyle(Style);
            UpdateLocks--;
            Invalidate();
            Update();
        }

        // Per-type resolved styles: single-pass scan, cached until InvalidateStylesCache.
        private static readonly ConcurrentDictionary<Type, (Style[] Inherited, Style Exact)> _resolvedStylesCache = new();

        public void ApplyInitialStyles()
        {
            UpdateLocks++;
            
            var resolved = _resolvedStylesCache.GetOrAdd(this.GetType(), ResolveStylesForType);

            foreach (var s in resolved.Inherited)
                ApplyStyle(s);

            if (resolved.Exact != null)
                ApplyStyle(resolved.Exact);

            ApplyStyle(Style);
            UpdateLocks--;
            Invalidate();
            Update();
        }

        static (Style[] Inherited, Style Exact) ResolveStylesForType(Type thisType)
        {
            var inherited = new List<(int Distance, Style Style)>();
            Style exact = null;

            foreach (var s in StylesCollection.Styles)
            {
                if (s.TargetType == thisType)
                    exact = s;
                else if (s.ApplyToDerivedTypes && thisType.IsSubclassOf(s.TargetType))
                    inherited.Add((StyleInheritanceDistance(s.TargetType, thisType), s));
            }

            if (inherited.Count > 1)
                inherited.Sort((a, b) => b.Distance.CompareTo(a.Distance));

            return (inherited.Count == 0
                ? Array.Empty<Style>()
                : inherited.ConvertAll(x => x.Style).ToArray(), exact);
        }

        static int StyleInheritanceDistance(Type baseType, Type derivedType)
        {
            int d = 0;
            var t = derivedType;
            while (t != null && t != baseType) { t = t.BaseType; d++; }
            return d;
        }

        public static void InvalidateStylesCache()
        {
            lock (lockOptimizeStyle)
            {
                OptimizedStyles.Clear();
                _resolvedStylesCache.Clear();
            }
        }

        // Properties set explicitly by user code — excluded from style application.
        private ConcurrentDictionary<string, bool> ExplicitPropertiesSet { get; } = new();

        public static ConcurrentDictionary<string, List<Setter>> OptimizedStyles = new();

        public IEnumerable<Setter> GetOptimizedSetters(Style style) => OptimizeStyle(style);

        static object lockOptimizeStyle = new();

        IEnumerable<Setter> OptimizeStyle(Style style, List<Setter> optimized = null)
        {
            lock (lockOptimizeStyle)
            {
                bool canCache = false;
                if (optimized == null)
                {
                    if (string.IsNullOrEmpty(style.Class))
                        style.Class = style.TargetType.Name;

                    if (OptimizedStyles.TryGetValue(style.Class, out var cached))
                        return cached;

                    canCache = !string.IsNullOrEmpty(style.Class);
                    optimized = new(style.Setters);
                }

                if (style.BasedOn != null)
                    OptimizeStyle(style.BasedOn, optimized);

                var keys = new HashSet<string>(optimized.Count);
                foreach (var s in optimized) keys.Add(s.Property.PropertyName);
                optimized.AddRange(style.Setters.Where(x => !keys.Contains(x.Property.PropertyName)));

                if (canCache)
                    OptimizedStyles[style.Class] = optimized;

                return optimized;
            }
        }

        public virtual void ApplyStyle(Style style)
        {
            if (style == null)
                return;

            var thisType = this.GetType();
            if (style.TargetType != thisType &&
                !(style.ApplyToDerivedTypes && thisType.IsSubclassOf(style.TargetType)))
            {
                throw new ApplicationException($"Style {style.Class} for {thisType} [{this.Tag}] has incorrect target type!");
            }

            foreach (Setter setter in GetOptimizedSetters(style))
            {
                if (!ExplicitPropertiesSet.ContainsKey(setter.Property.PropertyName))
                {
                    isApplyingStyle = true;
                    SetPropertyValue(setter.Property, setter.Value);
                    isApplyingStyle = false;
                }
            }
        }

        protected volatile bool isApplyingStyle;

        partial void SetValueFromStyleDefault(BindableProperty property, object value, ref bool handled)
        {
            isApplyingStyle = true;
            SetValue(property, value);
            isApplyingStyle = false;
            handled = true;
        }

        // Tracks which properties were set explicitly so style application skips them.
        protected void TrackExplicitPropertyChange(string propertyName)
        {
            if (!isApplyingStyle && !string.IsNullOrEmpty(propertyName))
                ExplicitPropertiesSet[propertyName] = true;
        }

        public T GetStyleValue<T>(Style style, BindableProperty property, IEnumerable<Setter> styleSetters = null)
        {
            if (styleSetters == null)
                styleSetters = GetOptimizedSetters(style);
            var setter = styleSetters.FirstOrDefault(p => p.Property == property);
            if (setter != null)
                return (T)setter.Value;
            return default;
        }

        public IEnumerable<Setter> ApplyStyleProperty(Style style, BindableProperty property, IEnumerable<Setter> styleSetters = null)
        {
            if (styleSetters == null)
                styleSetters = GetOptimizedSetters(style);
            var setter = styleSetters.FirstOrDefault(p => p.Property == property);
            if (setter != null)
                SetPropertyValue(setter.Property, setter.Value);
            return styleSetters;
        }

        #endregion
    }
}
