using System;
using System.Collections.Generic;
using System.Text;
using PassengerManager.Shared.Protos;

namespace PassengerManager.Client.Driver.Stores
{
    public class ManifestStore
    {
        public List<RouteOption> AvailableRoutes { get; set; } = new();
        
        public RouteOption? SelectedRoute { get; set; }

        public void Clear()
        {
            AvailableRoutes.Clear();
            SelectedRoute = null;
        }
    }
}
