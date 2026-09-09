using DrawnUi.Testing;

// The device-build switches (auto-test driver / motion tracer) are compile-time statics on ChatPage.
// They MUST be off in the harness: the auto-test suite would drive jumps on the SAME pages the repros
// are driving (two drivers, phantom windows, no-op deletes) — repro results become garbage.
DrawnChatList.ChatPage.AutoTestEnabled = false;
DrawnChatList.ChatPage.MotionTraceEnabled = false;


// List reorder gate: ObservableCollection.Move on a templated layout reorders in place — no rebuild,
// no scroll jump, no content-height change — over both measuring strategies.
VirtualizationHarnessDemo.MoveReorderRepro.Run();

// Drag-to-reorder gate: a grip inside a scrolling list must reorder on a vertical drag without the
// scroll stealing the pan, and a pan started off the grip must still scroll and never reorder.
VirtualizationHarnessDemo.GripDragRepro.Run();

// Scroll must not lose its position when a SIBLING resizes: an unconstrained measure pass used to make
// the scroll conclude "content fits" and reset to the top (a scrolled editor list jumped away on save).
VirtualizationHarnessDemo.EditorShapedDragRepro.Run();

// Lift-and-drop reorder: a ghost row carries the item over the list (positioned with Left/Top, which
// offset a cache), the gap travels, a drop inside lands and a drop outside cancels back to the start.
VirtualizationHarnessDemo.LiftDragRepro.Run();

// Engage-on-grow vs the LoadMore that triggered it: 2-col CachedStack, tail add crossing the window
// threshold — content must never shrink and no frame may have an empty visible band.
VirtualizationHarnessDemo.EngageOnLoadMoreRepro.Run();

// Split>1 grid gate: 2-col MeasureFirst grid — every cell in its column slot, initial + range-append
// (the uniform-clone fast path used to stamp a Fill-expanded full-width first cell onto every clone).
VirtualizationHarnessDemo.SplitGridRepro.Run();

// Diagnostic (no assert): plane re-record cadence — how far a SkiaCachedStack scrolls between records
// and why each record fired (drift / dirty bakes / coverage). Split=2 grid, plain + DB cell shapes.
VirtualizationHarnessDemo.PlaneDriftRepro.Run();

// Built-in ItemsSourceWindow regressions (MeasureFirst): Feed preset (recycled binds, slides,
// idle churn, in-use collapse) + window slides/trims/backward refills over a 2000-item source.
VirtualizationHarnessDemo.FeedPresetRepro.Run();
// TELEGRAM lifecycle gate: cold start under threshold -> LoadMore pages -> ENGAGE-ON-GROW anchored
// in place -> incoming Insert(0) glued -> badge jump to newest -> deep paging at true end.
VirtualizationHarnessDemo.TelegramLikeRepro.Run();
// PHASE 2 chat-migration gate: inverted newest-first chat conditions over the BUILT-IN window
// (engage, history flings, head-insert glue, global jumps both ways, live message).
VirtualizationHarnessDemo.BuiltinWindowChatRepro.Run();
VirtualizationHarnessDemo.MeasureFirstWindowRepro.Run();
VirtualizationHarnessDemo.MeasureVisibleWindowRepro.Run();
VirtualizationHarnessDemo.CachedStackWindowRepro.Run();

// parked investigation: subpixel-grid / tearing probe (device "saw" while scrolling); timing-sensitive.
//VirtualizationHarnessDemo.SubpixelGridRepro.Run();

// Repro for the reported "consecutive ScrollToOldest jumps but never scrolls to top" bug. Runs first.
VirtualizationHarnessDemo.StoConsecutiveJumpRepro.Run();
VirtualizationHarnessDemo.PlaneImageStartupRepro.Run();
VirtualizationHarnessDemo.ScrollToTopWallRepro.Run();
VirtualizationHarnessDemo.PlaceholderFlashRepro.Run();
VirtualizationHarnessDemo.DeleteJumpCycleRepro.Run();
VirtualizationHarnessDemo.DeleteGapRepro.Run();
VirtualizationHarnessDemo.PreparedViewsRepro.Run();

