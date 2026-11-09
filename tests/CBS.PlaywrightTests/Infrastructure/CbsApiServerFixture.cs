using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests.Infrastructure;

public class CbsApiServerFixture : IAsyncLifetime
{
    private WebApplication _app = null!;
    public string ServerAddress { get; private set; } = string.Empty;
    public IPlaywright PlaywrightInstance { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _app = Program.CreateWebApplication([]);
        _app.Urls.Clear();
        _app.Urls.Add("http://127.0.0.1:0"); // Dynamic free port on loopback
        await _app.StartAsync();

        var server = _app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        ServerAddress = addresses!.Addresses.First();

        PlaywrightInstance = await Playwright.CreateAsync();
    }

    public async Task<IAPIRequestContext> CreateApiContextAsync()
    {
        return await PlaywrightInstance.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = ServerAddress
        });
    }

    public async Task DisposeAsync()
    {
        PlaywrightInstance?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
