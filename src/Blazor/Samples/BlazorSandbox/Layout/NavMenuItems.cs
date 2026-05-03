using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Components.Routing;

namespace BlazorSandbox.Layout;

internal static class NavMenuItems
{
    public static IReadOnlyList<NavMenuSectionDefinition> RootSections { get; } =
    [
        // WELCOME
        new(
            "presentation",
            "Presentation",
            true,
            Items:
            [
                new("Home", string.Empty, "bi-house-door-nav-menu", NavLinkMatch.All)
            ]),

        // THE CANVAS
        new(
            "canvas",
            "The Canvas",
            false,
            Items:
            [
                new("Canvas", "canvas", "bi-aspect-ratio"),
                new("Auto Size", "canvas-auto-size", "bi-arrows-angle-expand"),
                new("Full Size", "canvas-fullscreen", "bi-bounding-box-circles"),
        
                new("Keyboard Input", "keyboard-probe", "bi-keyboard-nav-menu"),
            ]),

        // CONTROLS
        new(
            "controls",
            "Drawn Elements",
            false,
            Children:
            [

                // GRAPHICS
                new(
                    "controls-graphics",
                    "Graphics",
                    false,
                    IsSubgroup: true,
                    Items:
                    [
                        new("SkiaLabel", "labels", "bi-type-nav-menu"),
                        new("SkiaRichLabel", "labels-rich", "bi-text-paragraph-nav-menu"),
                        new("SkiaLabelFps", "labels-fps", "bi-speedometer2-nav-menu"),

                        new("SkiaShape", "graphics-shape", "bi-bounding-box-nav-menu"),
                        new("SkiaSvg", "graphics-svg", "bi-badge-sd-nav-menu"),
                        new("SkiaImage", "graphics-image", "bi-image-nav-menu"),

                        new("SkiaLottie", "lottie-probe", "bi-play-circle-nav-menu"),
                        new("SkiaGif", "gif-probe", "bi-film-nav-menu"),

                        new("SkiaBackdrop", "effects-backdrop", "bi-layers-half-nav-menu"),
                        new("SkiaHoverMask", "effects-hover-mask", "bi-intersect-nav-menu"),
                    ]),

                // Layouts
                new(
                    "controls-layouts",
                    "Layouts",
                    false,
                    IsSubgroup: true,
                    Items:
                    [
                        new("SkiaLayout", "layouts-layout", "bi-layout-text-window-nav-menu"),
                        new("SkiaLayer", "layouts-layer", "bi-layers-nav-menu"),
                        new("SkiaStack", "layouts-stack", "bi-stack-nav-menu"),
                        new("SkiaRow", "layouts-row", "bi-distribute-horizontal-nav-menu"),
                        new("SkiaWrap", "layouts-wrap", "bi-text-wrap-nav-menu"),
                        new("SkiaGrid", "layouts-grid", "bi-grid-nav-menu"),
                        new("SkiaDecoratedGrid", "layouts-decorated-grid", "bi-grid-3x3-gap-nav-menu"),
                        new("SkiaViewSwitcher", "layouts-view-switcher", "bi-window-stack-nav-menu"),
                        new("SkiaTabsSelector", "layouts-tabs-selector", "bi-ui-radios-grid-nav-menu"),

                        new("SkiaScroll", "scrolls", "bi-arrow-down-up-nav-menu"),
                        new("SkiaCarousel", "scrolls-carousel", "bi-view-list-nav-menu"),
                        new("SkiaDrawer", "scrolls-drawer", "bi-layout-sidebar-inset-nav-menu")
                    ]),

                // CONTROLS
                new(
                    "controls-buttons",
                    "Controls",
                    false,
                    IsSubgroup: true,
                    Items:
                    [
                        new("SkiaButton", "buttons", "bi-hand-index-thumb-nav-menu"),
                        new("Custom Button", "buttons-custom", "bi-boxes-nav-menu"),
                        new("Radio Buttons", "buttons-radio", "bi-record-circle-nav-menu"),

                        new("SkiaSwitch", "switches", "bi-toggle2-on-nav-menu"),

                        new("Picker Wheels", "pickers", "bi-list-ul-nav-menu"),
                        new("Picker Spinner", "pickers-spinner", "bi-disc-nav-menu")
                    ]),


            ]),



 



        //TUTORIALS
        new(
            "tutorials",
            "Tutorials",
            false,
            Items:
            [
                new("First App", "tutorial-first-app", "bi-stars-nav-menu"),
                new("Custom Button", "tutorial-custom-button", "bi-dpad-fill-nav-menu"),
                new("News Feed", "tutorial-news-feed", "bi-newspaper-nav-menu"),
                new("Cards", "cards", "bi-columns-gap-nav-menu")
            ]),


        new(
            "advanced",
            "Advanced",
            false,
            Items:
            [
                new("SKMesh", "skmesh-probe", "bi-grid-3x3-gap-fill-nav-menu"),
                new("Projection", "skmesh-projection-probe", "bi-badge-3d-nav-menu"),
                new("Rocket", "skmesh-rocket-probe", "bi-rocket-takeoff-nav-menu")
            ]),

        new(
            "gaming",
            "Gaming",
            false,
            Items:
            [
                new("Space Shooter", "space-shooter", "bi-rocket-takeoff-nav-menu"),
                new("Parallax", "skmesh-parallax-probe", "bi-layers-fill-nav-menu")
            ]),

        //INTERNAL TESTING
        new(
            "testing",
            "Testing",
            false,
            Items:
            [
                new("Fill Direction", "canvas-fill-direction", "bi-arrows-collapse-vertical"),
                new("Full Fill", "canvas-full-fill", "bi-fullscreen"),
                new("Image", "image-probe", "bi-image-nav-menu"),
                new("Shapes", "shapes-probe", "bi-bounding-box-nav-menu"),
                new("Svg", "svg-probe", "bi-badge-sd-nav-menu"),
                new("Scroll", "scroll-probe", "bi-arrow-down-up-nav-menu"),
            ]),
    ];

    public static HashSet<string> CreateDefaultExpandedSections()
    {
        return EnumerateSections(RootSections)
            .Where(section => section.IsExpandedByDefault)
            .Select(section => section.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<NavMenuSectionDefinition> EnumerateSections(IEnumerable<NavMenuSectionDefinition> sections)
    {
        foreach (var section in sections)
        {
            yield return section;

            foreach (var child in EnumerateSections(section.Children))
            {
                yield return child;
            }
        }
    }
}

public sealed record NavMenuSectionDefinition(
    string Key,
    string Title,
    bool IsExpandedByDefault,
    bool IsSubgroup = false,
    IReadOnlyList<NavMenuLinkDefinition>? Items = null,
    IReadOnlyList<NavMenuSectionDefinition>? Children = null)
{
    public IReadOnlyList<NavMenuLinkDefinition> Items { get; init; } = Items ?? Array.Empty<NavMenuLinkDefinition>();

    public IReadOnlyList<NavMenuSectionDefinition> Children { get; init; } = Children ?? Array.Empty<NavMenuSectionDefinition>();
}

public sealed record NavMenuLinkDefinition(
    string Title,
    string Href,
    string IconClass,
    NavLinkMatch Match = NavLinkMatch.Prefix);
