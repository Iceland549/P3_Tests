using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using P3AddNewFunctionalityDotNetCore.Models.Repositories;
using P3AddNewFunctionalityDotNetCore.Models.Services;
using P3AddNewFunctionalityDotNetCore.Models;
using Microsoft.AspNetCore.Identity;
using P3AddNewFunctionalityDotNetCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using P3AddNewFunctionalityDotNetCore;
using System.Linq;
using Microsoft.AspNetCore.Hosting;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
}

public class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Add services to the container.
        services.AddControllersWithViews();

        services.AddLocalization(opts => { opts.ResourcesPath = "Resources"; });
        services.AddSingleton<ICart, Cart>();
        services.AddSingleton<ILanguageService, LanguageService>();
        services.AddTransient<IProductService, ProductService>();
        services.AddTransient<IProductRepository, ProductRepository>();
        services.AddTransient<IOrderService, OrderService>();
        services.AddTransient<IOrderRepository, OrderRepository>();
        services.AddMemoryCache();
        services.AddSession();
        services.AddMvc()
            .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix, opts => { opts.ResourcesPath = "Resources"; })
            .AddDataAnnotationsLocalization();

        services.AddDbContext<P3Referential>(options =>
            options.UseSqlServer(Configuration.GetConnectionString("P3Referential")));

        services.AddDbContext<AppIdentityDbContext>(options =>
            options.UseSqlServer(Configuration.GetConnectionString("P3Identity")));

        services.AddDefaultIdentity<IdentityUser>()
                .AddEntityFrameworkStores<AppIdentityDbContext>()
                .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (!env.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }
        else
        {
            app.SeedDatabase(Configuration);
        }

        if (!env.IsEnvironment("Testing"))
        {
            app.UseHttpsRedirection();
        }
        app.UseStaticFiles();

        var supportedCultures = new[] { "en-GB", "en-US", "en", "fr-FR", "fr" };
        var localizationOptions = new RequestLocalizationOptions().SetDefaultCulture(supportedCultures[0])
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures);
        app.UseRequestLocalization(localizationOptions);

        app.UseSession();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller=Product}/{action=Index}/{id?}");
        });

        IdentitySeedData.EnsurePopulated(app).Wait();
    }
}