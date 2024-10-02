using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using P3AddNewFunctionalityDotNetCore.Data;
using P3AddNewFunctionalityDotNetCore.Models;
using P3AddNewFunctionalityDotNetCore.Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace P3AddNewFunctionalityDotNetCore
{
    internal static class DbInitializerExtension
    {
        public static IApplicationBuilder SeedDatabase(this IApplicationBuilder app, IConfiguration config)
        {
            ArgumentNullException.ThrowIfNull(app, nameof(app));

            using var scope = app.ApplicationServices.CreateScope();
            var services = scope.ServiceProvider;
            try
            {
                var context = services.GetRequiredService<P3Referential>();
                context.Database.Migrate();
                var identityContext = services.GetRequiredService<AppIdentityDbContext>();
                identityContext.Database.Migrate();
                SeedData.Initialize(services, config);
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred seeding the DB.");
            }

            return app;
        }
    }
}
