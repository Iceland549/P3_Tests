﻿
using P3AddNewFunctionalityDotNetCore.Models;
using P3AddNewFunctionalityDotNetCore.Models.Repositories;
using P3AddNewFunctionalityDotNetCore.Models.Services;
using P3AddNewFunctionalityDotNetCore.Models.ViewModels;

using Xunit;

using Microsoft.Extensions.Localization;
using Moq;

namespace P3AddNewFunctionalityDotNetCore.Tests
{
    public class ProductServiceTests 
    {
        private readonly IProductService _productService;
        private readonly Mock<ICart> _mockCart;
        private readonly Mock<IProductRepository> _mockProductRepository;
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly Mock<IStringLocalizer<ProductService>> _mockLocalizer;

        public ProductServiceTests()
        {
            _mockCart = new Mock<ICart>();
            _mockProductRepository = new Mock<IProductRepository>();
            _mockOrderRepository = new Mock<IOrderRepository>();
            _mockLocalizer = new Mock<IStringLocalizer<ProductService>>();

            _productService = new ProductService(
                _mockCart.Object,
                _mockProductRepository.Object,
                _mockOrderRepository.Object,
                _mockLocalizer.Object
            );
        }

        /// <summary>
        /// Take this test method as a template to write your test method.
        /// A test method must check if a definite method does its job:
        /// returns an expected value from a particular set of parameters
        /// </summary>
        // TODO write test methods to ensure a correct coverage of all possibilities
        [Fact] 
        public void CheckProductModelErrors_MissingName_ReturnsError()
        {
            // Setup
            _mockLocalizer.Setup(x => x["MissingName"]).Returns(new LocalizedString("MissingName", "Le nom est obligatoire"));
            var product = new ProductViewModel { Name = "", Price = "10", Stock = "5" };

            // Act
            var errors = _productService.CheckProductModelErrors(product);

            // Assert
            Assert.True(string.IsNullOrEmpty(product.Name));
            Assert.Contains("MissingName", errors);
        }
        [Fact]
        public void CheckProductModelErrors_MissingPrice_ReturnsError()
        {
            // Setup
            _mockLocalizer.Setup(x => x["MissingPrice"]).Returns(new LocalizedString("MissingPrice", "Le prix est obligatoire"));
            var product = new ProductViewModel { Name = "Test Product", Price = "", Stock = "5" };

            // Act
            var errors = _productService.CheckProductModelErrors(product);

            // Assert
            Assert.True(string.IsNullOrEmpty(product.Price));
            Assert.Contains("MissingPrice", errors);
        }

        [Fact]
        public void CheckProductModelErrors_PriceNotANumber_ReturnsError()
        {
            // Setup
            _mockLocalizer.Setup(x => x["PriceNotANumber"]).Returns(new LocalizedString("PriceNotANumber", "Le prix doit être un nombre"));
            var product = new ProductViewModel { Name = "Test Product", Price = "abc", Stock = "5" };

            // Act
            var errors = _productService.CheckProductModelErrors(product);

            // Assert
            Assert.False(double.TryParse(product.Price, out _));
            Assert.Contains("PriceNotANumber", errors);
        }

        [Fact]
        public void CheckProductModelErrors_PriceNotGreaterThanZero_ReturnsError()
        {
            // Setup
            _mockLocalizer.Setup(x => x["PriceNotGreaterThanZero"]).Returns(new LocalizedString("PriceNotGreaterThanZero", "Le prix doit être supérieur à zéro"));
            var product = new ProductViewModel { Name = "Test Product", Price = "0", Stock = "5" };

            // Act
            var errors = _productService.CheckProductModelErrors(product);

            // Assert
            Assert.True(double.Parse(product.Price) <= 0);
            Assert.Contains("PriceNotGreaterThanZero", errors);
        }

        [Fact]
        public void CheckProductModelErrors_MissingQuantity_ReturnsError()
        {
            // Setup
            _mockLocalizer.Setup(x => x["MissingQuantity"]).Returns(new LocalizedString("MissingQuantity", "La quantité est obligatoire"));
            var product = new ProductViewModel { Name = "Test Product", Price = "10", Stock = "" };

            // Act
            var errors = _productService.CheckProductModelErrors(product);

            // Assert
            Assert.True(string.IsNullOrEmpty(product.Stock));
            Assert.Contains("MissingQuantity", errors);
        }

        [Fact]
        public void CheckProductModelErrors_QuantityNotAnInteger_ReturnsError()
        {
            // Setup
            _mockLocalizer.Setup(x => x["QuantityNotAnInteger"]).Returns(new LocalizedString("QuantityNotAnInteger", "La quantité doit être un nombre entier"));
            var product = new ProductViewModel { Name = "Test Product", Price = "10", Stock = "abc" };

            // Act
            var errors = _productService.CheckProductModelErrors(product);

            // Assert
            Assert.False(int.TryParse(product.Stock, out _));
            Assert.Contains("QuantityNotAnInteger", errors);
        }

        [Fact]
        public void CheckProductModelErrors_QuantityNotGreaterThanZero_ReturnsError()
        {
            // Setup
            _mockLocalizer.Setup(x => x["QuantityNotGreaterThanZero"]).Returns(new LocalizedString("QuantityNotGreaterThanZero", "La quantité doit être supérieure à zéro"));
            var product = new ProductViewModel { Name = "Test Product", Price = "10", Stock = "0" };

            // Act
            var errors = _productService.CheckProductModelErrors(product);

            // Assert
            Assert.True(int.Parse(product.Stock) <= 0);
            Assert.Contains("QuantityNotGreaterThanZero", errors);
        }


    }

}
