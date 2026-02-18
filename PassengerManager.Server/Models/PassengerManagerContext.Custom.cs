using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PassengerManager.Shared.Models;

namespace PassengerManager.Server.Models;

public partial class PassengerManagerContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRole>().HasData
        (
            new UserRole
            {
                RoleId = 1,
                RoleName = "Administrator",
                AccessLevel = 100,
                DefaultWindow = "AdminDashboard",
                Description = "Full database control. Developed for persons responsible for transportation management, such as within the city council."
            },

            new UserRole
            {
                RoleId = 2,
                RoleName = "Dispatcher",
                AccessLevel = 50,
                DefaultWindow = "DispatcherDashboard",
                Description = "Incident management control. Developed for persons responsible for transportation monitoring, such as depot dispatchers."
            },

            new UserRole
            {
                RoleId = 3,
                RoleName = "Driver",
                AccessLevel = 10,
                DefaultWindow = "DriverView",
                Description = "Current route control. Developed for use in tablets inside driver compartments of transportational vehicles."
            }
        );
    }
}
