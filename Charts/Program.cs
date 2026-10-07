var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSyncfusionBlazor();
builder.Services.AddOptions<BrightSkyOptions>()
    .Bind(builder.Configuration.GetSection(BrightSkyOptions.SectionName));
builder.Services.AddHttpClient<ApiService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<BrightSkyOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
});
builder.Services.AddScoped<WeatherService>();
builder.Services.AddScoped<WeatherDashboardService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();

//IConfigurationRoot configuration = new ConfigurationBuilder()
//    .SetBasePath(Directory.GetCurrentDirectory())
//    .AddJsonFile("appsettings.json")
//    .Build();

//var syncfusionKey = configuration["SyncfusionLicenseKey"];
//Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);

var syncfusionKey =
    builder.Configuration["Syncfusion:LicenseKey"] ??
    Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

// Register Syncfusion license key
if (!string.IsNullOrWhiteSpace(syncfusionKey))
{
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
}


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
