using BlazorSandbox;
using DrawnUi.Draw;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

DrawnExtensions.RegisterFont("FontEmoji", "/fonts/NotoColorEmoji-Regular.ttf");

DrawnExtensions.RegisterFont("FontText", FontWeight.Regular, "/fonts/OpenSans-Regular.ttf");
DrawnExtensions.RegisterFont("FontTextBol", "/fonts/OpenSans-Semibold.ttf");
DrawnExtensions.RegisterFont("FontTextTitle", "/fonts/OpenSans-Semibold.ttf");

DrawnExtensions.RegisterFont("FontBrand", "/fonts/DOM.TTF");
DrawnExtensions.RegisterFont("FontBrandBold", "/fonts/DOMB.TTF");

DrawnExtensions.RegisterFont("FontGame", FontWeight.Regular, "/fonts/Orbitron-Regular.ttf");
DrawnExtensions.RegisterFont("FontGame", FontWeight.Medium, "/fonts/Orbitron-Medium.ttf");
DrawnExtensions.RegisterFont("FontGame", FontWeight.SemiBold, "/fonts/Orbitron-SemiBold.ttf");
DrawnExtensions.RegisterFont("FontGame", FontWeight.Bold, "/fonts/Orbitron-Bold.ttf");
DrawnExtensions.RegisterFont("FontGame", FontWeight.ExtraBold, "/fonts/Orbitron-ExtraBold.ttf");

DrawnExtensions.RegisterFont("FontGameMedium", "/fonts/Orbitron-Medium.ttf");
DrawnExtensions.RegisterFont("FontGameSemiBold", "/fonts/Orbitron-SemiBold.ttf");
DrawnExtensions.RegisterFont("FontGameBold", "/fonts/Orbitron-Bold.ttf");
DrawnExtensions.RegisterFont("FontGameExtraBold", "/fonts/Orbitron-ExtraBold.ttf");

var host = await builder.UseDrawnUiAsync(new DrawnUiStartupSettings
{
	UseDesktopKeyboard = true
});

await host.RunAsync();
