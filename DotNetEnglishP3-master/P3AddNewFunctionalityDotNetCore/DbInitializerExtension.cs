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
    public static class DbInitializerExtension
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
        public static void InitializeTestData(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<P3Referential>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            {
                context.Database.Migrate();
            }
            var users = userManager.Users.ToList();
            foreach (var user in users)
            {
                userManager.DeleteAsync(user).Wait();
            }
            if (!context.Product.Any())
            {
                context.Product.AddRange(
                    new Product { Name = "Test Product 1", Price = 10.00, Quantity = 100 },
                    new Product { Name = "Test Product 2", Price = 20.00, Quantity = 50 }
                );
                context.SaveChanges();
                Console.WriteLine($"Products count after initialization: {context.Product.Count()}");
            }

            Console.WriteLine($"Products count after initialization: {context.Product.Count()}");
            var existingUser = userManager.FindByNameAsync("testadmin").Result;
            if (existingUser == null)
            {
                Console.WriteLine("Creating admin user...");
                var user = new IdentityUser { UserName = "testadmin" };
                var result = userManager.CreateAsync(user, "P@ssword456").Result;
                if (!result.Succeeded)
                {
                    throw new Exception($"Failed to create admin user: {string.Join(", ", result.Errors)}");
                }
            }
        }
    }
}
