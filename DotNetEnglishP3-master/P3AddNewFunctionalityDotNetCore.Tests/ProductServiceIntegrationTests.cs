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
            Console.WriteLine("Starting AuthenticateAsAdmin method");

            // Données d'authentification de l'administrateur
            var loginData = new Dictionary<string, string>
            {
                { "username", "testadmin" },  // Nom d'utilisateur de l'administrateur
                { "password", "P@ssword456" }  // Mot de passe de l'administrateur
            };
            Console.WriteLine($"Login data prepared: username={loginData["username"]}");

            // Contenu du formulaire pour l'envoi POST
            var content = new FormUrlEncodedContent(loginData);
            Console.WriteLine("FormUrlEncodedContent created");

            // Envoi de la requête POST à l'endpoint de connexion pour l'authentification
            Console.WriteLine("Sending POST request to /Account/LoginForTesting");
            var response = await client.PostAsync("/Account/LoginForTesting", content);
            Console.WriteLine($"Response received. Status code: {response.StatusCode}");

            // Vérifie si l'authentification a réussi
            try
            {
                response.EnsureSuccessStatusCode();
                Console.WriteLine("Authentication successful");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Authentication failed. Exception: {ex.Message}");
                throw;
            }

            Console.WriteLine("Fetching product list from /Product/Index");
            var productList = await client.GetStringAsync("/Product/Index");
            Console.WriteLine($"Product list fetched. Length: {productList.Length}");


            var currentEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            Console.WriteLine($"Current environment during test: {currentEnv}");

            Console.WriteLine("AuthenticateAsAdmin method completed");
        }
        [Theory]
        [InlineData("/")]
        [InlineData("/Product")]
        [InlineData("/Cart")]
        public async Task Get_EndpointsReturnSuccessAndCorrectContentType(string url)
        {
            Console.WriteLine($"Current environment during test: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");
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
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

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
            // Log de la réponse
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response Content: {responseContent}");
            // Assert
            response.EnsureSuccessStatusCode();
            var productList = await client.GetStringAsync("/Product/Index");
            Assert.Contains("Test Product", productList);
        }
        [Fact]
        public void Test_Environment_Is_Set_To_Testing()
        {
            // Arrange
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            var expectedEnvironment = "Testing";

            // Act
            var actualEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            // Assert
            Assert.Equal(expectedEnvironment, actualEnvironment);
        }
        [Fact]
        public async Task TestAdminEditProduct()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

            // Arrange
            var client = _factory.CreateClient();
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            Console.WriteLine($"Current environment: {environment}");
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
            Console.WriteLine($"Response Content: {responseContent}");

            // Assert
            response.EnsureSuccessStatusCode();
            var productList = await client.GetStringAsync("/Product/Index");
            Assert.Contains("Test Product ", productList);
            Assert.Contains("50", productList);
        }
        [Fact]
        public async Task TestAdminDeleteProduct()
        {
            Console.WriteLine("[AdminProductDel] Début du test de suppression de produit");
            var client = _factory.CreateClient();
            await AuthenticateAsAdmin(client);
            Console.WriteLine("[AdminProductDel] Authentification admin réussie");

            var initialResponse = await client.GetStringAsync("/Product/Index");
            Console.WriteLine($"[AdminProductDel] Contenu initial de la page produits : {initialResponse.Substring(0, Math.Min(initialResponse.Length, 200))}...");

            Console.WriteLine("[AdminProductDel] Tentative de suppression du produit 2");
            var deleteResponse = await client.PostAsync("/Product/Delete/2", null);
            Console.WriteLine($"[AdminProductDel] Statut de la réponse de suppression : {deleteResponse.StatusCode}");

            var clientResponse = await client.GetStringAsync("/Product/Index");
            Console.WriteLine($"[AdminProductDel] Contenu de la page produits après suppression : {clientResponse.Substring(0, Math.Min(clientResponse.Length, 200))}...");

            Console.WriteLine("[AdminProductDel] Fin du test de suppression de produit");
        }
        [Fact]
        public async Task TestAddToCartAvailability()
        {
            Console.WriteLine("[AddToCart] Début du test d'ajout au panier");
            var client = _factory.CreateClient();

            var initialProductResponse = await client.GetStringAsync("/Product/Details/1");
            Console.WriteLine($"[AddToCart] Détails initiaux du produit : {initialProductResponse.Substring(0, Math.Min(initialProductResponse.Length, 200))}...");

            Console.WriteLine("[AddToCart] Tentative d'ajout du produit 1 au panier");
            var addToCartResponse = await client.PostAsync("/Cart/AddToCart/1", null);
            Console.WriteLine($"[AddToCart] Statut de la réponse d'ajout au panier : {addToCartResponse.StatusCode}");

            var cartResponse = await client.GetStringAsync("/Cart/Index");
            Console.WriteLine($"[AddToCart] Contenu du panier : {cartResponse.Substring(0, Math.Min(cartResponse.Length, 200))}...");

            var updatedProductResponse = await client.GetStringAsync("/Product/Details/1");
            Console.WriteLine($"[AddToCart] Détails du produit après ajout au panier : {updatedProductResponse.Substring(0, Math.Min(updatedProductResponse.Length, 200))}...");

            Console.WriteLine("[AddToCart] Fin du test d'ajout au panier");
        }
        [Fact]
        public async Task TestProductConsistency()
        {
            Console.WriteLine("[AdminProductConsistency] Début du test de cohérence");
            var client = _factory.CreateClient();
            await AuthenticateAsAdmin(client);
            Console.WriteLine("[AdminProductConsistency] Authentification admin réussie");

            var initialResponse = await client.GetStringAsync("/Product/Index");
            Console.WriteLine($"[AdminProductConsistency] Liste initiale des produits : {initialResponse.Substring(0, Math.Min(initialResponse.Length, 200))}...");

            var newProduct = new ProductViewModel
            {
                Name = "Consistency Test Product",
                Price = "25.99",
                Stock = "100",
                Description = "Test description"
            };
            Console.WriteLine($"[AdminProductConsistency] Tentative d'ajout du produit : {newProduct.Name}");
            var addResponse = await client.PostAsJsonAsync("/Product/Create", newProduct);
            Console.WriteLine($"[AdminProductConsistency] Statut de la réponse d'ajout : {addResponse.StatusCode}");

            var productListAfterAdd = await client.GetStringAsync("/Product/Index");
            Console.WriteLine($"[AdminProductConsistency] Liste des produits après ajout : {productListAfterAdd.Substring(0, Math.Min(productListAfterAdd.Length, 200))}...");

            Console.WriteLine("[AdminProductConsistency] Tentative de suppression du produit ajouté");
            var deleteResponse = await client.PostAsync($"/Product/Delete/3", null);
            Console.WriteLine($"[AdminProductConsistency] Statut de la réponse de suppression : {deleteResponse.StatusCode}");

            var productListAfterDelete = await client.GetStringAsync("/Product/Index");
            Console.WriteLine($"[AdminProductConsistency] Liste des produits après suppression : {productListAfterDelete.Substring(0, Math.Min(productListAfterDelete.Length, 200))}...");

            Console.WriteLine("[AdminProductConsistency] Fin du test de cohérence");
        }
    }
}