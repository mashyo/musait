// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Musait.Models;

namespace Musait.Services
{
    public class AiBrowserService
    {
        private readonly string _sessionImageName;

        public AiBrowserService()
        {
            _sessionImageName = $"capture_{Guid.NewGuid().ToString("N").Substring(0, 8)}.png";
        }

        public void NavigateToAi(WebView2 webView)
        {
            NavigateToTarget(webView, AiServiceTarget.Renders);
        }

        public void NavigateToTarget(WebView2 webView, AiServiceTarget target)
        {
            webView.Source = new Uri(GetTargetUrl(target));
        }

        public static string GetTargetUrl(AiServiceTarget target)
        {
            return target switch
            {
                AiServiceTarget.Build => "https://aistudio.google.com/app/prompts/new_chat?model=gemini-3.5-flash",
                _ => "https://gemini.google.com/"
            };
        }

        public static bool IsExpectedHost(Uri? source, AiServiceTarget target)
        {
            if (source == null) return false;

            string host = source.Host;
            return target switch
            {
                AiServiceTarget.Build => host.EndsWith("aistudio.google.com", StringComparison.OrdinalIgnoreCase),
                _ => host.EndsWith("gemini.google.com", StringComparison.OrdinalIgnoreCase)
            };
        }

        public async Task InjectImageAsync(WebView2 webView, string base64Image)
        {
            await InjectPromptAndImageAsync(webView, base64Image, string.Empty);
        }

        public async Task InjectPromptAndImageAsync(WebView2 webView, string base64Image, string prompt)
        {
            await InjectPromptAndImageAsync(webView, base64Image, prompt, AiServiceTarget.Renders);
        }

        public async Task InjectPromptAndImageAsync(WebView2 webView, string base64Image, string prompt, AiServiceTarget target)
        {
            if (webView.CoreWebView2 == null) return;
            if (!IsExpectedHost(webView.Source, target))
            {
                throw new InvalidOperationException($"Musait can only inject captures while the browser is on {new Uri(GetTargetUrl(target)).Host}.");
            }

            if (!string.IsNullOrWhiteSpace(prompt))
            {
                bool focused = await FocusGeminiEditorAsync(webView);
                bool inserted = false;

                if (focused)
                {
                    inserted = await InsertPromptWithBrowserInputAsync(webView, prompt + "\n\n");
                }

                if (!inserted)
                {
                    await InsertPromptFallbackAsync(webView, prompt + "\n\n");
                }
            }

            await InjectImageFileAsync(webView, base64Image);
        }

        private static async Task<bool> FocusGeminiEditorAsync(WebView2 webView)
        {
            webView.Focus();

            string js = @"
                (function() {
                    function findEditor() {
                        var selectors = [
                            'rich-textarea .ql-editor[contenteditable=""true""]',
                            '.ql-editor.textarea[contenteditable=""true""]',
                            '.ql-editor[contenteditable=""true""]',
                            'textarea',
                            '[contenteditable=""true""]'
                        ];

                        for (var s = 0; s < selectors.length; s++) {
                            var editors = document.querySelectorAll(selectors[s]);
                            for (var i = editors.length - 1; i >= 0; i--) {
                                var editor = editors[i];
                                if (editor && editor.offsetParent !== null) return editor;
                            }
                        }

                        return null;
                    }

                    var editor = findEditor();
                    if (!editor) return false;

                    editor.focus();

                    if (editor.isContentEditable) {
                        var selection = window.getSelection();
                        var range = document.createRange();
                        range.selectNodeContents(editor);
                        range.collapse(false);
                        selection.removeAllRanges();
                        selection.addRange(range);
                    }

                    return document.activeElement === editor || editor.contains(document.activeElement);
                })();
            ";

            string result = await webView.CoreWebView2.ExecuteScriptAsync(js);
            return IsTruthyScriptResult(result);
        }

        private static async Task<bool> InsertPromptWithBrowserInputAsync(WebView2 webView, string prompt)
        {
            string promptProbe = prompt.Substring(0, Math.Min(20, prompt.Length));
            try
            {
                string parameters = JsonConvert.SerializeObject(new { text = prompt });
                await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.insertText", parameters);
                return await EditorContainsTextAsync(webView, promptProbe);
            }
            catch
            {
                return false;
            }
        }

        private static async Task InsertPromptFallbackAsync(WebView2 webView, string prompt)
        {
            string promptJson = JsonConvert.SerializeObject(prompt);

            string js = $@"
                (function() {{
                    const promptText = {promptJson};

                    function findEditor() {{
                        var selectors = [
                            'rich-textarea .ql-editor[contenteditable=""true""]',
                            '.ql-editor.textarea[contenteditable=""true""]',
                            '.ql-editor[contenteditable=""true""]',
                            'textarea',
                            '[contenteditable=""true""]'
                        ];

                        for (var s = 0; s < selectors.length; s++) {{
                            var editors = document.querySelectorAll(selectors[s]);
                            for (var i = editors.length - 1; i >= 0; i--) {{
                                var editor = editors[i];
                                if (editor && editor.offsetParent !== null) return editor;
                            }}
                        }}

                        return null;
                    }}

                    var editor = findEditor();
                    if (!editor) return false;

                    editor.focus();

                    if ('value' in editor) {{
                        editor.value = promptText;
                    }} else {{
                        editor.textContent = '';
                        promptText.split('\n').forEach(function(line) {{
                            var p = document.createElement('p');
                            p.textContent = line || '\u00a0';
                            editor.appendChild(p);
                        }});
                    }}

                    editor.dispatchEvent(new InputEvent('input', {{ bubbles: true, inputType: 'insertText', data: promptText }}));
                    editor.dispatchEvent(new Event('change', {{ bubbles: true }}));
                    return true;
                }})();
            ";

            await webView.CoreWebView2.ExecuteScriptAsync(js);
        }

