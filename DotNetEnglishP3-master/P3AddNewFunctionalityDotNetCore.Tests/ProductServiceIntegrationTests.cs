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


namespace P3AddNewFunctionalityDotNetCore.Tests
{
    // Configuration de l'environnement de test
    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
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
                }
            });
        }
    }

    public class IntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        public IntegrationTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IPolicyEvaluator, FakePolicyEvaluator>();
                });
            }).CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public static StringContent BuildRequestContent<T>(T content)
        {
            string serialized = JsonConvert.SerializeObject(content);

            return new StringContent(serialized, Encoding.UTF8, "application/json");
        }

        // Authentification comme Admin
        private async Task AuthenticateAsAdmin()
        {
            LoginModel content = new LoginModel
            {
                Name = "Admin",
                Password = "P@ssword123",
                ReturnUrl = "/"
            };

            StringContent requestContent = BuildRequestContent(content);

            var response = await _client.PostAsJsonAsync("Account/login", requestContent);

            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Status Code: {response.StatusCode}");
            Console.WriteLine($"Response Content: {responseContent}");
            response.EnsureSuccessStatusCode();
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
                await AuthenticateAsAdmin(); // Authentification
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
                await AuthenticateAsAdmin(); // Authentification

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
            }
    }
}