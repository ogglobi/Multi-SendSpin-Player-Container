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
    public Task<IPage> CreatePageAsync()
    {
        if (Browser == null) throw new InvalidOperationException("Browser not initialized");
        // Create a fresh context and page for each test to avoid shared-state and
        // parallelization issues where the page/context may be closed unexpectedly.
        return Task.Run(async () =>
        {
            var context = await Browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = HttpClient.BaseAddress.ToString() });
            var page = await context.NewPageAsync();
            return page;
        });
    }

        public async Task InitializeAsync()
        {
            _playwright = await Playwright.CreateAsync();
            Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                // Nothing more to do here — pages/contexts are created per-test in CreatePageAsync
        }

    public async Task DisposeAsync()
    {
        try { if (_context != null) await _context.CloseAsync(); } catch { }
        try { if (Browser != null) await Browser.CloseAsync(); } catch { }
        _playwright?.Dispose();
        HttpClient.Dispose();
    }
}
