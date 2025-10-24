using danserdan.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add memory cache
builder.Services.AddMemoryCache();

// Add DbContext for SQL Server
builder.Services.AddDbContext<ApplicationDBContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

// Register HttpClient and AlphaVantageService with all dependencies
builder.Services.AddHttpClient<AlphaVantageService>()
    .ConfigureHttpClient(client =>
    {
        // You can configure the HttpClient here, e.g., set the base address or headers.
        client.BaseAddress = new Uri("https://www.alphavantage.co");
    });

// Register the AlphaVantageService with ApiKey and ApplicationDBContext injection
builder.Services.AddScoped<AlphaVantageService>(serviceProvider =>
{
    var apiKey = builder.Configuration.GetValue<string>("AlphaVantage:ApiKey");
    var context = serviceProvider.GetRequiredService<ApplicationDBContext>();
    var httpClient = serviceProvider.GetRequiredService<HttpClient>();
    var memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
    return new AlphaVantageService(httpClient, apiKey, context, memoryCache);
});

// Add session support
builder.Services.AddDistributedMemoryCache(); // This adds in-memory cache for session storage
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Set session timeout
});

// Register the background service for random stock price updates
builder.Services.AddHostedService<StockPriceUpdateService>();
builder.Services.AddScoped<StripeService>();

// Add HttpContextAccessor for currency service
builder.Services.AddHttpContextAccessor();

// Register the currency service
builder.Services.AddScoped<CurrencyService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Enable session middleware
app.UseSession();  // Add this line to enable session

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();