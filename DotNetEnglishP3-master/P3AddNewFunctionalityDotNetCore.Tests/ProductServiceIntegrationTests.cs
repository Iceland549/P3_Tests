using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using P3AddNewFunctionalityDotNetCore.Data;
using Xunit;
using System.Linq;
using P3AddNewFunctionalityDotNetCore.Models.Entities;
using P3AddNewFunctionalityDotNetCore.Models.ViewModels;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;

namespace P3AddNewFunctionalityDotNetCore.Tests
{
    // Configuration de l'environnement de test
    public class CustomWebApplicationFactory<Program> : WebApplicationFactory<Program> where Program : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Remplacement de la base de données réelle par une base de données en mémoire pour les tests
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<P3Referential>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddDbContext<P3Referential>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForTesting");
                });

                // Création de la base de données pour chaque test
                var sp = services.BuildServiceProvider();
                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<P3Referential>();
                    db.Database.EnsureDeleted(); // Supprime la base de données
                    db.Database.EnsureCreated(); // Crée une nouvelle base de données
                    DbInitializerExtension.InitializeTestData(scopedServices);
                }
            });
        }
    }

    public class IntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public IntegrationTests(CustomWebApplicationFactory<Program> factory)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    DbInitializerExtension.InitializeTestData(services.BuildServiceProvider());
                });
            });
        }
        private async Task AuthenticateAsAdmin(HttpClient client)
        {
            var currentEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            // Données d'authentification de l'administrateur
            var loginData = new Dictionary<string, string>
            {
                { "username", "testadmin" },  
                { "password", "P@ssword456" }  
            };

            // Contenu du formulaire pour l'envoi POST
            var content = new FormUrlEncodedContent(loginData);

            // Envoi de la requête POST à l'endpoint de connexion pour l'authentification
            var response = await client.PostAsync("/Account/LoginForTesting", content);

            // Vérifie si l'authentification a réussi
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw;
            }

            var productList = await client.GetStringAsync("/Product/Index");
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/Product")]
        [InlineData("/Cart")]
        public async Task Get_EndpointsReturnSuccessAndCorrectContentType(string url)
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync(url);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            Assert.Equal("text/html; charset=utf-8",
            response.Content.Headers.ContentType.ToString());
        }

        [Fact]
        public async Task TestAdminAddProduct()
        {
            // Arrange
            var client = _factory.CreateClient();
            await AuthenticateAsAdmin(client); // Authentification

            var newProduct = new ProductViewModel
            {
                Name = "Test Product",
                Price = "10.00",
                Stock = "50",
                Description = "Test description"
            };

            // Act
            var response = await client.PostAsJsonAsync("/Product/Create", newProduct);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            response.EnsureSuccessStatusCode();
            var productList = await client.GetStringAsync("/Product/Index");
            Assert.Contains("Test Product", productList);
        }

        [Fact]
        public async Task TestAdminEditProduct()
        {
            // Arrange
            var client = _factory.CreateClient();
            await AuthenticateAsAdmin(client); // Authentification

            // Vérifier qu'un produit existe déjà
            var initialResponse = await client.GetStringAsync("/Product/Index");
            Assert.Contains("Test Product 1", initialResponse);

            var updatedProduct = new ProductViewModel
            {
                Name = "Test Product",
                Price = "10.00",
                Stock = "50",
                Description = "Test description"
            };

            // Act
            var response = await client.PostAsJsonAsync("/Product/Create", updatedProduct);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            response.EnsureSuccessStatusCode();
            var productList = await client.GetStringAsync("/Product/Index");
            Assert.Contains("Test Product ", productList);
            Assert.Contains("50", productList);
        }

        [Fact]
        public async Task TestAdminDeleteProduct()
        {
            // Arrange
            var client = _factory.CreateClient();
            await AuthenticateAsAdmin(client); // Authentification

            // Vérifier qu'un produit existe avant suppression
            var initialResponse = await client.GetStringAsync("/Product/Index");
            Assert.Contains("Test Product 2", initialResponse);

            // Act
            var deleteResponse = await client.PostAsync("/Product/DeleteProduct/2", null);
            var responseContent = await deleteResponse.Content.ReadAsStringAsync();

            // Assert
            deleteResponse.EnsureSuccessStatusCode();  
            var productListAfterDeletion = await client.GetStringAsync("/Product/Index");
            Assert.DoesNotContain("Test Product 2", productListAfterDeletion);
        }

        [Fact]
        public async Task TestAddToCartAvailability()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Vérifier que le produit existe dans les détails avant l'ajout au panier
            var initialProductResponse = await client.GetStringAsync("/Product/Index/1");
            Assert.Contains("Test Product 1", initialProductResponse);

            // Act
            var addToCartResponse = await client.PostAsync("/Cart/AddToCart/1", null);
            var responseContent = await addToCartResponse.Content.ReadAsStringAsync();

            // Assert
            addToCartResponse.EnsureSuccessStatusCode();  // Vérifier que l'ajout au panier a réussi
            var cartResponse = await client.GetStringAsync("/Cart/Index");
            Assert.Contains("Test Product 1", cartResponse);

            // Vérification que les détails du produit sont mis à jour après ajout au panier
            var updatedProductResponse = await client.GetStringAsync("/Product/Index/1");
            Assert.Contains("Test Product 1", updatedProductResponse);
            //TEST GIT
        }
    }
}