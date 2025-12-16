using CitationReader.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.RegisterCoreServices(builder.Configuration);

var app = builder.Build();
ServiceProvider = app.Services;

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<CitationReader.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
    public static IServiceProvider ServiceProvider { get; private set; }
}
