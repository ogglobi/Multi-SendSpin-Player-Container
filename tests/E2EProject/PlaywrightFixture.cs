namespace MultiRoomAudio.E2ETests;

using Microsoft.Playwright;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Concurrent;

public class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    public IBrowser? Browser { get; private set; }
    public HttpClient HttpClient { get; private set; } = new HttpClient { BaseAddress = new Uri("http://localhost:8096") };
    private readonly ConcurrentDictionary<IBrowserContext, string> _contextTraceIds = new();

    public Task<IPage> CreatePageAsync()
    {
        if (Browser == null) throw new InvalidOperationException("Browser not initialized");
        // Create a fresh context and page for each test to avoid shared-state and
        // parallelization issues where the page/context may be closed unexpectedly.
        return Task.Run(async () =>
        {
            var context = await Browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = HttpClient.BaseAddress.ToString() });
            // Ensure a traces directory exists
            try { System.IO.Directory.CreateDirectory("/tmp/playwright-traces"); } catch { }
            var traceId = Guid.NewGuid().ToString("N");
            _contextTraceIds[context] = traceId;
            await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true, Sources = true });
            var page = await context.NewPageAsync();
            // Stop tracing and save when the page is closed (context close/page close)
            page.Close += async (_, _) =>
            {
                try
                {
                    await context.Tracing.StopAsync(new TracingStopOptions { Path = $"/tmp/playwright-traces/trace-{traceId}.zip" });
                }
                catch { }
            };
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
        // Ensure tracing is stopped for any active contexts so traces are written
        foreach (var kv in _contextTraceIds)
        {
            try
            {
                var ctx = kv.Key;
                var id = kv.Value;
                await ctx.Tracing.StopAsync(new TracingStopOptions { Path = $"/tmp/playwright-traces/trace-{id}.zip" });
            }
            catch { }
        }

        try { if (Browser != null) await Browser.CloseAsync(); } catch { }
        _playwright?.Dispose();
        HttpClient.Dispose();
    }
}
