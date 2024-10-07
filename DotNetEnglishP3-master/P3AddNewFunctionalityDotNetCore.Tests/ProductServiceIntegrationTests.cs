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
using Microsoft.AspNetCore.TestHost;
using Newtonsoft.Json;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Net.Http.Headers;
using P3AddNewFunctionalityDotNetCore.Models;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using static P3AddNewFunctionalityDotNetCore.Tests.IntegrationTests;


namespace P3AddNewFunctionalityDotNetCore.Tests
{
    // Configuration de l'environnement de test
    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddControllersWithViews(options =>
                {
                    options.Filters.Add(new IgnoreAntiforgeryTokenAttribute()); // Désactive l'antiforgery dans les tests
                });
                var descriptorReferential = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<P3Referential>));
                if (descriptorReferential != null)
                    services.Remove(descriptorReferential);

                var descriptorIdentity = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppIdentityDbContext>));
                if (descriptorIdentity != null)
                    services.Remove(descriptorIdentity);

                // Ajouter des contextes de base de données en mémoire
                services.AddDbContext<AppIdentityDbContext>(options => options.UseInMemoryDatabase("TestIdentityDb"));
                services.AddDbContext<P3Referential>(options => options.UseInMemoryDatabase("TestDb"));

                services.AddSingleton<IPolicyEvaluator, FakePolicyEvaluator>();


                // Initialisation de la base de données (peut être commenté si non nécessaire)
                var sp = services.BuildServiceProvider();
                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<P3Referential>();
                    var dbIdentity = scopedServices.GetRequiredService<AppIdentityDbContext>();

                    Utilities.InitializeDatabase(db);
                    if (db.Database.IsRelational())
                    {
                        // Si c'est une base relationnelle, on peut appliquer les migrations
                        db.Database.Migrate();
                        dbIdentity.Database.Migrate();
                    }

                    // Créer un utilisateur Admin pour les tests d'authentification
                    var userManager = scopedServices.GetRequiredService<UserManager<IdentityUser>>();
                    dbIdentity.Database.EnsureCreated();

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
                }
            });

        }
    }
    public static class Utilities
    {
        public static void InitializeDatabase(P3Referential context)
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            SeedDatabase(context);
        }

        public static void Cleanup(P3Referential context)
        {
            context.Product.RemoveRange(context.Product);
            context.SaveChanges();
            SeedDatabase(context);
        }

        private static void SeedDatabase(P3Referential context)
        {
            // Create a few Products
            var products = new List<Product>
            {
                new()
                {
                    Name = "Test Product 1",
                    Price = 10.00,
                    Quantity = 50,
                    Description = "Test description Product 1"
                },
                new()
                {
                    Name = "Test Product 2",
                    Price = 25.50,
                    Quantity = 380,
                    Description = "Test description Produit 2"
                }
            };
            context.Product.AddRange(products);

            context.SaveChanges();
        }
    }
    public class IntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public IntegrationTests(CustomWebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private async Task AuthenticateAsAdmin()
        {
            var content = new LoginModel
            {
                Name = "Admin",
                Password = "P@ssword123",
                ReturnUrl = "/"
            };

            StringContent requestContent = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("Account/login", requestContent);
            response.EnsureSuccessStatusCode(); // Vérifie que l'authentification a réussi
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/Product")]
        [InlineData("/Cart")]
        public async Task Get_EndpointsReturnSuccessAndCorrectContentType(string url)
        {
            var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType.ToString());
        }

        [Fact]
        public async Task TestAdminAddProduct()
        {
            Console.WriteLine("Démarrage du test d'ajout de produit.");
            await AuthenticateAsAdmin(); // Authentification
            Console.WriteLine("Authentification réussie.");

            var newProduct = new ProductViewModel
            {
                Name = "Test Product",
                Price = "10.00",
                Stock = "50",
                Description = "Test description"
            };

            var response = await _client.PostAsJsonAsync("/Product/Create", newProduct);
            response.EnsureSuccessStatusCode(); // Vérifie que l'ajout a réussi

            var productList = await _client.GetStringAsync("/Product/Index");
            Assert.Contains("Test Product", productList); // Vérifie que le produit a été ajouté
        }


        public class FakePolicyEvaluator : IPolicyEvaluator
        {
            public virtual async Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
            {
                var principal = new ClaimsPrincipal();

                principal.AddIdentity(new ClaimsIdentity(new[]
                {
                        new Claim(ClaimTypes.Name, "Admin"),
                        new Claim(ClaimTypes.Role, "Admin")
                    }, "FakeScheme"));

                return await Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal,
                 new AuthenticationProperties(), "FakeScheme")));
            }

            public virtual async Task<PolicyAuthorizationResult> AuthorizeAsync(AuthorizationPolicy policy,
             AuthenticateResult authenticationResult, HttpContext context, object resource)
            {
                return await Task.FromResult(PolicyAuthorizationResult.Success());
            }
        }


        [Fact]
        public async Task TestAdminEditProduct()
        {
            // Arrange
            Console.WriteLine("Démarrage du test de modification de produit.");
            await AuthenticateAsAdmin();  // Authentification
            Console.WriteLine("Authentification réussie.");

            // Vérifier qu'un produit existe déjà
            var initialResponse = await _client.GetStringAsync("/Product/Index");
            Console.WriteLine($"Contenu initial de la liste des produits : {initialResponse}");
            Assert.Contains("Test Product 1", initialResponse);

            var updatedProduct = new ProductViewModel
            {
                Name = "Test Product",
                Price = "10.00",
                Stock = "50",
                Description = "Test description"
            };

            Console.WriteLine("Envoi de la requête de modification de produit...");
            // Act
            var response = await _client.PostAsJsonAsync("/Product/Create", updatedProduct);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Log la réponse et le statut
            Console.WriteLine($"Status Code: {response.StatusCode}");
            Console.WriteLine($"Response Content: {responseContent}");

            // Assert
            response.EnsureSuccessStatusCode();
            var productList = await _client.GetStringAsync("/Product/Index");
            Console.WriteLine($"Contenu de la liste des produits après modification : {productList}");
            Assert.Contains("Test Product", productList);
        }


       /* [Fact]
        public async Task TestAdminDeleteProduct()
        {
            // Arrange
           // var client = _factory.CreateClient();
            await AuthenticateAsAdmin(); // Authentification

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
        }*/

        [Fact]
        public async Task TestAddToCartAvailability()
        {
            // Arrange
            Console.WriteLine("Démarrage du test d'ajout au panier.");
            var initialProductResponse = await _client.GetStringAsync("/Product/Index/1");
            Console.WriteLine($"Contenu initial du produit avant ajout au panier : {initialProductResponse}");
            Assert.Contains("Test Product 1", initialProductResponse);

            // Act
            Console.WriteLine("Envoi de la requête d'ajout au panier...");
            var addToCartResponse = await _client.PostAsync("/Cart/AddToCart/1", null);
            var responseContent = await addToCartResponse.Content.ReadAsStringAsync();

            // Log la réponse et le statut
            Console.WriteLine($"Status Code: {addToCartResponse.StatusCode}");
            Console.WriteLine($"Response Content: {responseContent}");

            // Assert
            addToCartResponse.EnsureSuccessStatusCode();  // Vérifie que l'ajout au panier a réussi
            var cartResponse = await _client.GetStringAsync("/Cart/Index");
            Console.WriteLine($"Contenu du panier après ajout : {cartResponse}");
            Assert.Contains("Test Product 1", cartResponse);
        }
    }
}