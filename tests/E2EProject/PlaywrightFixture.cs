namespace MultiRoomAudio.E2ETests;

using Microsoft.Playwright;
using System;
using System.Net.Http;
using System.Threading.Tasks;

public class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    public IBrowser? Browser { get; private set; }
    public HttpClient HttpClient { get; private set; } = new HttpClient { BaseAddress = new Uri("http://localhost:8096") };
    private IBrowserContext? _context;
    private IPage? _page;

    public Task<IPage> CreatePageAsync()
    {
        if (_page == null) throw new InvalidOperationException("Page not initialized");
        return Task.FromResult(_page);
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _context = await Browser.NewContextAsync();
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        try { if (_context != null) await _context.CloseAsync(); } catch { }
        try { if (Browser != null) await Browser.CloseAsync(); } catch { }
        _playwright?.Dispose();
        HttpClient.Dispose();
    }
}