        private static async Task<bool> EditorContainsTextAsync(WebView2 webView, string text)
        {
            string textJson = JsonConvert.SerializeObject(text);

            string js = $@"
                (function() {{
                    const text = {textJson};
                    var editors = document.querySelectorAll('rich-textarea .ql-editor, .ql-editor, textarea, [contenteditable=""true""]');
                    for (var i = editors.length - 1; i >= 0; i--) {{
                        var editor = editors[i];
                        if (!editor || editor.offsetParent === null) continue;
                        var visibleText = editor.innerText || editor.value || editor.textContent || '';
                        if (visibleText.indexOf(text) !== -1) return true;
                    }}
                    return false;
                }})();
            ";

            string result = await webView.CoreWebView2.ExecuteScriptAsync(js);
            return IsTruthyScriptResult(result);
        }

        private async Task InjectImageFileAsync(WebView2 webView, string base64Image)
        {
            string base64Json = JsonConvert.SerializeObject(base64Image);
            string imageNameJson = JsonConvert.SerializeObject(_sessionImageName);

            string js = $@"
                (async function() {{
                const base64Image = {base64Json};
                const imageName = {imageNameJson};

                function findEditor() {{
                    var selectors = [
                        'rich-textarea .ql-editor[contenteditable=""true""]',
                        '.ql-editor.textarea[contenteditable=""true""]',
                        '.ql-editor[contenteditable=""true""]',
                        'textarea',
                        '[contenteditable=""true""]'
                    ];

                    for (var s = 0; s < selectors.length; s++) {{
                        var editors = document.querySelectorAll(selectors[s]);
                        for (var i = editors.length - 1; i >= 0; i--) {{
                            var editor = editors[i];
                            if (editor && editor.offsetParent !== null) return editor;
                        }}
                    }}

                    return null;
                }}

                function dispatchPaste(editor, file) {{
                    var dt = new DataTransfer();
                    if (file) dt.items.add(file);

                    var pasteEvent = new ClipboardEvent('paste', {{
                        clipboardData: dt,
                        bubbles: true,
                        cancelable: true
                    }});

                    editor.focus();
                    editor.dispatchEvent(pasteEvent);
                }}

                const editor = findEditor();
                if (!editor) {{
                    alert('Musait Warning: Could not find the Gemini chat input box.');
                    return false;
                }}

                const blob = await fetch('data:image/png;base64,' + base64Image)
                .then(res => res.blob())
                .catch(err => {{
                    alert('Injection Error: ' + err);
                    return null;
                }});

                if (!blob) return false;

                var file = new File([blob], imageName, {{type: 'image/png'}});
                dispatchPaste(editor, file);
                return true;
                }})()
            ";

            await webView.CoreWebView2.ExecuteScriptAsync(js);
        }

        private static bool IsTruthyScriptResult(string result)
        {
            return string.Equals(result?.Trim().Trim('"'), "true", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<string> CheckLoginStatusAsync(WebView2 webView)
        {
            if (webView.CoreWebView2 == null) return "NOT_LOGGED_IN";

            string js = @"
                (function() {
                    var elements = document.querySelectorAll('a, button, span');
                    for (var i = 0; i < elements.length; i++) {
                        if (elements[i].textContent.trim() === 'Sign in' || elements[i].textContent.trim() === 'Sign In') {
                            if (elements[i].offsetHeight > 0) {
                                return 'NOT_LOGGED_IN';
                            }
                        }
                    }

                    var accountBtn = document.querySelector('a[aria-label*=""@""]');
                    if (accountBtn) {
                        var label = accountBtn.getAttribute('aria-label');
                        var colonIdx = label.indexOf(':');
                        if (colonIdx !== -1) label = label.substring(colonIdx + 1);
                        var parenIdx = label.lastIndexOf('(');
                        if (parenIdx !== -1) label = label.substring(0, parenIdx);
                        
                        var nameToPass = label.replace('\n', '').replace('\r', '').trim();
                        if (nameToPass.includes(' ')) nameToPass = nameToPass.split(' ')[0];
                        return nameToPass;
                    }
                    
                    var meta = document.querySelector('meta[name=""og-profile-acct""]');
                    if (meta && meta.content) {
                        return meta.content.split('@')[0];
                    } 
                    
                    return 'NOT_LOGGED_IN';
                })();
            ";

            try
            {
                string result = await webView.CoreWebView2.ExecuteScriptAsync(js);
                return result.Trim('"');
            }
            catch
            {
                return "NOT_LOGGED_IN";
            }
        }

        public async Task ApplyPreferredColorSchemeAsync(WebView2 webView, bool isDark)
        {
            if (webView.CoreWebView2 == null) return;

            string payload = JsonConvert.SerializeObject(new
            {
                features = new[]
                {
                    new
                    {
                        name = "prefers-color-scheme",
                        value = isDark ? "dark" : "light"
                    }
                }
            });

            await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia", payload);
        }
    }
}

