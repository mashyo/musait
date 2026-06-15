// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Musait.Models;

namespace Musait.Utils
{
    public static class WebView2Bootstrapper
    {
        public static async Task InitializeAsync(WebView2 webView)
        {
            try
            {
                string dataFolder = Constants.BrowserDataFolder;
                Directory.CreateDirectory(dataFolder);

                var environment = await CoreWebView2Environment.CreateAsync(null, dataFolder);
                await webView.EnsureCoreWebView2Async(environment);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize WebView2 environment. Please make sure the Microsoft WebView2 Runtime is installed.", ex);
            }
        }
    }
}

