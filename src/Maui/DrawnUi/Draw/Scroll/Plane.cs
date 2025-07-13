namespace DrawnUi.Draw
{
    [DebuggerDisplay("{ToString(),nq}")]
    public class Plane
    {
        public override string ToString()
        {
            return $"Plane {Id}, offset {OffsetX},{OffsetY}, destination {Destination}";
        }

        //sources offsets
        public float OffsetY;
        public float OffsetX;

        public SKColor BackgroundColor { get; set; } = SKColors.Transparent;
        public RenderObject RenderObject { get; set; }
        public SKRect Destination { get; set; }
        public SKRect LastDrawnAt { get; set; }
        public CachedObject CachedObject { get; set; }
        public SKSurface Surface { get; set; }
        public bool IsReady { get; set; } = false;
        public string Id { get; set; }

        /// <summary>
        /// Rendering tree specific to this plane's content, captured during plane preparation.
        /// Immutable snapshot to prevent race conditions during gesture processing.
        /// </summary>
        public IReadOnlyList<SkiaControlWithRect> RenderTree { get; private set; }

        /// <summary>
        /// The scroll position when this plane's render tree was captured.
        /// Used for coordinate transformation during gesture processing.
        /// </summary>
        public SKPoint RenderTreeCaptureOffset { get; private set; }

        /// <summary>
        /// The plane's OffsetY when this plane's render tree was captured.
        /// Used for coordinate transformation during gesture processing.
        /// </summary>
        public float RenderTreeCapturePlaneOffsetY { get; private set; }

        /// <summary>
        /// Captures rendering tree when plane content is prepared
        /// </summary>
        /// <param name="tree">The rendering tree to capture</param>
        /// <param name="captureOffset">The scroll offset when this tree was captured</param>
        /// <param name="planeOffsetY">The plane's OffsetY when this tree was captured</param>
        public void CaptureRenderTree(List<SkiaControlWithRect> tree, SKPoint captureOffset, float planeOffsetY)
        {
            // Create immutable snapshot with frozen binding context and index
            // This ensures that gesture processing uses the binding context that was active when the render tree was captured
            if (tree != null)
            {
                var frozenTree = new List<SkiaControlWithRect>(tree.Count);
                for (int i = 0; i < tree.Count; i++)
                {
                    var item = tree[i];
                    // Create a new record with the frozen binding context and index
                    var frozenItem = new SkiaControlWithRect(
                        item.Control,
                        item.Rect,
                        item.HitRect,
                        item.Index,
                        item.Index, // Set FreezeIndex to the current index
                        item.Control.BindingContext // Capture the current binding context as frozen
                    );
                    frozenTree.Add(frozenItem);
                }
                RenderTree = frozenTree.AsReadOnly();
            }
            else
            {
                RenderTree = null;
            }
            
            RenderTreeCaptureOffset = captureOffset;
            RenderTreeCapturePlaneOffsetY = planeOffsetY;
        }

        public void Reset(SKSurface surface, SKRect source)
        {
            OffsetX = 0;
            OffsetY = 0;
            Surface = surface;
            Invalidate();
        }

        public void Invalidate()
        {
            if (IsReady)
            {
                Debug.WriteLine($"PLANE {Id} invalidated!");

                IsReady = false;
                LastDrawnAt = SKRect.Empty;
                // Don't clear RenderTree - let immutable snapshots be garbage collected naturally
            }
        }
    }
}