// Repro for the reported blank-screen-after-second-jump bug (memo on vs off). Runs first, then returns.
VirtualizationHarnessDemo.BlankJumpRepro.Run();
VirtualizationHarnessDemo.LoadMoreSpinnerRepro.Run();
VirtualizationHarnessDemo.JumpReleaseRepro.Run();
VirtualizationHarnessDemo.CachedJumpRepro.Run();
VirtualizationHarnessDemo.RealChatJumpRepro.Run();
VirtualizationHarnessDemo.ScrollBarOverflowRepro.Run();
VirtualizationHarnessDemo.CachedScrollTrimRepro.Run();
// Double-buffer stale-plane content gate: a slow bake (forced past the 16ms wait) must never serve
// pre-change pixels after a live frame presented newer content (image flicker / typing jump class).
VirtualizationHarnessDemo.StalePlaneContentRepro.Run();
VirtualizationHarnessDemo.AssetResolveRepro.Run();

// Investigation: wrong cell tapped after scrolling under double buffering (stale gesture tree).
// LAST: creates its own hosts; running it first steals the dispatcher pump from the jump repros.
VirtualizationHarnessDemo.TapStaleTreeRepro.Run();

// Frontier catch-up spinner stuck when scrolling before initial measurement completes. Also LAST
// (own host, same dispatcher-pump constraint).
VirtualizationHarnessDemo.FrontierSpinnerStuckRepro.Run();

// Streaming-AI cell growth: bottom-pinned smooth growth at newest + reading position glued when
// scrolled away (StartMockAiAnswer must badge, not yank — same convention as ReceiveMessage).
// LAST: own host + real ChatPage (dispatcher-pump constraint).
VirtualizationHarnessDemo.TypingJumpRepro.Run();
return;

// Headless reconstruction of LoadMoreRepro (static 1000 items, Managed planes).
// Drives scrolling and reports the measurement frontier + whether item measurement happens on the
// render thread (foreground = non-fluid) or on background threadpool threads (fluid).

using var scene = new VirtualizationScene(itemsCount: 1000);
scene.Warmup();

var (wfg, wbg) = scene.DrainMeasures();
Console.WriteLine($"warmup: frontier={scene.List.LastMeasuredIndex} MEASURES fg(render)={wfg} bg(pool)={wbg}");

var probe = new VirtualizationProbe(scene.Host, scene.Scroll, scene.List);
var robot = new GestureRobot(scene.Host);

int totalFg = 0, totalBg = 0;
for (int round = 0; round < 30; round++)
{
    robot.Pan(215, 520, 215, 180, durationMs: 90, steps: 8);
    probe.SettleBackground();
    var (fg, bg) = scene.DrainMeasures();
    totalFg += fg;
    totalBg += bg;

    if (round % 5 == 0 || probe.Frontier >= probe.ItemsCount - 1)
        Console.WriteLine(
            $"round{round,2}: frontier={probe.Frontier,4}/{probe.ItemsCount} offY={probe.OffsetY,7:0} " +
            $"contentH={probe.ContentHeight,7:0}  MEASURES fg(render)={fg,3} bg(pool)={bg,3}");

    if (probe.Frontier >= probe.ItemsCount - 1)
        break;
}

Console.WriteLine();
Console.WriteLine($"FINAL frontier={probe.Frontier}/{probe.ItemsCount}");
Console.WriteLine($"TOTAL scroll-phase MEASURES: fg(render-thread)={totalFg}  bg(threadpool)={totalBg}");
Console.WriteLine(totalFg == 0
    ? "=> GOAL MET: ALL scroll-phase measurement happened in BACKGROUND (zero on render thread)."
    : $"=> NOT MET: {totalFg} cell measurements ran on the render thread during scroll (jank risk).");

scene.Host.SavePng(Path.Combine(AppContext.BaseDirectory, "virt-demo-final.png"));

VirtualizationHarnessDemo.GestureProbe.Run();

Console.WriteLine("done.");
