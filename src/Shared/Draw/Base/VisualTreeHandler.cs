using System.Xml.Linq;

namespace DrawnUi.Draw
{
    public class VisualTreeHandler
    {
        #region DEBUG

        public void DumpPreparedTree()
        {
            if (PreparedTree!=null)
                DumpTree(PreparedTree);
        }

        public void DumpActiveTree()
        {
            if (ActiveTree!=null)
                DumpTree(ActiveTree);
        }

        public void DumpTree(VisualLayer node, string prefix = "", bool isLast = true, int level = 0)
        {
            string indent = new string(' ', level * 4);

            // Box drawing characters for tree structure
            string connector = isLast ? "└─ " : "├─ ";
            string childPrefix = isLast ? "   " : "│  ";

            // Print the current node with appropriate prefix

            var controlInfo = node.Control.GetType().Name;
            if (node.Control is SkiaLayout layout)
            {
                controlInfo += $" {layout.Type}";
            }

            var line =
                $"{indent}{prefix}{connector}{controlInfo} at {node.HitBoxWithTransforms.Pixels.Location} ({node.Children.Count})";

            if (node.Cached != SkiaCacheType.None)
            {
                line += $" [{node.Cached}]";
            }

            if (!string.IsNullOrEmpty(node.Control.Tag))
            {
                line += $" - '{node.Control.Tag}'";
            }

            if (!string.IsNullOrEmpty(node.Control.Tag))
            {
                line += $" by {node.Control.Tag}";
            }

            Trace.WriteLine(line);

            // Process children
            for (int i = 0; i < node.Children.Count; i++)
            {
                bool childIsLast = (i == node.Children.Count - 1);
                DumpTree(node.Children[i], prefix + childPrefix, childIsLast, level);
            }
        }

        #endregion

        public bool IsReady
        {
            get
            {
                return PreparedTree != null;
            }
        }

        public bool WasRendered
        {
            get
            {
                return ActiveTree != null;
            }
        }

        /// <summary>
        /// This is used for rendering
        /// </summary>
        protected VisualLayer ActiveTree;

        /// <summary>
        /// This is prepared and can be used to replace ActiveTree
        /// </summary>
        protected VisualLayer PreparedTree;

        /// <summary>
        /// STEP 1 (or Background thread) prepare rendering tree that will be used for rendering later.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="context"></param>
        public void PrepareRenderingTree(DrawingContext context, float widthRequest, float heightRequest, SkiaControl root)
        {
            // Build visual tree with transforms and caches
            var node = root.PrepareNode(context, widthRequest, heightRequest);

            var previousPrepared = PreparedTree;

            // Push new tree
            PreparedTree = node;

            if (previousPrepared != null
                && !ReferenceEquals(previousPrepared, ActiveTree)
                && !ReferenceEquals(previousPrepared, PreparedTree))
            {
                DisposeDetachedTree(previousPrepared);
            }
        }

        /// <summary>
        /// STEP 2 (or Main thread) use prepared rendering tree to draw its nodes
        /// </summary>
        /// <param name="node"></param>
        /// <param name="context"></param>
        public void Render(DrawingContext context)
        {
            var prepared = PreparedTree;
            if (prepared != null && !ReferenceEquals(prepared, ActiveTree))
            {
                var previousActive = ActiveTree;
                ActiveTree = prepared;

                if (previousActive != null && !ReferenceEquals(previousActive, PreparedTree))
                {
                    DisposeDetachedTree(previousActive);
                }
            }

            RenderTreeInternal(context, ActiveTree);
        }

        private static void DisposeDetachedTree(VisualLayer node)
        {
            if (node == null)
            {
                return;
            }

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    DisposeDetachedTree(child);
                }

                node.Children.Clear();
            }

