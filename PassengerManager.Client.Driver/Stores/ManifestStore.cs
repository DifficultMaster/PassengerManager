using System;
using System.Collections.Generic;
using System.Text;
using PassengerManager.Shared.Protos;

namespace PassengerManager.Client.Driver.Stores
{
    public class ManifestStore
    {
        public List<RouteOption> AvailableRoutes { get; set; } = new();

        private RouteOption? _selectedRoute;

        public RouteOption? SelectedRoute
        {
            get => _selectedRoute;
            set
            {
                _selectedRoute = value;
                OnSelectedRouteChanged?.Invoke();
            }
        }

        public event Action? OnSelectedRouteChanged;

        public void Clear()
        {
            AvailableRoutes.Clear();
            SelectedRoute = null;
        }
    }
}
