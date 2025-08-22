using AppoMobi.Specials;
using BruTile;
using BruTile.Cache;
using BruTile.Predefined;
using BruTile.Web;
using DrawnUi.MapsUi;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.Tiling.Fetcher;
using Mapsui.Tiling.Layers;
using Mapsui.Tiling.Rendering;
using Mapsui.Widgets.ButtonWidgets;

namespace PreviewTests.Views
{
    [Preview<SkiaMapsUi>]
    public partial class MapsUiPage
    {

        public MapsUiPage()
        {
            try
            {
                InitializeComponent();

                //avoid setting context BEFORE initialize component, can bug 
                //having parent context still null when constructing from xaml

                BindingContext = new MainPageViewModel();
            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
                Console.WriteLine(e);
            }
        }

        bool once;

        public static class OpenCustomStreetMap
        {
            public class CustomizedLayer : TileLayer
            {
                public CustomizedLayer(ITileSource tileSource, int minTiles = 200, int maxTiles = 300, IDataFetchStrategy dataFetchStrategy = null,
                    IRenderFetchStrategy renderFetchStrategy = null,
                    int minExtraTiles = -1, int maxExtraTiles = -1)
                    : base(tileSource, minTiles, maxTiles, dataFetchStrategy, renderFetchStrategy, minExtraTiles, maxExtraTiles, null)
                {
                }
            }

            public static IPersistentCache<byte[]>? DefaultCache = null;

            private static readonly BruTile.Attribution OpenStreetMapAttribution = new(
                "OpenStreetMap", "https://www.openstreetmap.org/copyright");

            public static TileLayer CreateTileLayer(string? userAgent = null)
            {
                userAgent ??= $"user-agent-of-{Path.GetFileNameWithoutExtension(System.AppDomain.CurrentDomain.FriendlyName)}";

                return new OpenCustomStreetMap.CustomizedLayer(CreateTileSource(userAgent))
                {
                    Name = "OpenStreetMap"
                };
            }

            private static HttpTileSource CreateTileSource(string userAgent)
            {
                var source = new HttpTileSource(new GlobalSphericalMercator(),
                    "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
                    new[] { "a", "b", "c" }, name: "OpenStreetMap",
                    attribution: OpenStreetMapAttribution);

                return source;
            }
        }

        /*
        private static HttpTileSource CreateTileSource(string userAgent)
        {
            var source = new CustomizeDownloadedTiles(new GlobalSphericalMercator(),
                "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
                new[] { "a", "b", "c" }, name: "OpenStreetMap",
                attribution: OpenStreetMapAttribution, userAgent: userAgent, persistentCache: DefaultCache);


            var filterTint = SkiaImageEffects.Tint(Colors.Blue.WithAlpha(0.13f), SKBlendMode.SrcATop);

            var filterDesaturate = ChainSaturationEffect.CreateSaturationFilter(0.2f);

            source.RenderBitmap = (renderAction, canvas) =>
            {
                using SKPaint paintTint = new SKPaint()
                {
                    ColorFilter = filterTint
                };

                using SKPaint paintDesaturate = new SKPaint()
                {
                    ColorFilter = filterDesaturate
                };

                //apply main filter
                var restore = canvas.SaveLayer(paintTint);

                //miltiple chain..
                //canvas.SaveLayer(paintDesaturate);
                //other chains
                //renderAction(null);

                renderAction(paintDesaturate);

                canvas.RestoreToCount(restore);
            };

            return source;
        }
    }
    */

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (!once)
            {
                once = true;

                var map = mapControl.Map;

                map.CRS = "EPSG:3857";

                //mapControl.Renderer.StyleRenderers.TryGetValue(typeof(RasterStyle), out var existing);
                //mapControl.Renderer.StyleRenderers[typeof(RasterStyle)] = new DrawnUiRasterStyleRenderer();

                var _layer = OpenCustomStreetMap.CreateTileLayer("drawnui-sandbox");

                map.Layers.Clear();
                map.Layers.Add(_layer);

                map.Widgets.Clear();
                map.Widgets.Add(new Mapsui.Widgets.ScaleBar.ScaleBarWidget(mapControl.Map)
                {
                    TextAlignment = Mapsui.Widgets.Alignment.Center,
                    HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Center,
                    VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Bottom,
                    Margin = new(16, 75),
                });

                //adds the +/- zoom widget
                map.Widgets.Add(new ZoomInOutWidget
                {
                    Margin = new(16, 75),
                });

                //mapControl.Map = map;
                //48.854985, 2.311670
                //mapControl.WasFirstTimeDrawn += (s, a) =>
                //{
                //    var point = new MPoint(48.856663, 2.351556);
                //    var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(point.X, point.Y).ToMPoint();

                //    mapControl.Map.Navigator.CenterOnAndZoomTo(
                //        sphericalMercatorCoordinate, mapControl.Map.Navigator.Resolutions[2]);
                //};

                mapControl?.Refresh();

                Tasks.StartDelayed(TimeSpan.FromSeconds(2), () =>
                {
                    var point = new MPoint(2.351556, 48.856663);
                    var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(point.X, point.Y).ToMPoint();

                    mapControl.Map.Navigator.CenterOnAndZoomTo(
                        sphericalMercatorCoordinate,
                        mapControl.Map.Navigator.Resolutions[10]);
                });

            }

        }


    }
}
