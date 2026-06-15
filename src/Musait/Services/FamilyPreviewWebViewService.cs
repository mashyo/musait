// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;

namespace Musait.Services
{
    public sealed class FamilyPreviewWebViewService
    {
        private const string PreviewHost = "musait-family";
        private const string PreviewRoute = "https://" + PreviewHost + "/family-viewer.html";
        private bool _initialized;

        public async Task InitializeAsync(WebView2 browser)
        {
            if (_initialized || browser?.CoreWebView2 == null) return;

            string previewerFolder = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                "UI",
                "Previewer");

            browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                PreviewHost,
                previewerFolder,
                CoreWebView2HostResourceAccessKind.DenyCors);

            await NavigateAsync(browser, PreviewRoute);
            _initialized = true;
        }

        public void Reset()
        {
            _initialized = false;
        }

        public async Task ShowPreviewAsync(WebView2 browser, string payloadJson)
        {
            await InitializeAsync(browser);
            if (browser?.CoreWebView2 == null) return;

            if (!string.Equals(browser.Source?.ToString(), PreviewRoute, StringComparison.OrdinalIgnoreCase))
            {
                await NavigateAsync(browser, PreviewRoute);
            }

            string script = "window.__loadJSON && window.__loadJSON(" +
                JsonConvert.SerializeObject(payloadJson) +
                ");";
            await browser.CoreWebView2.ExecuteScriptAsync(script);
            browser.CoreWebView2.PostWebMessageAsJson(payloadJson);
        }

        public Task ApplyThemeAsync(WebView2 browser, bool isDark)
        {
            if (browser?.CoreWebView2 == null) return Task.CompletedTask;

            string mode = isDark ? "dark" : "light";
            string payload = JsonConvert.SerializeObject(new
            {
                type = "theme",
                mode
            });
            browser.CoreWebView2.PostWebMessageAsJson(payload);
            return browser.CoreWebView2.ExecuteScriptAsync(
                "window.__setTheme && window.__setTheme(" +
                JsonConvert.SerializeObject(mode) +
                ");");
        }

        private static Task NavigateAsync(WebView2 browser, string route)
        {
            var completion = new TaskCompletionSource<bool>();

            void OnCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
            {
                browser.NavigationCompleted -= OnCompleted;
                completion.TrySetResult(args.IsSuccess);
            }

            browser.NavigationCompleted += OnCompleted;
            try
            {
                browser.CoreWebView2.Navigate(route);
            }
            catch
            {
                browser.NavigationCompleted -= OnCompleted;
                completion.TrySetResult(false);
            }

            return completion.Task;
        }

        public static bool IsTrustedSource(Uri? source)
        {
            return source != null &&
                   string.Equals(source.Scheme, "https", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(source.Host, PreviewHost, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTrustedSource(string? source)
        {
            return Uri.TryCreate(source, UriKind.Absolute, out var uri) && IsTrustedSource(uri);
        }
    }
}
