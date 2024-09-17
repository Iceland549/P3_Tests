using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Xunit;
using P3AddNewFunctionalityDotNetCore.Models.Services;
using P3AddNewFunctionalityDotNetCore.Models.ViewModels;
using P3AddNewFunctionalityDotNetCore.Models.Entities;
using P3AddNewFunctionalityDotNetCore.Models;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using P3AddNewFunctionalityDotNetCore.Data;
using Microsoft.Extensions.Logging;

namespace P3AddNewFunctionalityDotNetCore.Tests
{
    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContext));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<P3Referential>(options => options.UseInMemoryDatabase("TestDb"));
                services.AddDbContext<AppIdentityDbContext>(options => options.UseInMemoryDatabase("TestIdentityDb"));

                // Configuration d'Identity
                services.AddDefaultIdentity<IdentityUser>()
                    .AddEntityFrameworkStores<AppIdentityDbContext>()
                    .AddDefaultTokenProviders();

                // Initialisation de la base de données
                var sp = services.BuildServiceProvider();
                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<P3Referential>();
                    var logger = scopedServices.GetRequiredService<ILogger<CustomWebApplicationFactory<TProgram>>>();
                    var userManager = scopedServices.GetRequiredService<UserManager<IdentityUser>>();

                    db.Database.EnsureCreated();

                    // Création d'un utilisateur Admin
                    try
                    {
                        if (userManager.FindByNameAsync("Admin").Result == null)
                        {
                            var user = new IdentityUser
                            {
                                UserName = "Admin",
                                Email = "admin@example.com",
                                EmailConfirmed = true
                            };
                            var result = userManager.CreateAsync(user, "P@ssword123").Result;
                            if (!result.Succeeded)
                            {
                                throw new Exception("Impossible de créer l'utilisateur Admin");
                            }
                        }
                        // Ajout de données de test dans la base de données
                        if (!db.Product.Any())
                        {
                            db.Product.Add(new Product { Name = "Test Product", Price = 10.00, Quantity = 100 });
                            db.SaveChanges();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Une erreur s'est produite lors de l'initialisation de la base de données. Erreur : {Message}", ex.Message);
                    }
                }
            });
        }
    }

    public class ProductIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<Program> _factory;

        // Authentification comme Admin
        private async Task AuthenticateAsAdmin()
        {
            var loginData = new Dictionary<string, string>
            {
                {"Name", "Admin"},
                {"Password", "P@ssword123"}
            };
            var content = new FormUrlEncodedContent(loginData);
            var response = await _client.PostAsync("/Account/Login", content);

            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Status Code: {response.StatusCode}");
            Console.WriteLine($"Response Content: {responseContent}");
            response.EnsureSuccessStatusCode();
        }

        public ProductIntegrationTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [Fact]
        public async Task CanAddProduct_AsAdmin()
        {
            // Arrange
            await AuthenticateAsAdmin();
            var newProduct = new ProductViewModel
            {
                Name = "New Test Product",
                Price = "15.00",
                Stock = "150",
                Description = "New Test Description"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/Product/Create", newProduct);

            // Assert
            response.EnsureSuccessStatusCode();
            var productList = await _client.GetStringAsync("/Product/Index");
            Assert.Contains("New Test Product", productList);

            using (var scope = _factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<P3Referential>();
                var addedProduct = await dbContext.Product.FirstOrDefaultAsync(p => p.Name == "New Test Product");
                Assert.NotNull(addedProduct);
                Assert.Equal(15.00, addedProduct.Price, 2);
                Assert.Equal(150, addedProduct.Quantity);
            }
        }
    }
}