            if (node.Cache != null)
            {
                var cache = node.Cache;
                node.Cache = null;

                if (node.Control != null)
                {
                    node.Control.DisposeObject(cache);
                }
                else
                {
                    cache.Dispose();
                }
            }
        }

        /// <summary>
        /// Used by STEP 2 RenderTree method
        /// </summary>
        /// <param name="context"></param>
        /// <param name="node"></param>
        protected void RenderTreeInternal(DrawingContext context, VisualLayer node)
        {
            if (node != null)
            {
                node.Render(context);

                if (node.Cache == null)
                {
                    foreach (var child in node.Children)
                    {
                        RenderTreeInternal(context, child);
                    }
                }
            }
        }




       

    }


    /*
    public class VisualTreeHandler2
    {
        public VisualNodePreparing PreparedLogicalTree { get; private set; }
        public VisualNode FrozenLogicalTree { get; private set; }

        /// <summary>
        /// STEP 1: Prepare logical rendering tree that maintains complete hierarchy
        /// </summary>
        public void PrepareLogicalTree(DrawingContext context, float widthRequest, float heightRequest, SkiaControl root)
        {
            // Build complete logical tree with transforms and caches
            var node = root.PrepareLogicalNode(context, widthRequest, heightRequest);

            // Update parent-child relationships and transforms
            if (node != null)
            {
                UpdateParentChildRelationships(node);
            }

            PreparedLogicalTree = node;

            // Create immutable snapshot for thread-safe operations
            if (node != null)
            {
                FrozenLogicalTree = VisualNode.FromPreparing(node);
            }
        }

        /// <summary>
        /// Updates parent-child transform relationships after tree is built
        /// </summary>
        private void UpdateParentChildRelationships(VisualNodePreparing node, VisualNodePreparing parent = null)
        {
            if (parent != null)
            {
                node.TransformsTotal = parent.TransformsTotal.PostConcat(node.Transforms);
                node.OpacityTotal = parent.OpacityTotal * (node.Control?.Opacity ?? 1.0);
                node.HitBoxWithTransforms = ScaledRect.FromPixels(
                    VisualLayerDraft.TransformRect(node.HitBox.Pixels, node.TransformsTotal),
                    node.HitBox.Scale);
            }

            foreach (var child in node.Children)
            {
                UpdateParentChildRelationships(child, node);
            }
        }

        public void RenderLogical(DrawingContext context)
        {
            Super.Log("--------------------------------------------------------------------------");
            FrozenLogicalTree?.Render(context);
            Super.Log("--------------------------------------------------------------------------");
            DumpPreparedLogicalTree(FrozenLogicalTree);
            Super.Log("--------------------------------------------------------------------------");
        }

        public void DumpPreparedLogicalTree(VisualNode node, string prefix = "", bool isLast = true, int level = 0)
        {
            if (node == null)
            {
                Super.Log("[DumpPreparedLogicalTree] root node is NULL");
                return;
            }

            string indent = new string(' ', level * 4);
            string connector = isLast ? "└─ " : "├─ ";
            string childPrefix = isLast ? "   " : "│  ";

            var line =
                $"{indent}{prefix}{connector}{node.Control.GetType()} at {node.HitBoxWithTransforms.Pixels.Location} ({node.Children.Length})";

            if (node.Cache!=null)
            {
                line += $" [{node.Cache.Type}]";
            }

            if (!string.IsNullOrEmpty(node.Tag))
            {
                line += $" \"{node.Tag}\"";
            }

            Super.Log(line);

            for (int i = 0; i < node.Children.Length; i++)
            {
                bool childIsLast = (i == node.Children.Length - 1);
                DumpPreparedLogicalTree(node.Children[i], prefix + childPrefix, childIsLast, level);
            }
        }

        /// <summary>
        /// Hit testing using the logical tree
        /// </summary>
        public SkiaControl HitTest(SKPoint point)
        {
            var hit = FrozenLogicalTree?.HitTest(point);
            return hit?.Control;
        }
    }
    */
}
