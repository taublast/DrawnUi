//reusing some code from #dotnetmaui Layout

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrawnUi.Draw
{
    /*

    public partial class SkiaLayout : IList<SkiaControl>
    {

        private void OnRemove(int index, SkiaControl child)
        {

        }

        private void OnUpdate(int index, SkiaControl value, SkiaControl old)
        {

        }

        private void OnAdd(int index, SkiaControl child)
        {

        }

        private void OnInsert(int index, SkiaControl child)
        {

        }

        private void OnClear()
        {

        }

        private List<SkiaControl> _children => this.Views;

        #region IContainer

        /// <summary>
        /// Gets whether this layout is readonly.
        /// </summary>
        public bool IsReadOnly => ((ICollection<SkiaControl>)_children).IsReadOnly;

        public SkiaControl this[int index]
        {
            get => _children[index];
            set
            {
                var old = _children[index];

                if (old == value)
                {
                    return;
                }

                _children.RemoveAt(index);
                if (old is Element oldElement)
                {
                    RemoveLogicalChild(oldElement);
                }

                _children.Insert(index, value);

                if (value is Element newElement)
                {
                    InsertLogicalChild(index, newElement);
                }

                OnUpdate(index, value, old);
            }
        }



        /// <summary>
        /// Gets the child object count in this layout.
        /// </summary>
        public int Count => _children.Count;

        /// <summary>
        /// Returns an enumerator that lists all of the children in this layout.
        /// </summary>
        /// <returns>A <see cref="IEnumerator{T}"/> of type <see cref="SkiaControl"/> with all the children in this layout.</returns>
        public IEnumerator<SkiaControl> GetEnumerator() => _children.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _children.GetEnumerator();

        public override SizeRequest Measure(double widthConstraint, double heightConstraint, MeasureFlags flags = MeasureFlags.None)
        {
            var size = (this as SkiaControl).Measure(widthConstraint, heightConstraint);
            return new SizeRequest(size);
        }

        protected override void InvalidateMeasureOverride()
        {
            base.InvalidateMeasureOverride();
        }

        /// <summary>
        /// Adds a child view to the end of this layout.
        /// </summary>
        /// <param name="child">The child view to add.</param>
        public void Add(SkiaControl child)
        {
            if (child == null)
            {
                return;
            }

            var index = _children.Count;
            _children.Add(child);

            if (child is Element element)
            {
                AddLogicalChild(element);
            }

            OnAdd(index, child);
        }


        /// <summary>
        /// Clears all child views from this layout.
        /// </summary>
        public void Clear()
        {
            for (int i = _children.Count - 1; i >= 0; i--)
            {
                var child = _children[i];
                _children.RemoveAt(i);
                if (child is Element element)
                {
                    RemoveLogicalChild(element);
                }
            }
            OnClear();
        }



        /// <summary>
        /// Determines if the specified child view is contained in this layout.
        /// </summary>
        /// <param name="item">The child view for which to determine if it is contained in this layout.</param>
        /// <returns><see langword="true"/> if <paramref name="item"/> exists in this layout, otherwise <see langword="false"/>.</returns>
        public bool Contains(SkiaControl item)
        {
            return _children.Contains(item);
        }

        /// <summary>
        /// Copies the child views to the specified array.
        /// </summary>
        /// <param name="array">The target array to copy the child views to.</param>
        /// <param name="arrayIndex">The index at which the copying needs to start.</param>
        public void CopyTo(SkiaControl[] array, int arrayIndex)
        {
            _children.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Gets the index of a specified child view.
        /// </summary>
        /// <param name="item">The child view of which to determine the index.</param>
        /// <returns>The index of the specified view, if the view was not found this will return <c>-1</c>.</returns>
        public int IndexOf(SkiaControl item)
        {
            return _children.IndexOf(item);
        }

        /// <summary>
        /// Inserts a child view at the specified index.
        /// </summary>
        /// <param name="index">The index at which to specify the child view.</param>
        /// <param name="child">The child view to insert.</param>
        public void Insert(int index, SkiaControl child)
        {
            if (child == null)
            {
                return;
            }

            _children.Insert(index, child);

            if (child is Element element)
            {
                InsertLogicalChild(index, element);
            }

            OnInsert(index, child);
        }


        /// <summary>
        /// Removes a child view.
        /// </summary>
        /// <param name="child">The child view to remove.</param>
        /// <returns><see langword="true"/> if the view was removed successfully, otherwise <see langword="false"/>.</returns>
        public bool Remove(SkiaControl child)
        {
            if (child == null)
            {
                return false;
            }

            var index = _children.IndexOf(child);

            if (index == -1)
            {
                return false;
            }

            RemoveAt(index);

            return true;
        }

        /// <summary>
        /// Removes a child view at the specified index.
        /// </summary>
        /// <param name="index">The index at which to remove the child view.</param>
        public void RemoveAt(int index)
        {
            if (index >= Count)
            {
                return;
            }

            var child = _children[index];

            _children.RemoveAt(index);

            if (child is Element element)
            {
                RemoveLogicalChild(element);
            }

            OnRemove(index, child);
        }



        #endregion
    }

    */
}
