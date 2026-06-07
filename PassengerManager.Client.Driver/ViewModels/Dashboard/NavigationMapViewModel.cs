using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Widgets;
using Mapsui.Widgets.InfoWidgets;
using NetTopologySuite.Geometries;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Client.Core.ViewModels;
using PassengerManager.Shared.Protos;
using Color = Mapsui.Styles.Color;

namespace PassengerManager.Client.Driver.ViewModels.Dashboard
{
    public partial class NavigationMapViewModel : BaseViewModel
    {
        private readonly DriverOpsService.DriverOpsServiceClient _driverOpsClient;
        private readonly DriverAccountStore _driverAccountStore;

        [ObservableProperty] private Map _mapData;

        public NavigationMapViewModel(
            INavigationService navigationService,
            DriverOpsService.DriverOpsServiceClient driverOpsClient,
            DriverAccountStore driverAccountStore) : base(navigationService, driverAccountStore)
        {
            _driverOpsClient = driverOpsClient;
            _driverAccountStore = driverAccountStore;

            MapData = new();
           
            var osmTileSource = new BruTile.Web.HttpTileSource(
                new BruTile.Predefined.GlobalSphericalMercator(),
                "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
                name: "OSM",
                configureHttpRequestMessage: request =>
                {
                    request.Headers.TryAddWithoutValidation("User-Agent", "PassengerManager.DriverApp/1.0");
                }
            );

            MapData.Layers.Add(new Mapsui.Tiling.Layers.TileLayer(osmTileSource));

            var perfWidget = MapData.Widgets.OfType<Mapsui.Widgets.InfoWidgets.PerformanceWidget>().FirstOrDefault();
            if (perfWidget != null)
                perfWidget.Performance.IsActive = ActiveMode.No;

            var logWidget = MapData.Widgets.OfType<LoggingWidget>().FirstOrDefault();
            if (logWidget != null)
                LoggingWidget.ShowLoggingInMap = ActiveMode.No;

            SetDefaultCamera();
            _ = LoadTripShapeAsync();
        }

        private void SetDefaultCamera()
        {
            var center = SphericalMercator.FromLonLat(30.5233, 50.4500);
            MapData.Navigator.CenterOn(new MPoint(center.x, center.y));
            MapData.Navigator.ZoomTo(150);
        }

        private async Task LoadTripShapeAsync()
        {
            if (string.IsNullOrEmpty(_driverAccountStore.CurrentTripId))
                return;

            var request = new GetTripShapeRequest
            {
                TripId = _driverAccountStore.CurrentTripId
            };

            var response = await _driverOpsClient.GetTripShapeAsync(request);

            if (response.Points.Count > 0)
            {
                RenderShapeOverlay(response.Points, response.ColorHex);
            }
        }

        private void RenderShapeOverlay(IEnumerable<ShapePoint> points, string colorHex)
        {
            var coordinates = points.Select(p =>
            {
                var mercator = SphericalMercator.FromLonLat(p.Longitude, p.Latitude);
                return new Coordinate(mercator.x, mercator.y);
            }).ToArray();

            var lineString = new LineString(coordinates);
            var feature = new GeometryFeature(lineString);

            Mapsui.Styles.Color lineColor;
            try
            {
                lineColor = Mapsui.Styles.Color.FromString(colorHex);
            }
            catch (Exception e)
            {
                lineColor = Mapsui.Styles.Color.Blue;
            }

            var shapeLayer = new MemoryLayer("TripRouteLayer")
            {
                Features = new[] { feature },
                Style = new VectorStyle
                {
                    Line = new Mapsui.Styles.Pen
                    {
                        Color = lineColor,
                        Width = 8
                    }
                }
            };

            MapData.Layers.Add(shapeLayer);

            if (shapeLayer.Extent != null)
                MapData.Navigator.ZoomToBox(shapeLayer.Extent);
        }
    }
}
