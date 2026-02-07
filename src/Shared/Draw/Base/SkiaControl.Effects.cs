using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace DrawnUi.Draw
{
    public partial class SkiaControl
    {
        #region EFFECTS

        private static void EffectsPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl control)
            {

                var skiaEffects = (IEnumerable<SkiaEffect>)newvalue;

                if (oldvalue != null)
                {
                    if (oldvalue is INotifyCollectionChanged oldCollection)
                    {
                        oldCollection.CollectionChanged -= control.EffectsCollectionChanged;
                    }

                    if (oldvalue is IEnumerable<SkiaEffect> oldList)
                    {
                        foreach (var skiaEffect in oldList)
                        {
                            skiaEffect.Dettach();
                        }
                    }
                }

                foreach (var shade in skiaEffects)
                {
                    shade.Attach(control);
                }

                if (newvalue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged -= control.EffectsCollectionChanged;
                    newCollection.CollectionChanged += control.EffectsCollectionChanged;
                }

                control.OnVisualEffectsChanged();
            }
        }

        protected void AttachEffects()
        {
            foreach (var content in this.VisualEffects)
            {
                content.Attach(this);
            }
        }

        private void EffectsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (SkiaEffect newItem in e.NewItems)
                    {
                        newItem.Attach(this);
                    }

                    break;

                case NotifyCollectionChangedAction.Reset:
                case NotifyCollectionChangedAction.Remove:
                    foreach (SkiaEffect oldItem in e.OldItems ?? new SkiaEffect[0])
                    {
                        oldItem.Dettach();
                    }

                    break;
            }

            OnVisualEffectsChanged();
        }

        protected virtual void OnVisualEffectsChanged()
        {
            if (VisualEffects != null)
            {
                EffectsGestureProcessors = VisualEffects.OfType<ISkiaGestureProcessor>().ToList();
                EffectColorFilter = VisualEffects.OfType<IColorEffect>().FirstOrDefault();
                EffectImageFilter = VisualEffects.OfType<IImageEffect>().FirstOrDefault();
                EffectRenderers = VisualEffects.OfType<IRenderEffect>().ToList();
                EffectsState = VisualEffects.OfType<IStateEffect>().ToList();
                EffectPostRenderers = VisualEffects.OfType<IPostRendererEffect>().ToList();
            }

            Update();
        }

        protected List<ISkiaGestureProcessor> EffectsGestureProcessors = new();
        protected List<IStateEffect> EffectsState = new();
        protected List<IRenderEffect> EffectRenderers = new();
        protected IImageEffect EffectImageFilter;
        protected IColorEffect EffectColorFilter;
        public List<IPostRendererEffect> EffectPostRenderers = new();

        public static readonly BindableProperty VisualEffectsProperty = BindableProperty.Create(
            nameof(VisualEffects),
            typeof(IList<SkiaEffect>),
            typeof(SkiaControl),
            defaultValueCreator: (instance) =>
            {
                var created = new ObservableAttachedItemsCollection<SkiaEffect>();
                created.CollectionChanged += ((SkiaControl)instance).EffectsCollectionChanged;
                return created;
            },
            validateValue: (bo, v) => v is IList<SkiaEffect>,
            propertyChanged: EffectsPropertyChanged,
            coerceValue: CoerceVisualEffects);



        public IList<SkiaEffect> VisualEffects
        {
            get => (IList<SkiaEffect>)GetValue(VisualEffectsProperty);
            set => SetValue(VisualEffectsProperty, value);
        }

        private static object CoerceVisualEffects(BindableObject bindable, object value)
        {
            if (!(value is ReadOnlyCollection<SkiaEffect> readonlyCollection))
            {
                return value;
            }
            return new ReadOnlyCollection<SkiaEffect>(
                readonlyCollection.ToList());
        }

        public static readonly BindableProperty DisableEffectsProperty = BindableProperty.Create(nameof(DisableEffects),
            typeof(bool),
            typeof(SkiaControl),
            false, propertyChanged: NeedDraw);
        public bool DisableEffects
        {
            get { return (bool)GetValue(DisableEffectsProperty); }
            set { SetValue(DisableEffectsProperty, value); }
        }

        #endregion







    }
}
