// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Autodesk.Revit.UI;
using Musait.Models;
using Musait.Services;
using Musait.Utils;
using WpfToggleButton = System.Windows.Controls.Primitives.ToggleButton;

namespace Musait.UI.Views
{
    public partial class AiDockablePane : UserControl
    {
        public static AiDockablePane? Instance { get; private set; }

        private readonly AiBrowserService _webService = new();
        private readonly FamilyPreviewWebViewService _familyPreviewWebViewService = new();
        private const string CustomPromptPresetId = "custom-prompt";
        private const string DefaultRendersPresetId = "as-captured";
        private const string RecommendedAiStudioModel = "gemini-3.5-flash";
        private bool _isBrowserInitialized;
        private bool _isFamilyPreviewBrowserInitialized;
        private bool _isFamilyPreviewBrowserInitializing;
        private bool _isDisposed;
        private bool _isLoadingSettingsControls;
        private PaneState _paneState = PaneState.BrowserLoading;
        private string? _pendingBase64Image;
        private string _pendingRevitContext = string.Empty;
        private bool _isContextIncludedInComposer;
        private bool _isPromptExpanded;
        private bool _isUpdatingComposerPrompt;
        private PromptMode _currentComposerMode = PromptMode.Visualize;
        private string _composerPresetId = DefaultRendersPresetId;
        private string _buildFunctionId = "family";
        private string? _familyReferenceBase64Image;
        private string? _familyReferencePath;
        private bool _pendingFamilyReferenceSendToStudio;
        private string? _familyJsonPath;
        private bool _familyJsonPreviewValid;
        private FamilyBuildPlan? _currentFamilyBuildPlan;
        private int _familyPipelineStep = 1;
        private int _familyUiState;
        private string? _selectedStudioPresetId;
        private bool _isModifiersExpanded;
        private readonly DispatcherTimer _sentStateTimer;
        private readonly DispatcherTimer _familyJsonReadinessTimer;
        private readonly UpdateService _updateService = new();
        private UpdateInfo? _availableUpdate;
        private bool _isCheckingForUpdates;
        private bool _isDownloadingUpdate;
        private AiServiceTarget _activeTarget = AiServiceTarget.Renders;
        private bool _familyJsonReadyToAcquire;
        private bool _familyJsonCandidateDetected;
        private bool _isCheckingFamilyJsonReadiness;
        private const double FamilyPreviewHostMinHeight = 560;
        private const double FamilyPreviewHostMaxHeight = 760;
        private const double FamilyPreviewHostBottomBuffer = 2;
        private const string FamilyJsonCandidateScanScript = @"
                    (function() {
                        function visibleText(el) {
                            if (!el) return '';
                            var style = window.getComputedStyle(el);
                            if (style && (style.display === 'none' || style.visibility === 'hidden')) return '';
                            var text = (el.innerText || el.textContent || '').trim();
                            return text.length > 80000 ? text.slice(-80000) : text;
                        }

                        var seen = Object.create(null);
                        var candidates = [];
                        function add(text) {
                            if (!text) return;
                            var lower = text.toLowerCase();
                            if (text.indexOf('{') === -1 || text.indexOf('}') === -1) return;
                            if (lower.indexOf('musait.family.rfa.v2') === -1 &&
                                lower.indexOf('reference_planes') === -1 &&
                                lower.indexOf('referenceplanes') === -1 &&
                                lower.indexOf('constraints') === -1 &&
                                lower.indexOf('components') === -1 &&
                                lower.indexOf('category') === -1 &&
                                lower.indexOf('parameters') === -1 &&
                                lower.indexOf('dims') === -1) return;
                            var key = text.slice(0, 120) + ':' + text.length;
                            if (seen[key]) return;
                            seen[key] = true;
                            candidates.push(text);
                        }

                        var selectors = [
                            'pre code',
                            'pre',
                            'code',
                            'article',
                            '[role=""article""]',
                            'model-response',
                            'message-content',
                            '[class*=""markdown""]',
                            '[class*=""response""]',
                            '[class*=""message""]',
                            '[class*=""model""]'
                        ];

                        selectors.forEach(function(selector) {
                            document.querySelectorAll(selector).forEach(function(el) {
                                add(visibleText(el));
                            });
                        });

                        add(visibleText(document.body));
                        return candidates.slice(-80);
                    })();
                ";
        private const string FamilyJsonReadinessProbeScript = @"
                    (function() {
                        if (!window.chrome || !window.chrome.webview || !document.body) return false;
                        if (window.__musaitFamilyJsonProbe && window.__musaitFamilyJsonProbe.stop) {
                            window.__musaitFamilyJsonProbe.stop();
                        }

                        function visibleText(el) {
                            if (!el) return '';
                            var style = window.getComputedStyle(el);
                            if (style && (style.display === 'none' || style.visibility === 'hidden')) return '';
                            var text = (el.innerText || el.textContent || '').trim();
                            return text.length > 80000 ? text.slice(-80000) : text;
                        }

                        function collectCandidates() {
                            var seen = Object.create(null);
                            var candidates = [];
                            function add(text) {
                                if (!text) return;
                                var lower = text.toLowerCase();
                                if (text.indexOf('{') === -1 || text.indexOf('}') === -1) return;
                                if (lower.indexOf('musait.family.rfa.v2') === -1 &&
                                    lower.indexOf('reference_planes') === -1 &&
                                    lower.indexOf('referenceplanes') === -1 &&
                                    lower.indexOf('constraints') === -1 &&
                                    lower.indexOf('components') === -1 &&
                                    lower.indexOf('category') === -1 &&
                                    lower.indexOf('parameters') === -1 &&
                                    lower.indexOf('dims') === -1) return;
                                var key = text.slice(0, 120) + ':' + text.length;
                                if (seen[key]) return;
                                seen[key] = true;
                                candidates.push(text);
                            }

                            [
                                'pre code',
                                'pre',
                                'code',
                                'article',
                                '[role=""article""]',
                                'model-response',
                                'message-content',
                                '[class*=""markdown""]',
                                '[class*=""response""]',
                                '[class*=""message""]',
                                '[class*=""model""]'
                            ].forEach(function(selector) {
                                document.querySelectorAll(selector).forEach(function(el) {
                                    add(visibleText(el));
                                });
                            });

                            add(visibleText(document.body));
                            return candidates.slice(-80);
                        }

                        var lastSignature = '';
                        var timer = 0;
                        function scan() {
                            timer = 0;
                            var text = collectCandidates().join('\n\n');
                            var signature = text.length + ':' + text.slice(-1200);
                            if (signature === lastSignature) return;
                            lastSignature = signature;
                            window.chrome.webview.postMessage({
                                source: 'musait-family-json-readiness',
                                text: text
                            });
                        }

                        function scheduleScan() {
                            if (timer) window.clearTimeout(timer);
                            timer = window.setTimeout(scan, 350);
                        }

                        var observer = new MutationObserver(scheduleScan);
                        observer.observe(document.body, { childList: true, subtree: true, characterData: true });
                        window.__musaitFamilyJsonProbe = {
                            stop: function() {
                                observer.disconnect();
                                if (timer) window.clearTimeout(timer);
                            },
                            scan: scheduleScan
                        };
                        scheduleScan();
                        return true;
                    })();
                ";

        public AiDockablePane()
        {
            InitializeComponent();
            Instance = this;
            ApplyTheme(SettingsManager.Settings.IsDarkTheme);
            LoadPromptControls();
            LoadSettingsControls();
            LoadAboutPanel();
            UpdateServiceTargetVisuals();

            _sentStateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _sentStateTimer.Tick += (s, e) =>
            {
                _sentStateTimer.Stop();
                if (!_isDisposed && _paneState == PaneState.Sent)
                {
                    SetPaneState(PaneState.Ready);
                }
            };

            _familyJsonReadinessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _familyJsonReadinessTimer.Tick += FamilyJsonReadinessTimer_Tick;

            Loaded += AiDockablePane_Loaded;
            Unloaded += AiDockablePane_Unloaded;
            SetPaneState(PaneState.BrowserLoading);
        }

        private void LoadPromptControls()
        {
            MigrateLegacyCustomPromptText();
            MigrateDefaultRenderPreset();
            var preset = PromptPresetCatalog.GetById(SettingsManager.Settings.PromptPresetId);
            if (preset.Id == CustomPromptPresetId)
            {
                preset = PromptPresetCatalog.GetDefaultForMode(PromptMode.Visualize);
            }

            _composerPresetId = preset.Id;
            _buildFunctionId = PromptPresetCatalog.GetById(SettingsManager.Settings.BuildFunctionId).Mode == PromptMode.Build
                ? SettingsManager.Settings.BuildFunctionId
                : PromptPresetCatalog.GetDefaultForMode(PromptMode.Build).Id;
            UpdateModeBadge(preset.Mode);
            LoadBuildControls();
        }

        private static void MigrateDefaultRenderPreset()
        {
            var settings = SettingsManager.Settings;
            if (string.Equals(settings.PromptPresetId, "exterior-render", StringComparison.Ordinal))
            {
                settings.PromptPresetId = DefaultRendersPresetId;
                SettingsManager.Save();
            }
        }

        private static void MigrateLegacyCustomPromptText()
        {
            var settings = SettingsManager.Settings;
            if (string.IsNullOrWhiteSpace(settings.CustomPromptText))
            {
                return;
            }

            bool changed = false;
            if (string.IsNullOrEmpty(settings.VisualizeCustomPromptText))
            {
                settings.VisualizeCustomPromptText = settings.CustomPromptText;
                changed = true;
            }

            if (string.IsNullOrEmpty(settings.TrendsCustomPromptText))
            {
                settings.TrendsCustomPromptText = settings.CustomPromptText;
                changed = true;
            }

            if (changed)
            {
                SettingsManager.Save();
            }
        }

        private void LoadSettingsControls()
        {
            _isLoadingSettingsControls = true;
            try
            {
                SettingsResolutionCombo.ItemsSource = new[]
                {
                    "1024 x 768",
                    "1024 x 1024",
                    "1920 x 1080",
                    "2560 x 1440",
                    "3840 x 2160",
                    "4096 x 2160"
                };
                SettingsDefaultModeCombo.ItemsSource = new[] { "Auto" };
                SettingsPresetCombo.ItemsSource = PromptPresetCatalog.GetByMode(PromptMode.Visualize);

                var settings = SettingsManager.Settings;
                SettingsResolutionCombo.SelectedIndex = Math.Max(0, Math.Min(SettingsResolutionCombo.Items.Count - 1, settings.CaptureResolutionIndex));
                SettingsWhiteBackgroundCheck.IsChecked = settings.ForceWhiteBackground;
                SettingsDisplayModeCheck.IsChecked = settings.ChangeDisplayMode;
                SettingsDefaultModeCombo.SelectedItem = "Auto";
                SettingsPresetCombo.SelectedItem = GetCurrentActionPreset();
                SettingsAutoSendCheck.IsChecked = settings.AutoSendCaptures;
                SettingsRequireFamilyPreviewCheck.IsChecked = settings.RequireFamilyPreviewBeforeGenerate;
                SettingsContextCheck.IsChecked = settings.IncludeRevitContext;
                SettingsThemeCombo.SelectedIndex = settings.IsDarkTheme ? 0 : 1;
                SettingsArchiveCheck.IsChecked = settings.AutoArchiveCaptures;
            }
            finally
            {
                _isLoadingSettingsControls = false;
            }
        }

        private void SaveSettingsControls()
        {
            if (_isLoadingSettingsControls) return;

            var settings = SettingsManager.Settings;
            settings.CaptureResolutionIndex = Math.Max(0, SettingsResolutionCombo.SelectedIndex);
            settings.ForceWhiteBackground = SettingsWhiteBackgroundCheck.IsChecked == true;
            settings.ChangeDisplayMode = SettingsDisplayModeCheck.IsChecked == true;
            settings.DefaultPromptMode = "Auto";
            settings.PromptPresetId = (SettingsPresetCombo.SelectedItem as PromptPreset)?.Id ?? settings.PromptPresetId;
            settings.AutoSendCaptures = SettingsAutoSendCheck.IsChecked == true;
            settings.RequireFamilyPreviewBeforeGenerate = SettingsRequireFamilyPreviewCheck.IsChecked == true;
            settings.ShowTrendsInComposer = true;
            settings.IncludeRevitContext = SettingsContextCheck.IsChecked == true;
            settings.IsDarkTheme = SettingsThemeCombo.SelectedIndex <= 0;
            settings.AutoArchiveCaptures = SettingsArchiveCheck.IsChecked == true;
            SettingsManager.Save();

            ApplyTheme(settings.IsDarkTheme);
            LoadPromptControls();
        }

        private void ApplyTheme(bool isDark)
        {
            if (isDark)
            {
                SetThemeColors(
                    "#1C1F21",
                    "#222628",
                    "#2E3235",
                    "#181B1F",
                    "#252A2D",
                    "#2A2E31",
                    "#3A3F43",
                    "#C8CDD2",
                    "#C0C5CA",
                    "#8A96A3",
                    "#3A6F96",
                    "#E8F2FB");
            }
            else
            {
                SetThemeColors(
                    "#F4F6F8",
                    "#FFFFFF",
                    "#E8EDF2",
                    "#EEF2F6",
                    "#D7DEE6",
                    "#CDD5DE",
                    "#BBC5D0",
                    "#1F2933",
                    "#35424F",
                    "#6B7886",
                    "#2F6F9F",
                    "#FFFFFF");
            }

            _ = ApplyFamilyPreviewThemeAsync();
            _ = ApplyPrimaryBrowserThemeAsync();
            UpdateStatusChipVisuals();
            UpdateServiceTargetVisuals();
            UpdatePromptModifierChipVisuals();
            UpdateSelectedPresetCard(_composerPresetId);
            UpdateStudioStyleVisuals();
        }

        private void LoadAboutPanel()
        {
            AboutTitleText.Text = $"{Constants.PluginName} v{Constants.CurrentVersion}";
            AboutBylineText.Text = $"by {Constants.AuthorName}";
            RefreshUpdateUi();
        }

        private void RefreshUpdateUi()
        {
            if (UpdateStatusText == null || CheckUpdatesButton == null || DownloadUpdateButton == null)
            {
                return;
            }

            CheckUpdatesButton.IsEnabled = !_isCheckingForUpdates && !_isDownloadingUpdate;
            DownloadUpdateButton.IsEnabled = _availableUpdate != null && !_isCheckingForUpdates && !_isDownloadingUpdate;
            DownloadUpdateButton.Visibility = _availableUpdate != null ? Visibility.Visible : Visibility.Collapsed;

            if (_isCheckingForUpdates)
            {
                UpdateStatusText.Text = "Checking GitHub Releases...";
            }
            else if (_isDownloadingUpdate)
            {
                UpdateStatusText.Text = "Downloading update...";
            }
            else if (!string.IsNullOrWhiteSpace(UpdateService.PendingInstallerPath))
            {
                UpdateStatusText.Text = "Installer will run after Revit closes.";
                DownloadUpdateButton.Visibility = Visibility.Collapsed;
            }
            else if (_availableUpdate != null)
            {
                UpdateStatusText.Text = string.IsNullOrWhiteSpace(_availableUpdate.InstallerAssetUrl)
                    ? $"Version {_availableUpdate.Version} is available, but the setup installer is missing. Open Releases to download manually."
                    : $"Version {_availableUpdate.Version} is available.";
            }
            else
            {
                UpdateStatusText.Text = $"Current version {Constants.CurrentVersion} is installed.";
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            if (_isCheckingForUpdates || _isDownloadingUpdate)
            {
                return;
            }

            _isCheckingForUpdates = true;
            RefreshUpdateUi();

            try
            {
                _availableUpdate = await _updateService.CheckLatestAsync();
                if (_availableUpdate == null)
                {
                    UpdateStatusText.Text = $"Current version {Constants.CurrentVersion} is installed.";
                }
            }
            catch
            {
                _availableUpdate = null;
                UpdateStatusText.Text = "Could not check updates. Use Releases if you want to check manually.";
            }
            finally
            {
                _isCheckingForUpdates = false;
                RefreshUpdateUi();
            }
        }

        private async Task DownloadUpdateAsync()
        {
            if (_availableUpdate == null || _isCheckingForUpdates || _isDownloadingUpdate)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_availableUpdate.InstallerAssetUrl))
            {
                UpdateStatusText.Text = "This release does not include the setup installer. Open Releases to download manually.";
                return;
            }

            _isDownloadingUpdate = true;
            RefreshUpdateUi();

            try
            {
                var progress = new Progress<string>(message =>
                {
                    UpdateStatusText.Text = message;
                });
                await _updateService.DownloadInstallerAsync(_availableUpdate, progress);
            }
            catch
            {
                UpdateStatusText.Text = "Download failed. Use Releases to download the installer manually.";
            }
            finally
            {
                _isDownloadingUpdate = false;
                RefreshUpdateUi();
            }
        }

        private void SetThemeColors(
            string backgroundPrimary,
            string backgroundSecondary,
            string backgroundTertiary,
            string backgroundCanvas,
            string borderPrimary,
            string borderSecondary,
            string borderTertiary,
            string textPrimary,
            string textSecondary,
            string textTertiary,
            string accentPrimary,
            string accentPrimaryText)
        {
            SetThemeColor("Color.Background.Primary", backgroundPrimary);
            SetThemeColor("Color.Background.Secondary", backgroundSecondary);
            SetThemeColor("Color.Background.Tertiary", backgroundTertiary);
            SetThemeColor("Color.Background.Canvas", backgroundCanvas);
            SetThemeColor("Color.Border.Primary", borderPrimary);
            SetThemeColor("Color.Border.Secondary", borderSecondary);
            SetThemeColor("Color.Border.Tertiary", borderTertiary);
            SetThemeColor("Color.Text.Primary", textPrimary);
            SetThemeColor("Color.Text.Secondary", textSecondary);
            SetThemeColor("Color.Text.Tertiary", textTertiary);
            SetThemeColor("Color.Accent.Primary", accentPrimary);
            SetThemeColor("Color.Accent.PrimaryText", accentPrimaryText);
        }

        private void SetThemeColor(string key, string color)
        {
            Resources[key] = (Color)ColorConverter.ConvertFromString(color);
        }

        private async Task ApplyFamilyPreviewThemeAsync()
        {
            if (_isDisposed || App.IsShuttingDown) return;
            try
            {
                if (FamilyPreviewBrowser?.CoreWebView2 == null) return;
                await _familyPreviewWebViewService.ApplyThemeAsync(FamilyPreviewBrowser, SettingsManager.Settings.IsDarkTheme);
            }
            catch
            {
                // WebView2 can be between navigations while the settings panel changes.
            }
        }

        private async Task ApplyPrimaryBrowserThemeAsync()
        {
            if (_isDisposed || App.IsShuttingDown) return;
            try
            {
                if (AiWebBrowser?.CoreWebView2 == null) return;
                await _webService.ApplyPreferredColorSchemeAsync(AiWebBrowser, SettingsManager.Settings.IsDarkTheme);
            }
            catch
            {
                // Some third-party pages ignore or briefly reject emulation while navigating.
            }
        }

        private static SolidColorBrush BrushFromHex(string color)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }

        private static SolidColorBrush SoftBrush(byte alpha, string color)
        {
            Color parsed = (Color)ColorConverter.ConvertFromString(color);
            parsed.A = alpha;
            return new SolidColorBrush(parsed);
        }

        private Brush GetSelectedCardBorderBrush()
        {
            return SettingsManager.Settings.IsDarkTheme
                ? BrushFromHex("#EFFFFFFF")
                : (Brush)FindResource("Brush.Text.Primary");
        }

        private static Thickness GetDefaultCardBorderThickness()
        {
            return new Thickness(1.5);
        }

        private Thickness GetSelectedCardBorderThickness()
        {
            return SettingsManager.Settings.IsDarkTheme
                ? GetDefaultCardBorderThickness()
                : new Thickness(2.5);
        }

        private static string GetTargetDisplayName(AiServiceTarget target)
        {
            return target switch
            {
                AiServiceTarget.Build => "AI Studio",
                _ => "Gemini"
            };
        }

        private static string GetTargetReadyLabel(AiServiceTarget target)
        {
            return target switch
            {
                AiServiceTarget.Build => "Build tools ready",
                _ => "Gemini ready"
            };
        }

        private static string GetTargetLoadingLabel(AiServiceTarget target)
        {
            return target switch
            {
                AiServiceTarget.Build => "Loading AI Studio",
                _ => "Loading Gemini"
            };
        }

        private bool RequiresTargetNavigation(AiServiceTarget target)
        {
            if (!AiBrowserService.IsExpectedHost(AiWebBrowser?.Source, target))
            {
                return true;
            }

            return target == AiServiceTarget.Build && !IsRecommendedAiStudioModelUrl(AiWebBrowser?.Source);
        }

        private void EnsureRecommendedAiStudioTarget()
        {
            if (AiWebBrowser == null) return;
            if (AiWebBrowser.CoreWebView2 != null && RequiresTargetNavigation(AiServiceTarget.Build))
            {
                _webService.NavigateToTarget(AiWebBrowser, AiServiceTarget.Build);
            }

            UpdateFamilyStudioModelBadgeFromUrl();
        }

        private static bool IsRecommendedAiStudioModelUrl(Uri? source)
        {
            if (!AiBrowserService.IsExpectedHost(source, AiServiceTarget.Build))
            {
                return false;
            }

            string query = source?.Query ?? string.Empty;
            if (query.StartsWith("?", StringComparison.Ordinal))
            {
                query = query.Substring(1);
            }

            foreach (string part in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = part.Split(new[] { '=' }, 2);
                string name = Uri.UnescapeDataString(pair[0]);
                string value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
                if (string.Equals(name, "model", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(value, RecommendedAiStudioModel, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void SelectServiceTarget(AiServiceTarget target)
        {
            if (_isDisposed || App.IsShuttingDown) return;
            if (_activeTarget == target)
            {
                UpdateServiceTargetVisuals();
                if (target == AiServiceTarget.Build)
                {
                    EnsureRecommendedAiStudioTarget();
                }
                return;
            }

            _activeTarget = target;
            HideComposer();
            HideSettingsPanel();
            HideAboutPanel();
            if (_activeTarget == AiServiceTarget.Build)
            {
                ShowBuildPipelinePanel(_buildFunctionId);
            }
            else
            {
                HideBuildPipelinePanel();
            }
            UpdateServiceTargetVisuals();

            try
            {
                if (AiWebBrowser?.CoreWebView2 != null && RequiresTargetNavigation(_activeTarget))
                {
                    _webService.NavigateToTarget(AiWebBrowser, _activeTarget);
                    SetPaneState(PaneState.BrowserLoading, GetTargetLoadingLabel(_activeTarget));
                }
                else
                {
                    SetPaneState(PaneState.Ready, GetTargetReadyLabel(_activeTarget));
                }
            }
            catch
            {
                SetPaneState(PaneState.UnsupportedPage, $"{GetTargetDisplayName(_activeTarget)} could not be opened.");
            }
        }

        private void UpdateServiceTargetVisuals()
        {
            if (RendersTabButton == null || BuildTabButton == null) return;

            ResetServiceTabButton(RendersTabButton);
            ResetServiceTabButton(BuildTabButton);

            Button activeButton = _activeTarget switch
            {
                AiServiceTarget.Build => BuildTabButton,
                _ => RendersTabButton
            };

            string accent = _activeTarget switch
            {
                AiServiceTarget.Build => "#4CAF82",
                _ => "#4A8FC0"
            };

            activeButton.Foreground = BrushFromHex(accent);
            activeButton.Background = SoftBrush(18, accent);
            activeButton.BorderBrush = BrushFromHex(accent);

            if (ServiceTextChip != null)
            {
                ServiceTextChip.Visibility = _activeTarget == AiServiceTarget.Renders ? Visibility.Collapsed : Visibility.Visible;
                ServiceTextChip.Background = SoftBrush(30, accent);
                ServiceTextChip.BorderBrush = SoftBrush(70, accent);
                ServiceTextDot.Fill = BrushFromHex(accent);
                ServiceTextChipLabel.Foreground = BrushFromHex(accent);
                ServiceTextChipLabel.Text = "Build Tools";
            }

            if (RenderModeSegment != null)
            {
                RenderModeSegment.Visibility = Visibility.Collapsed;
            }

            bool isBuild = _activeTarget == AiServiceTarget.Build;
            if (ActionRowBorder != null) ActionRowBorder.Visibility = isBuild ? Visibility.Collapsed : Visibility.Visible;
            if (CaptureButton != null) CaptureButton.Visibility = isBuild ? Visibility.Collapsed : Visibility.Visible;
            if (RenderModeSegment != null) RenderModeSegment.Visibility = isBuild ? Visibility.Collapsed : RenderModeSegment.Visibility;
            if (ServiceTextChip != null && isBuild) ServiceTextChip.Visibility = Visibility.Collapsed;

            if (CaptureButton != null)
            {
                CaptureButton.Background = _activeTarget switch
                {
                    AiServiceTarget.Build => BrushFromHex("#2A6F52"),
                    _ => BrushFromHex("#3A6F96")
                };
                CaptureButton.Foreground = _activeTarget switch
                {
                    AiServiceTarget.Build => BrushFromHex("#D4F5E9"),
                    _ => BrushFromHex("#E8F2FB")
                };
                CaptureButton.BorderBrush = (Brush)FindResource("Brush.Transparent");
            }
        }

        private void ResetServiceTabButton(Button button)
        {
            button.Foreground = (Brush)FindResource("Brush.Text.Tertiary");
            button.Background = (Brush)FindResource("Brush.Transparent");
            button.BorderBrush = (Brush)FindResource("Brush.Transparent");
        }

        private PromptPreset GetCurrentActionPreset()
        {
            var preset = PromptPresetCatalog.GetById(SettingsManager.Settings.PromptPresetId);
            return preset.Id == CustomPromptPresetId || preset.Mode == PromptMode.Trends || preset.Mode == PromptMode.Build
                ? PromptPresetCatalog.GetDefaultForMode(PromptMode.Visualize)
                : preset;
        }

        private PromptPreset GetCurrentComposerPreset()
        {
            return PromptPresetCatalog.GetById(_composerPresetId);
        }

        private PromptMode ResolveInitialComposerMode(PromptPreset preset)
        {
            PromptMode mode = SettingsManager.Settings.DefaultPromptMode switch
            {
                "Always Visualize" => PromptMode.Visualize,
                "Always Trends" => PromptMode.Visualize,
                "Always Build" => PromptMode.Visualize,
                _ => preset.Mode == PromptMode.None ? PromptMode.Visualize : preset.Mode
            };

            return mode == PromptMode.Trends || mode == PromptMode.Build ? PromptMode.Visualize : mode;
        }

        private bool IsPresetVisibleInComposer(PromptPreset preset)
        {
            return true;
        }

        private void SelectActionPreset(PromptPreset preset)
        {
            if (preset.Id == CustomPromptPresetId)
            {
                UpdateModeBadge(_currentComposerMode);
                return;
            }

            if (preset.Mode == PromptMode.Build)
            {
                SettingsManager.Settings.BuildFunctionId = preset.Id;
            }
            else
            {
                SettingsManager.Settings.PromptPresetId = preset.Id;
            }

            SettingsManager.Save();
            UpdateModeBadge(preset.Mode);
        }

        private void LoadBuildControls()
        {
            if (FamilyImagePromptTextBox != null && string.IsNullOrWhiteSpace(FamilyImagePromptTextBox.Text))
            {
                FamilyImagePromptTextBox.Text = PromptPresetCatalog.BuildPrompt("family", string.Empty, false);
            }
        }

        private void UpdateModeBadge(PromptMode mode)
        {
            UpdateRendersModeSegment(mode);

            if (_activeTarget == AiServiceTarget.Build)
            {
                ComposerTitleText.Text = "Build from capture";
                SendComposerButton.Background = BrushFromHex("#2A6F52");
                SendComposerButton.Foreground = BrushFromHex("#D4F5E9");
                SendComposerIcon.Text = "B";
                SendComposerLabel.Text = "Send to AI Studio";
                PromptLabel.Text = "Build prompt";
                PromptExpandButton.Foreground = BrushFromHex("#4CAF82");
                PrivacyNote.Background = SoftBrush(18, "#4CAF82");
                PrivacyNote.BorderBrush = SoftBrush(46, "#4CAF82");
                PrivacyNoteText.Foreground = BrushFromHex("#5DAA84");
                PrivacyNoteText.Text = string.Equals(_buildFunctionId, "family", StringComparison.OrdinalIgnoreCase)
                    ? "After sending, copy or acquire the Family JSON from AI Studio. Preview still gates Create RFA."
                    : "Build prompts produce artifacts or explanations. Revit conversion is available in Pro.";
                ComposerContextButton.Foreground = BrushFromHex("#4CAF82");
                ComposerPreviewBorder.Background = BrushFromHex("#132820");
                ComposerPreviewBorder.BorderBrush = SoftBrush(80, "#4CAF82");
                ComposerPreviewIcon.Foreground = SoftBrush(110, "#4CAF82");
                ComposerPreviewLabel.Foreground = SoftBrush(110, "#4CAF82");
                RetakeButton.Content = string.Equals(_buildFunctionId, "family", StringComparison.OrdinalIgnoreCase)
                    ? "Browse"
                    : "Retake";
            }
            else
            {
                RetakeButton.Content = "Retake";
            }
        }

        private void UpdateRendersModeSegment(PromptMode mode)
        {
            if (RendersVizModeButton == null || RendersReviewModeButton == null) return;

            ResetSegmentButton(RendersVizModeButton);
            ResetSegmentButton(RendersReviewModeButton);

            Button activeButton = RendersVizModeButton;
            string accent = "#9B8FE8";

            activeButton.Background = SoftBrush(42, accent);
            activeButton.Foreground = BrushFromHex("#B8AFEE");
        }

        private void ResetSegmentButton(Button button)
        {
            button.Foreground = (Brush)FindResource("Brush.Text.Tertiary");
            button.Background = (Brush)FindResource("Brush.Background.Tertiary");
        }



        private void SetComposerMode(PromptMode mode, bool selectDefaultIfNeeded)
        {
            _currentComposerMode = mode == PromptMode.None ? PromptMode.Visualize : mode;

            bool isRenders = _activeTarget == AiServiceTarget.Renders;
            bool isBuild = _activeTarget == AiServiceTarget.Build;

            ComposerModeSelector.Visibility = Visibility.Collapsed;
            RenderTypeHeading.Visibility = isRenders ? Visibility.Visible : Visibility.Collapsed;
            VisualizePresetScroll.Visibility = isRenders ? Visibility.Visible : Visibility.Collapsed;
            VisualizePresetGrid.Visibility = isRenders ? Visibility.Visible : Visibility.Collapsed;
            StyleTypeHeading.Visibility = isRenders ? Visibility.Visible : Visibility.Collapsed;
            TrendsPresetScroll.Visibility = isRenders ? Visibility.Visible : Visibility.Collapsed;
            TrendsPresetGrid.Visibility = isRenders ? Visibility.Visible : Visibility.Collapsed;
            TrendHelperText.Visibility = Visibility.Collapsed;
            ModifiersAccordion.Visibility = isRenders ? Visibility.Visible : Visibility.Collapsed;
            PromptOptionsBar.Visibility = Visibility.Collapsed;

            var selected = GetCurrentComposerPreset();
            PromptMode requiredMode = isBuild ? PromptMode.Build : PromptMode.Visualize;

            if (selectDefaultIfNeeded && (selected.Mode != requiredMode || !IsPresetVisibleInComposer(selected)))
            {
                selected = PromptPresetCatalog.GetDefaultForMode(requiredMode);
                _composerPresetId = selected.Id;
                SelectActionPreset(selected);
            }

            UpdateComposerModeVisuals(isBuild ? PromptMode.Build : PromptMode.Visualize);
            UpdateSelectedPresetCard(selected.Id);
            UpdateComposerPromptFromCurrentSelection();
            if (selected.Id != CustomPromptPresetId)
            {
                CollapsePromptPreview();
            }
        }

        private void UpdateComposerModeVisuals(PromptMode mode)
        {
            ResetModeButton(VisualizeModeButton);
            ResetModeButton(TrendsModeButton);
            ResetModeButton(ReviewModeButton);

            Button active = mode switch
            {
                PromptMode.Trends => TrendsModeButton,
                PromptMode.Build => ReviewModeButton,
                _ => VisualizeModeButton
            };

            string accent = mode switch
            {
                PromptMode.Trends => "#C89A3C",
                PromptMode.Build => "#4CAF82",
                _ => "#9B8FE8"
            };

            active.Foreground = BrushFromHex(accent);
            active.Background = SoftBrush(32, accent);
            active.BorderBrush = SoftBrush(96, accent);

            switch (mode)
            {
                case PromptMode.Trends:
                    ComposerTitleText.Text = "Visualize capture";
                    SendComposerButton.Background = BrushFromHex("#7A5A18");
                    SendComposerButton.Foreground = BrushFromHex("#F5E0A0");
                    SendComposerIcon.Text = "T";
                    SendComposerLabel.Text = "Render Style";
                    PromptLabel.Text = "Studio style prompt";
                    PromptExpandButton.Foreground = BrushFromHex("#C89A3C");
                    PrivacyNote.Background = SoftBrush(18, "#C89A3C");
                    PrivacyNote.BorderBrush = SoftBrush(46, "#C89A3C");
                    PrivacyNoteText.Foreground = BrushFromHex("#C89A3C");
                    PrivacyNoteText.Text = "Only the capture and optional view metadata are sent. The Revit model is not exported or modified.";
                    ComposerContextTitle.Text = "Spatial reference included";
                    ComposerContextButton.Foreground = BrushFromHex("#C89A3C");
                    ComposerPreviewBorder.Background = BrushFromHex("#221E16");
                    ComposerPreviewBorder.BorderBrush = SoftBrush(80, "#C89A3C");
                    ComposerPreviewIcon.Foreground = SoftBrush(110, "#C89A3C");
                    ComposerPreviewLabel.Foreground = SoftBrush(110, "#C89A3C");
                    break;
                case PromptMode.Build:
                    ComposerTitleText.Text = "Build from capture";
                    SendComposerButton.Background = BrushFromHex("#2A6F52");
                    SendComposerButton.Foreground = BrushFromHex("#D4F5E9");
                    SendComposerIcon.Text = "B";
                    SendComposerLabel.Text = "Send to AI Studio";
                    PromptLabel.Text = "Build prompt";
                    PromptExpandButton.Foreground = BrushFromHex("#4CAF82");
                    PrivacyNote.Background = SoftBrush(18, "#4CAF82");
                    PrivacyNote.BorderBrush = SoftBrush(46, "#4CAF82");
                    PrivacyNoteText.Foreground = BrushFromHex("#5DAA84");
                    PrivacyNoteText.Text = string.Equals(_buildFunctionId, "family", StringComparison.OrdinalIgnoreCase)
                        ? "After sending, copy or acquire the Family JSON from AI Studio. Preview still gates Create RFA."
                        : "Build mode creates controlled artifacts or explanations. Revit conversion is available in Pro.";
                    ComposerContextTitle.Text = "Revit context included";
                    ComposerContextButton.Foreground = BrushFromHex("#4CAF82");
                    ComposerPreviewBorder.Background = BrushFromHex("#132820");
                    ComposerPreviewBorder.BorderBrush = SoftBrush(80, "#4CAF82");
                    ComposerPreviewIcon.Foreground = SoftBrush(110, "#4CAF82");
                    ComposerPreviewLabel.Foreground = SoftBrush(110, "#4CAF82");
                    break;
                default:
                    ComposerTitleText.Text = "Visualize capture";
                    SendComposerButton.Background = BrushFromHex("#5B4FBC");
                    SendComposerButton.Foreground = BrushFromHex("#EBE8FF");
                    SendComposerIcon.Text = "*";
                    SendComposerLabel.Text = "Render with Gemini";
                    PromptLabel.Text = "Render prompt";
                    PromptExpandButton.Foreground = BrushFromHex("#9B8FE8");
                    PrivacyNote.Background = SoftBrush(18, "#9B8FE8");
                    PrivacyNote.BorderBrush = SoftBrush(46, "#9B8FE8");
                    PrivacyNoteText.Foreground = BrushFromHex("#B8AFEE");
                    PrivacyNoteText.Text = "Only the capture and optional view metadata are sent. The Revit model is not exported or modified.";
                    ComposerContextTitle.Text = "Spatial reference included";
                    ComposerContextButton.Foreground = BrushFromHex("#9B8FE8");
                    ComposerPreviewBorder.Background = BrushFromHex("#1E2A38");
                    ComposerPreviewBorder.BorderBrush = SoftBrush(80, "#9B8FE8");
                    ComposerPreviewIcon.Foreground = SoftBrush(110, "#9B8FE8");
                    ComposerPreviewLabel.Foreground = SoftBrush(110, "#9B8FE8");
                    break;
            }
        }

        private void ResetModeButton(Button button)
        {
            button.Foreground = (Brush)FindResource("Brush.Text.Tertiary");
            button.Background = (Brush)FindResource("Brush.Background.Secondary");
            button.BorderBrush = (Brush)FindResource("Brush.Border.Secondary");
        }

        private void UpdateSelectedPresetCard(string presetId)
        {
            foreach (Button button in GetVisualizePresetButtons())
            {
                if (button.Tag is not string tag) continue;
                button.BorderBrush = SoftBrush(28, "#FFFFFF");
                button.BorderThickness = GetDefaultCardBorderThickness();
                button.Foreground = BrushFromHex("#D9FFFFFF");
            }

            Button? selected = FindCardButtonByTag(presetId);
            if (selected == null) return;

            selected.BorderBrush = GetSelectedCardBorderBrush();
            selected.BorderThickness = GetSelectedCardBorderThickness();
            selected.Foreground = BrushFromHex("#FFFFFFFF");
            UpdateStudioStyleVisuals();
        }

        private void UpdateStudioStyleVisuals()
        {
            foreach (Button button in GetStudioPresetButtons())
            {
                if (button.Tag is not string tag) continue;
                bool selected = string.Equals(tag, _selectedStudioPresetId, StringComparison.Ordinal);
                button.BorderBrush = selected ? GetSelectedCardBorderBrush() : SoftBrush(28, "#FFFFFF");
                button.BorderThickness = selected ? GetSelectedCardBorderThickness() : GetDefaultCardBorderThickness();
                button.Foreground = selected ? BrushFromHex("#FFFFFFFF") : BrushFromHex("#D0FFFFFF");
            }
        }

        private Button[] GetVisualizePresetButtons()
        {
            return new[]
            {
                AsCapturedRenderCard,
                ExteriorRenderCard,
                InteriorAtmosphereCard,
                MaterialStudyCard,
                NightSceneCard,
                AerialSiteCard,
                SketchRenderCard,
                MarkerRenderCard
            };
        }

        private Button[] GetStudioPresetButtons()
        {
            return new[]
            {
                GlyphCard,
                SignalCard,
                VellumCard,
                FauveCard,
                MirageCard
            };
        }

        private Button? FindCardButtonByTag(string tag)
        {
            foreach (Button button in GetVisualizePresetButtons().Concat(GetStudioPresetButtons()))
            {
                if (button.Tag as string == tag)
                {
                    return button;
                }
            }

            return null;
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed)
                {
                    yield return typed;
                }

                foreach (T descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private void UpdateComposerPromptFromCurrentSelection()
        {
            var preset = GetCurrentComposerPreset();
            if (preset.Id == CustomPromptPresetId)
            {
                ComposerPromptText.IsReadOnly = false;
                PromptLabel.Text = "Custom";
                PromptExpandButton.Visibility = Visibility.Visible;
                PromptOptionsBar.Visibility = Visibility.Collapsed;

                _isUpdatingComposerPrompt = true;
                ComposerPromptText.Text = BuildCustomPromptDisplay();
                _isUpdatingComposerPrompt = false;
                ApplyPromptExpansion();
                ComposerPromptText.Focus();
                ComposerPromptText.CaretIndex = GetCurrentCustomPromptText().Length;
                UpdateModeBadge(_currentComposerMode);
                UpdateComposerContextCard();
                return;
            }

            ComposerPromptText.IsReadOnly = true;
            PromptExpandButton.Visibility = Visibility.Visible;
            PromptOptionsBar.Visibility = Visibility.Collapsed;
            if (_currentComposerMode == PromptMode.Visualize)
            {
                PromptLabel.Text = "Render prompt";
            }

            _isUpdatingComposerPrompt = true;
            string builtPrompt = _activeTarget == AiServiceTarget.Renders
                ? BuildRendersPrompt(preset)
                : PromptPresetCatalog.BuildPrompt(
                    preset.Id,
                    _pendingRevitContext,
                    _isContextIncludedInComposer,
                    GetSelectedPromptModifierIds());
            ComposerPromptText.Text = builtPrompt;
            _isUpdatingComposerPrompt = false;
            UpdateModeBadge(preset.Mode);
            UpdateComposerContextCard();
        }

        private string BuildRendersPrompt(PromptPreset renderPreset)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(renderPreset.Prompt))
            {
                parts.Add(renderPreset.Prompt);
            }

            string selectedStudioPresetId = _selectedStudioPresetId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(selectedStudioPresetId))
            {
                var stylePreset = PromptPresetCatalog.GetById(selectedStudioPresetId);
                if (stylePreset.Mode == PromptMode.Trends && !string.IsNullOrWhiteSpace(stylePreset.Prompt))
                {
                    parts.Add("Studio style layer:\n" + stylePreset.Prompt);
                }
            }

            if (_isContextIncludedInComposer && !string.IsNullOrWhiteSpace(_pendingRevitContext))
            {
                parts.Add(_pendingRevitContext.Trim());
            }

            var keep = new List<string>();
            var skip = new List<string>();
            foreach (string modifierId in GetSelectedPromptModifierIds())
            {
                foreach (var modifier in PromptPresetCatalog.Modifiers)
                {
                    if (modifier.Id != modifierId) continue;
                    if (modifier.Id.StartsWith("preserve-", StringComparison.Ordinal))
                    {
                        keep.Add(modifier.Label);
                    }
                    else
                    {
                        skip.Add(modifier.Label);
                    }
                    break;
                }
            }

            if (keep.Count > 0 || skip.Count > 0)
            {
                var directive = new List<string>();
                if (keep.Count > 0)
                {
                    directive.Add("Preserve: " + string.Join(", ", keep) + ".");
                }

                if (skip.Count > 0)
                {
                    directive.Add("Exclude: " + string.Join(", ", skip) + ".");
                }

                parts.Add(string.Join(" ", directive));
            }

            if (parts.Count > 0)
            {
                parts.Add("Use the image as the primary source. Preserve visible geometry and proportions unless the prompt explicitly asks for representational reinterpretation.");
            }

            return string.Join("\n\n", parts);
        }

        private IEnumerable<string> GetSelectedPromptModifierIds()
        {
            if (_currentComposerMode == PromptMode.Build)
            {
                yield break;
            }

            foreach (WpfToggleButton checkBox in GetPromptModifierChips())
            {
                if (checkBox.IsChecked == true && checkBox.Tag is string modifierId)
                {
                    yield return modifierId;
                }
            }
        }

        private WpfToggleButton[] GetPromptModifierChips()
        {
            return new[]
            {
                PreserveGeometryCheck,
                PreserveCameraCheck,
                PreserveMaterialsCheck,
                PreserveRatioCheck,
                AvoidInventedElementsCheck,
                AvoidTextCheck,
                AvoidPeopleCheck,
                AvoidVehiclesCheck
            };
        }

        private void ResetPromptModifiers()
        {
            foreach (WpfToggleButton checkBox in GetPromptModifierChips())
            {
                checkBox.IsChecked = false;
            }

            UpdatePromptModifierChipVisuals();
        }

        private void UpdatePromptModifierChipVisuals()
        {
            if (PreserveGeometryCheck == null) return;

            foreach (WpfToggleButton chip in GetPromptModifierChips())
            {
                UpdatePromptModifierChipVisual(chip);
            }

            UpdateModifierSummary();
        }

        private void UpdatePromptModifierChipVisual(WpfToggleButton chip)
        {
            if (chip.Tag is not string modifierId) return;

            bool isActive = chip.IsChecked == true;
            bool isKeep = modifierId.StartsWith("preserve-", StringComparison.Ordinal);
            bool isDark = SettingsManager.Settings.IsDarkTheme;

            if (!isActive)
            {
                chip.Background = (Brush)FindResource("Brush.Background.Tertiary");
                chip.BorderBrush = (Brush)FindResource("Brush.Border.Tertiary");
                chip.Foreground = (Brush)FindResource("Brush.Text.Tertiary");
                return;
            }

            string foreground = isKeep
                ? (isDark ? "#78D6AC" : "#197A55")
                : (isDark ? "#E09090" : "#9A3030");
            string background = isKeep
                ? (isDark ? "#243A32" : "#EAF7F1")
                : (isDark ? "#3A2424" : "#FBEAEA");
            string border = isKeep
                ? (isDark ? "#4CAF82" : "#8ACAAE")
                : (isDark ? "#D07070" : "#DA9A9A");

            chip.Foreground = BrushFromHex(foreground);
            chip.Background = BrushFromHex(background);
            chip.BorderBrush = BrushFromHex(border);
        }

        private void UpdateModifierSummary()
        {
            if (ModifierKeepCountText == null || ModifierSkipCountText == null) return;

            int keep = 0;
            int skip = 0;
            foreach (WpfToggleButton chip in GetPromptModifierChips())
            {
                if (chip.IsChecked != true || chip.Tag is not string modifierId) continue;
                if (modifierId.StartsWith("preserve-", StringComparison.Ordinal))
                {
                    keep++;
                }
                else
                {
                    skip++;
                }
            }

            ModifierKeepCountText.Text = keep + " keep";
            ModifierSkipCountText.Text = skip + " skip";
        }

        private void ApplyModifierExpansion()
        {
            if (ModifiersBody == null) return;

            ModifiersBody.Visibility = _isModifiersExpanded ? Visibility.Visible : Visibility.Collapsed;
            if (ModifiersChevronText != null)
            {
                ModifiersChevronText.Text = _isModifiersExpanded ? "^" : "v";
            }
        }

        private void CollapsePromptPreview()
        {
            _isPromptExpanded = false;
            ApplyPromptExpansion();
        }

        private void ApplyPromptExpansion()
        {
            ComposerPromptText.Visibility = _isPromptExpanded ? Visibility.Visible : Visibility.Collapsed;
            ComposerPromptText.MaxHeight = _isPromptExpanded ? 120 : 72;
            ComposerPromptText.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            if (PromptAccordionStateText != null)
            {
                PromptAccordionStateText.Text = _isPromptExpanded ? "Hide" : "Preview";
            }

            if (PromptChevronText != null)
            {
                PromptChevronText.Text = _isPromptExpanded ? "^" : "v";
            }
        }

        private string GetComposerSendPrompt()
        {
            if (_composerPresetId == CustomPromptPresetId)
            {
                return BuildCustomPromptForSending();
            }

            return ComposerPromptText.Text ?? string.Empty;
        }

        private string BuildCustomPromptForSending()
        {
            var parts = new List<string>();
            string customPrompt = GetCurrentCustomPromptText().Trim();
            if (!string.IsNullOrWhiteSpace(customPrompt))
            {
                parts.Add(customPrompt);
            }

            foreach (string section in BuildCustomPromptGeneratedSections())
            {
                parts.Add(section);
            }

            if (parts.Count > 0)
            {
                parts.Add("Use the image as the primary source. Preserve visible geometry and proportions unless the prompt explicitly asks for representational reinterpretation.");
            }

            return string.Join("\n\n", parts);
        }

        private string BuildCustomPromptDisplay()
        {
            string customPrompt = GetCurrentCustomPromptText();
            var generatedSections = BuildCustomPromptGeneratedSections();
            if (generatedSections.Count == 0)
            {
                return customPrompt;
            }

            string generatedBody = string.Join("\n\n", generatedSections);
            return string.IsNullOrWhiteSpace(customPrompt)
                ? "\n\n" + generatedBody
                : customPrompt.TrimEnd() + "\n\n" + generatedBody;
        }

        private List<string> BuildCustomPromptGeneratedSections()
        {
            var parts = new List<string>();

            if (_isContextIncludedInComposer && !string.IsNullOrWhiteSpace(_pendingRevitContext))
            {
                parts.Add(_pendingRevitContext.Trim());
            }

            var modifierRules = new List<string>();
            foreach (var modifierId in GetSelectedPromptModifierIds())
            {
                foreach (var modifier in PromptPresetCatalog.Modifiers)
                {
                    if (modifier.Id == modifierId)
                    {
                        modifierRules.Add("- " + modifier.PromptRule);
                        break;
                    }
                }
            }

            if (modifierRules.Count > 0)
            {
                parts.Add("Additional prompt options:\n" + string.Join("\n", modifierRules));
            }

            return parts;
        }

        private string GetCurrentCustomPromptText()
        {
            var settings = SettingsManager.Settings;
            return _currentComposerMode == PromptMode.Trends
                ? settings.TrendsCustomPromptText ?? string.Empty
                : settings.VisualizeCustomPromptText ?? string.Empty;
        }

        private void SetCurrentCustomPromptText(string prompt)
        {
            var settings = SettingsManager.Settings;
            string generatedBody = string.Join("\n\n", BuildCustomPromptGeneratedSections());
            string userPrompt = ExtractCustomPromptUserText(prompt, generatedBody);
            if (_currentComposerMode == PromptMode.Trends)
            {
                settings.TrendsCustomPromptText = userPrompt;
            }
            else
            {
                settings.VisualizeCustomPromptText = userPrompt;
            }

            SettingsManager.Save();
        }

        private static string ExtractCustomPromptUserText(string prompt, string generatedBody)
        {
            if (string.IsNullOrWhiteSpace(generatedBody))
            {
                return prompt;
            }

            string normalizedPrompt = NormalizeLineEndings(prompt);
            string normalizedGeneratedBody = NormalizeLineEndings(generatedBody);
            string generatedSuffix = "\n\n" + normalizedGeneratedBody;

            if (!normalizedPrompt.EndsWith(generatedSuffix, StringComparison.Ordinal))
            {
                return prompt;
            }

            string userPrompt = normalizedPrompt.Substring(0, normalizedPrompt.Length - generatedSuffix.Length);
            return userPrompt.TrimEnd('\n');
        }

        private static string NormalizeLineEndings(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private string ComposePrompt(string revitContext)
        {
            if (_activeTarget == AiServiceTarget.Build)
            {
                string buildPrompt = PromptPresetCatalog.BuildPrompt(
                    "family",
                    revitContext,
                    SettingsManager.Settings.IncludeRevitContext);

                return buildPrompt;
            }

            string presetId = SettingsManager.Settings.PromptPresetId;
            if (presetId == CustomPromptPresetId)
            {
                presetId = PromptPresetCatalog.GetDefaultForMode(PromptMode.Visualize).Id;
            }

            bool includeContext = SettingsManager.Settings.IncludeRevitContext;
            return PromptPresetCatalog.BuildPrompt(presetId, revitContext, includeContext);
        }

        private bool CanStartCapture()
        {
            return !IsCaptureBusy();
        }

        private bool IsPanelOpen()
        {
            return ComposerPanel.Visibility == Visibility.Visible ||
                   BuildPipelinePanel.Visibility == Visibility.Visible ||
                   SettingsPanel.Visibility == Visibility.Visible ||
                   AboutPanel.Visibility == Visibility.Visible;
        }

        private bool IsCaptureBusy()
        {
            return _paneState == PaneState.Capturing ||
                   _paneState == PaneState.ProcessingCapture ||
                   _paneState == PaneState.Sending;
        }

        private bool CanOpenSettings()
        {
            return ComposerPanel.Visibility != Visibility.Visible &&
                   SettingsPanel.Visibility != Visibility.Visible &&
                   AboutPanel.Visibility != Visibility.Visible &&
                   !IsCaptureBusy();
        }

        private bool CanOpenAbout()
        {
            return ComposerPanel.Visibility != Visibility.Visible &&
                   SettingsPanel.Visibility != Visibility.Visible &&
                   AboutPanel.Visibility != Visibility.Visible &&
                   !IsCaptureBusy();
        }

        private void SetPaneState(PaneState state, string? detail = null)
        {
            if (_isDisposed || App.IsShuttingDown) return;
            _sentStateTimer.Stop();
            _paneState = state;
            string message = detail ?? GetStateLabel(state);
            SetStatus(message);
            UpdateControlAvailability();
            UpdateInlineBanner(state, detail);

            if (state == PaneState.Sent)
            {
                _sentStateTimer.Start();
            }
        }

        private string GetStateLabel(PaneState state)
        {
            return state switch
            {
                PaneState.BrowserLoading => GetTargetLoadingLabel(_activeTarget),
                PaneState.LoginRequired => $"{GetTargetDisplayName(_activeTarget)} sign-in needed",
                PaneState.Ready => GetTargetReadyLabel(_activeTarget),
                PaneState.Capturing => "Capturing view",
                PaneState.ProcessingCapture => "Preparing capture",
                PaneState.Composing => "Ready to send",
                PaneState.Sending => "Sending capture",
                PaneState.Sent => "Capture sent",
                PaneState.SendFailed => "Send failed",
                PaneState.CaptureFailed => "Capture failed",
                PaneState.UnsupportedPage => $"Open {GetTargetDisplayName(_activeTarget)} to send",
                _ => "Ready"
            };
        }

        private void SetStatus(string message)
        {
            if (StatusText != null)
            {
                StatusText.Text = message;
            }
        }

        private void UpdateControlAvailability()
        {
            bool canCapture = CanStartCapture() && !IsPanelOpen();
            bool canSend = _paneState == PaneState.Composing && !string.IsNullOrWhiteSpace(_pendingBase64Image);

            if (CaptureButton != null) CaptureButton.IsEnabled = canCapture;
            if (SnipButton != null) SnipButton.IsEnabled = canCapture;
            if (AboutButton != null) AboutButton.IsEnabled = CanOpenAbout();
            if (SettingsButton != null) SettingsButton.IsEnabled = CanOpenSettings();
            if (SendComposerButton != null) SendComposerButton.IsEnabled = canSend;
            if (BuildPipelinePrimaryButton != null) BuildPipelinePrimaryButton.IsEnabled = !IsCaptureBusy();
            if (FamilyImportLatestJsonButton != null) FamilyImportLatestJsonButton.IsEnabled = !IsCaptureBusy() && AiWebBrowser?.CoreWebView2 != null;
            if (FamilyStudioImportLatestJsonButton != null) FamilyStudioImportLatestJsonButton.IsEnabled = !IsCaptureBusy() && AiWebBrowser?.CoreWebView2 != null;
            if (FamilyPasteJsonButton != null) FamilyPasteJsonButton.IsEnabled = !IsCaptureBusy();
            if (FamilyAttachJsonButton != null) FamilyAttachJsonButton.IsEnabled = !IsCaptureBusy();
            if (FamilySendToStudioButton != null) FamilySendToStudioButton.IsEnabled = !IsCaptureBusy() && !string.IsNullOrWhiteSpace(_familyReferenceBase64Image);
            if (FamilyUseInlineJsonButton != null) FamilyUseInlineJsonButton.IsEnabled = !IsCaptureBusy();
            if (CaptureButton != null) CaptureButton.IsEnabled = !IsCaptureBusy();
            if (InlineBannerAction != null) InlineBannerAction.IsEnabled = !IsCaptureBusy();

            CapturingOverlay.Visibility = _paneState == PaneState.Capturing || _paneState == PaneState.ProcessingCapture
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdateWebViewVisibility();
            UpdateServiceTargetVisuals();
            UpdateComposerContextCard();
            UpdateStatusChipVisuals();
        }

        private void UpdateStatusChipVisuals()
        {
            Brush stateBrush = GetStateBrush(_paneState);
            StatusText.Foreground = stateBrush;
            StatusDot.Fill = stateBrush;
            StatusChip.BorderBrush = stateBrush;
            StatusChip.Background = GetSoftStateBrush(_paneState);
        }

        private Brush GetStateBrush(PaneState state)
        {
            string resource = state switch
            {
                PaneState.Ready or PaneState.Sent => "Brush.State.Ready",
                PaneState.LoginRequired or PaneState.UnsupportedPage => "Brush.State.Warning",
                PaneState.SendFailed or PaneState.CaptureFailed => "Brush.State.Error",
                PaneState.Capturing or PaneState.ProcessingCapture or PaneState.Composing or PaneState.Sending => "Brush.State.Capturing",
                _ => "Brush.Text.Tertiary"
            };

            return (Brush)FindResource(resource);
        }

        private static Brush GetSoftStateBrush(PaneState state)
        {
            Color color = state switch
            {
                PaneState.Ready or PaneState.Sent => Color.FromArgb(32, 76, 175, 130),
                PaneState.LoginRequired or PaneState.UnsupportedPage => Color.FromArgb(32, 200, 154, 60),
                PaneState.SendFailed or PaneState.CaptureFailed => Color.FromArgb(32, 217, 85, 88),
                PaneState.Capturing or PaneState.ProcessingCapture or PaneState.Composing or PaneState.Sending => Color.FromArgb(32, 127, 135, 212),
                _ => Color.FromRgb(45, 46, 48)
            };

            return new SolidColorBrush(color);
        }

        private void UpdateInlineBanner(PaneState state, string? detail)
        {
            bool show = state == PaneState.LoginRequired ||
                        state == PaneState.UnsupportedPage ||
                        state == PaneState.SendFailed ||
                        state == PaneState.CaptureFailed;

            InlineBanner.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!show) return;

            bool isError = state == PaneState.SendFailed || state == PaneState.CaptureFailed;
            Brush brush = isError ? (Brush)FindResource("Brush.State.Error") : (Brush)FindResource("Brush.State.Warning");
            InlineBannerText.Foreground = brush;
            InlineBannerIcon.Foreground = brush;
            InlineBannerAction.Foreground = brush;
            InlineBannerBox.BorderBrush = brush;
            InlineBannerText.Text = detail ?? GetBannerText(state);
            InlineBannerAction.Content = state == PaneState.LoginRequired ? "Sign in" : "Retry";
        }

        private string GetBannerText(PaneState state)
        {
            return state switch
            {
                PaneState.LoginRequired => $"{GetTargetDisplayName(_activeTarget)} sign-in required.",
                PaneState.UnsupportedPage => $"Open {GetTargetDisplayName(_activeTarget)} before sending a capture.",
                PaneState.SendFailed => $"Send failed. Check {GetTargetDisplayName(_activeTarget)} and try again.",
                PaneState.CaptureFailed => "Capture failed.",
                _ => string.Empty
            };
        }

        private void UpdateWebViewVisibility()
        {
            bool familyStudioAcquire = FamilyStudioFooterOverlay != null &&
                                       FamilyStudioFooterOverlay.Visibility == Visibility.Visible;
            AiWebBrowser.Visibility = IsPanelOpen() && !familyStudioAcquire ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ShowBuildPipelinePanel(string functionId)
        {
            var preset = PromptPresetCatalog.GetById("family");
            if (preset.Mode != PromptMode.Build)
            {
                preset = PromptPresetCatalog.GetDefaultForMode(PromptMode.Build);
            }

            _buildFunctionId = preset.Id;
            SettingsManager.Settings.BuildFunctionId = preset.Id;
            SettingsManager.Save();

            BuildPipelinePanel.Visibility = Visibility.Visible;
            FamilyPipelinePanel.Visibility = Visibility.Visible;
            BuildGenericFooter.Visibility = Visibility.Collapsed;
            FamilyFooterStepper.Visibility = Visibility.Visible;
            BuildPipelineInfoCard.Visibility = Visibility.Collapsed;

            BuildPipelineSafetyText.Text = GetBuildPipelineSafetyText(preset.Id);
            UpdateFamilyPipelineUi();
            UpdateServiceTargetVisuals();
            UpdateComposerContextCard();
            UpdateWebViewVisibility();
            SetPaneState(PaneState.Ready, GetBuildPipelineReadyText(preset.Id));
        }

        private void HideBuildPipelinePanel()
        {
            if (BuildPipelinePanel != null)
            {
                BuildPipelinePanel.Visibility = Visibility.Collapsed;
            }

            if (FamilyStudioFooterOverlay != null)
            {
                FamilyStudioFooterOverlay.Visibility = Visibility.Collapsed;
            }

            UpdateWebViewVisibility();
        }

        private string GetBuildPipelineReadyText(string functionId)
        {
            return functionId switch
            {
                _ => "Family attach ready"
            };
        }

        private string GetBuildPipelineSafetyText(string functionId)
        {
            return functionId switch
            {
                _ => "Musait saves only valid Family JSON locally. Preview gates Create RFA."
            };
        }

        private void UpdateFamilyPipelineUi()
        {
            if (FamilyPipelinePanel == null) return;

            bool hasJson = !string.IsNullOrWhiteSpace(_familyJsonPath);
            bool jsonFileExists = hasJson && File.Exists(_familyJsonPath);
            bool hasReferenceImage = !string.IsNullOrWhiteSpace(_familyReferenceBase64Image);
            FamilyReferenceImage.Visibility = hasReferenceImage ? Visibility.Visible : Visibility.Collapsed;
            FamilyReferenceEmptyHint.Visibility = hasReferenceImage ? Visibility.Collapsed : Visibility.Visible;
            if (hasReferenceImage)
            {
                FamilyReferenceImage.Source = CreateBitmapImage(_familyReferenceBase64Image!);
            }
            else
            {
                FamilyReferenceImage.Source = null;
            }

            FamilySendToStudioButton.IsEnabled = hasReferenceImage && !IsCaptureBusy();
            FamilyPipelineJsonPathText.Text = hasJson ? _familyJsonPath : "No JSON attached";
            if (FamilyStudioFooterStatusText != null)
            {
                FamilyStudioFooterStatusText.Text = hasJson
                    ? "Family JSON acquired. Continue to Preview when ready."
                    : "Answer AI Studio questions until the complete Family JSON appears.";
            }

            if (!hasJson)
            {
                _familyPipelineStep = 1;
                if (_familyUiState is 4 or 5 or 6)
                {
                    _familyUiState = 0;
                }

                FamilyPipelineStatusText.Text = "Paste or browse a Family JSON definition.";
                FamilyPipelinePreviewText.Text = string.Empty;
                FamilyPreviewVisualText.Text = "Validated Family JSON will be summarized here.";
                FamilyPreviewComponentsText.Text = "-";
                FamilyPreviewParametersText.Text = "-";
            }

            ApplyFamilyViewState();
            UpdateFamilyPreviewBadgeVisual();
            UpdateFamilyFooterStepper();
        }

        private void ApplyFamilyViewState()
        {
            bool showStudioFooter = _activeTarget == AiServiceTarget.Build &&
                                    string.Equals(_buildFunctionId, "family", StringComparison.OrdinalIgnoreCase) &&
                                    _familyUiState == 3;

            if (FamilyStudioFooterOverlay != null)
            {
                FamilyStudioFooterOverlay.Visibility = showStudioFooter ? Visibility.Visible : Visibility.Collapsed;
            }

            if (showStudioFooter && BuildPipelinePanel != null)
            {
                BuildPipelinePanel.Visibility = Visibility.Collapsed;
            }
            else if (_activeTarget == AiServiceTarget.Build &&
                     string.Equals(_buildFunctionId, "family", StringComparison.OrdinalIgnoreCase) &&
                     BuildPipelinePanel != null &&
                     !IsCaptureBusy())
            {
                BuildPipelinePanel.Visibility = Visibility.Visible;
            }

            FamilySourceView.Visibility = _familyUiState == 0 ? Visibility.Visible : Visibility.Collapsed;
            FamilyImageView.Visibility = _familyUiState == 1 ? Visibility.Visible : Visibility.Collapsed;
            FamilyJsonView.Visibility = _familyUiState == 2 ? Visibility.Visible : Visibility.Collapsed;
            FamilyAcquireView.Visibility = _familyUiState == 3 ? Visibility.Visible : Visibility.Collapsed;
            FamilyPreviewView.Visibility = _familyUiState == 4 ? Visibility.Visible : Visibility.Collapsed;
            FamilyCreateView.Visibility = _familyUiState == 5 ? Visibility.Visible : Visibility.Collapsed;
            FamilyDoneView.Visibility = _familyUiState == 6 ? Visibility.Visible : Visibility.Collapsed;
            FamilyImageFooterActions.Visibility = _familyUiState == 1 ? Visibility.Visible : Visibility.Collapsed;
            ScheduleFamilyPreviewBrowserHostHeightUpdate();
            UpdateWebViewVisibility();
            UpdateFamilyJsonReadinessMonitoring();
        }

        private void BuildPipelineScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScheduleFamilyPreviewBrowserHostHeightUpdate();
        }

        private void ScheduleFamilyPreviewBrowserHostHeightUpdate()
        {
            if (_isDisposed || App.IsShuttingDown || Dispatcher.HasShutdownStarted || _familyUiState != 4)
            {
                return;
            }

            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (_isDisposed || App.IsShuttingDown || Dispatcher.HasShutdownStarted)
                    {
                        return;
                    }

                    UpdateFamilyPreviewBrowserHostHeight();
                }),
                DispatcherPriority.Loaded);
        }

        private void UpdateFamilyPreviewBrowserHostHeight()
        {
            if (_isDisposed ||
                App.IsShuttingDown ||
                Dispatcher.HasShutdownStarted ||
                FamilyPreviewBrowserHost == null ||
                BuildPipelineScrollViewer == null ||
                _familyUiState != 4)
            {
                return;
            }

            double scrollHeight = BuildPipelineScrollViewer.ActualHeight;
            if (double.IsNaN(scrollHeight) || double.IsInfinity(scrollHeight) || scrollHeight <= 0)
            {
                return;
            }

            double availableHeight = scrollHeight
                - GetVerticalMargins(BuildPipelineContentPanel)
                - GetActualHeightWithMargins(FamilyPreviewHeader)
                - GetVerticalMargins(FamilyPreviewBrowserHost)
                - FamilyPreviewHostBottomBuffer;

            double targetHeight = FamilyPreviewHostMinHeight;
            if (!double.IsNaN(availableHeight) && !double.IsInfinity(availableHeight) && availableHeight > 0)
            {
                targetHeight = Math.Min(
                    FamilyPreviewHostMaxHeight,
                    Math.Max(FamilyPreviewHostMinHeight, availableHeight));
            }

            if (Math.Abs(FamilyPreviewBrowserHost.Height - targetHeight) > 1)
            {
                FamilyPreviewBrowserHost.Height = targetHeight;
            }
        }

        private static double GetActualHeightWithMargins(FrameworkElement? element)
        {
            if (element == null || element.Visibility == Visibility.Collapsed)
            {
                return 0;
            }

            return element.ActualHeight + GetVerticalMargins(element);
        }

        private static double GetVerticalMargins(FrameworkElement? element)
        {
            if (element == null)
            {
                return 0;
            }

            Thickness margin = element.Margin;
            return margin.Top + margin.Bottom;
        }

        private bool ShouldMonitorFamilyJsonReadiness()
        {
            return !_isDisposed &&
                   !App.IsShuttingDown &&
                   !Dispatcher.HasShutdownStarted &&
                   _familyUiState == 3 &&
                   _activeTarget == AiServiceTarget.Build &&
                   string.Equals(_buildFunctionId, "family", StringComparison.OrdinalIgnoreCase) &&
                   !_familyJsonReadyToAcquire &&
                   !IsCaptureBusy() &&
                   AiWebBrowser?.CoreWebView2 != null &&
                   AiBrowserService.IsExpectedHost(AiWebBrowser.Source, AiServiceTarget.Build);
        }

        private void UpdateFamilyJsonReadinessMonitoring()
        {
            if (ShouldMonitorFamilyJsonReadiness())
            {
                if (!_familyJsonReadinessTimer.IsEnabled)
                {
                    _familyJsonReadinessTimer.Start();
                }
            }
            else
            {
                _familyJsonReadinessTimer.Stop();
            }

            UpdateFamilyAcquireReadinessVisual();
        }

        private async void FamilyJsonReadinessTimer_Tick(object? sender, EventArgs e)
        {
            if (_isCheckingFamilyJsonReadiness || !ShouldMonitorFamilyJsonReadiness()) return;

            _isCheckingFamilyJsonReadiness = true;
            try
            {
                string text = await GetVisibleFamilyJsonCandidateTextAsync();
                if (_isDisposed || App.IsShuttingDown) return;

                if (string.IsNullOrWhiteSpace(text))
                {
                    SetFamilyJsonCandidateDetected(false);
                }
                else if (TryPrepareFamilyJson(text, out _, out _, out _))
                {
                    SetFamilyJsonAcquireReady(true, "Complete Family JSON detected. Acquire latest is ready.");
                }
                else
                {
                    SetFamilyJsonCandidateDetected(true, "JSON-like response detected. Waiting for complete valid Family JSON.");
                }
            }
            catch
            {
                // AI Studio changes often; a failed readiness scan should not block manual acquire.
            }
            finally
            {
                _isCheckingFamilyJsonReadiness = false;
                UpdateFamilyJsonReadinessMonitoring();
            }
        }

        private void SetFamilyJsonAcquireReady(bool ready, string? status = null)
        {
            _familyJsonReadyToAcquire = ready;
            if (ready)
            {
                _familyJsonCandidateDetected = true;
            }
            else
            {
                _familyJsonCandidateDetected = false;
            }

            if (!string.IsNullOrWhiteSpace(status) && FamilyStudioFooterStatusText != null && _familyUiState == 3)
            {
                FamilyStudioFooterStatusText.Text = status;
            }

            UpdateFamilyJsonReadinessMonitoring();
        }

        private void SetFamilyJsonCandidateDetected(bool detected, string? status = null)
        {
            if (_familyJsonReadyToAcquire) return;

            _familyJsonCandidateDetected = detected;
            if (!string.IsNullOrWhiteSpace(status) && FamilyStudioFooterStatusText != null && _familyUiState == 3)
            {
                FamilyStudioFooterStatusText.Text = status;
            }

            UpdateFamilyAcquireReadinessVisual();
        }

        private void UpdateFamilyAcquireReadinessVisual()
        {
            bool ready = _familyJsonReadyToAcquire;
            bool candidate = _familyJsonCandidateDetected;
            Brush readyBrush = BrushFromHex("#4CAF82");
            Brush candidateBrush = BrushFromHex("#C89A3C");
            Brush neutralBackground = (Brush)FindResource("Brush.Background.Primary");
            Brush neutralBorder = (Brush)FindResource("Brush.Border.Tertiary");
            Brush neutralText = (Brush)FindResource("Brush.Text.Primary");
            Brush mutedText = (Brush)FindResource("Brush.Text.Tertiary");

            if (FamilyAcquireReadinessDot != null)
            {
                FamilyAcquireReadinessDot.Fill = ready ? readyBrush : candidate ? candidateBrush : neutralBorder;
            }

            if (FamilyAcquireReadinessText != null)
            {
                FamilyAcquireReadinessText.Text = ready
                    ? "Complete Family JSON detected in AI Studio"
                    : candidate
                        ? "JSON is streaming; waiting for complete Family JSON"
                    : "Waiting for complete Family JSON in AI Studio";
                FamilyAcquireReadinessText.Foreground = ready ? readyBrush : candidate ? candidateBrush : mutedText;
            }

            ApplyFamilyAcquireButtonVisual(FamilyImportLatestJsonButton, ready, candidate, neutralBackground, neutralBorder, neutralText, readyBrush, candidateBrush);
            ApplyFamilyAcquireButtonVisual(FamilyStudioImportLatestJsonButton, ready, candidate, neutralBackground, neutralBorder, neutralText, readyBrush, candidateBrush);
        }

        private static void ApplyFamilyAcquireButtonVisual(Button? button, bool ready, bool candidate, Brush neutralBackground, Brush neutralBorder, Brush neutralText, Brush readyBrush, Brush candidateBrush)
        {
            if (button == null) return;

            button.Background = ready ? readyBrush : neutralBackground;
            button.BorderBrush = ready ? readyBrush : candidate ? candidateBrush : neutralBorder;
            button.Foreground = ready ? BrushFromHex("#0D1F14") : neutralText;
            button.ToolTip = ready
                ? "Save the latest valid Family JSON response from AI Studio"
                : candidate
                    ? "AI Studio is producing JSON. The button turns green when the complete Family JSON validates."
                : "AI Studio is still waiting for a complete valid Family JSON response. You can still scan manually.";
        }

        private async Task InstallFamilyJsonReadinessProbeAsync()
        {
            if (_isDisposed || App.IsShuttingDown || AiWebBrowser?.CoreWebView2 == null) return;
            if (!AiBrowserService.IsExpectedHost(AiWebBrowser.Source, AiServiceTarget.Build)) return;

            try
            {
                await AiWebBrowser.CoreWebView2.ExecuteScriptAsync(FamilyJsonReadinessProbeScript);
            }
            catch
            {
                // Polling remains as a fallback when script injection is unavailable.
            }
        }

        private void UpdateFamilyFooterStepper()
        {
            int phase = _familyUiState switch
            {
                3 => 2,
                4 => 3,
                5 => 4,
                6 => 5,
                _ => 1
            };

            bool hasJsonFile = !string.IsNullOrWhiteSpace(_familyJsonPath) && File.Exists(_familyJsonPath);
            bool canSaveJson = !string.IsNullOrWhiteSpace(_familyJsonPath) && File.Exists(_familyJsonPath);

            ApplyFamilyFooterPhase(FamilyFooterAcquireSourceDot, FamilyFooterAcquireSourceMark, FamilyFooterAcquireSourceLabel, FamilyFooterAcquireSourceButton, phase, 1, "1", "Acquire Src", true);
            ApplyFamilyFooterPhase(FamilyFooterAcquireJsonDot, FamilyFooterAcquireJsonMark, FamilyFooterAcquireJsonLabel, FamilyFooterAcquireJsonButton, phase, 2, "2", "Acquire JSON", true);
            ApplyFamilyFooterPhase(FamilyFooterPreviewDot, FamilyFooterPreviewMark, FamilyFooterPreviewLabel, FamilyFooterPreviewButton, phase, 3, "3", "Preview", hasJsonFile || _familyJsonPreviewValid);
            ApplyFamilyFooterPhase(FamilyFooterCreateDot, FamilyFooterCreateMark, FamilyFooterCreateLabel, FamilyFooterCreateButton, phase, 4, "4", "Create RFA", _familyJsonPreviewValid);
            ApplyFamilyFooterPhase(FamilyFooterDownloadDot, FamilyFooterDownloadMark, FamilyFooterDownloadLabel, FamilyFooterDownloadButton, phase, 5, "5", "JSON", canSaveJson);

            ApplyFamilyFooterPhase(FamilyStudioFooterAcquireSourceDot, FamilyStudioFooterAcquireSourceMark, FamilyStudioFooterAcquireSourceLabel, FamilyStudioFooterAcquireSourceButton, phase, 1, "1", "Acquire Src", true);
            ApplyFamilyFooterPhase(FamilyStudioFooterAcquireJsonDot, FamilyStudioFooterAcquireJsonMark, FamilyStudioFooterAcquireJsonLabel, FamilyStudioFooterAcquireJsonButton, phase, 2, "2", "Acquire JSON", true);
            ApplyFamilyFooterPhase(FamilyStudioFooterPreviewDot, FamilyStudioFooterPreviewMark, FamilyStudioFooterPreviewLabel, FamilyStudioFooterPreviewButton, phase, 3, "3", "Preview", hasJsonFile || _familyJsonPreviewValid);
            ApplyFamilyFooterPhase(FamilyStudioFooterCreateDot, FamilyStudioFooterCreateMark, FamilyStudioFooterCreateLabel, FamilyStudioFooterCreateButton, phase, 4, "4", "Create RFA", _familyJsonPreviewValid);
            ApplyFamilyFooterPhase(FamilyStudioFooterDownloadDot, FamilyStudioFooterDownloadMark, FamilyStudioFooterDownloadLabel, FamilyStudioFooterDownloadButton, phase, 5, "5", "JSON", canSaveJson);

            Brush inactiveLine = (Brush)FindResource("Brush.Border.Tertiary");
            FamilyFooterLine1.Background = phase > 1 ? SoftBrush(88, "#4CAF82") : inactiveLine;
            FamilyFooterLine2.Background = phase > 2 ? SoftBrush(88, "#4CAF82") : inactiveLine;
            FamilyFooterLine3.Background = phase > 3 ? SoftBrush(88, "#4CAF82") : inactiveLine;
            FamilyFooterLine4.Background = phase > 4 ? SoftBrush(88, "#4CAF82") : inactiveLine;
            FamilyStudioFooterLine1.Background = phase > 1 ? SoftBrush(88, "#4CAF82") : inactiveLine;
            FamilyStudioFooterLine2.Background = phase > 2 ? SoftBrush(88, "#4CAF82") : inactiveLine;
            FamilyStudioFooterLine3.Background = phase > 3 ? SoftBrush(88, "#4CAF82") : inactiveLine;
            FamilyStudioFooterLine4.Background = phase > 4 ? SoftBrush(88, "#4CAF82") : inactiveLine;
            UpdateFamilyStudioModelBadgeFromUrl();
        }

        private void ApplyFamilyFooterPhase(Border dot, TextBlock mark, TextBlock label, Button button, int currentPhase, int phase, string indexText, string labelText, bool canNavigate)
        {
            bool done = currentPhase > phase;
            bool current = currentPhase == phase;
            bool downloadReady = phase == 5 && _familyUiState == 6;
            Brush ready = BrushFromHex("#4CAF82");
            Brush muted = (Brush)FindResource("Brush.Text.Tertiary");
            Brush dormant = (Brush)FindResource("Brush.Border.Tertiary");
            Brush inactiveFill = (Brush)FindResource("Brush.Background.Primary");
            Brush inactiveBorder = (Brush)FindResource(canNavigate ? "Brush.Border.Tertiary" : "Brush.Border.Secondary");

            dot.Background = done || downloadReady
                ? SoftBrush(34, "#4CAF82")
                : current
                    ? SoftBrush(24, "#4CAF82")
                    : inactiveFill;
            dot.BorderBrush = done || current || downloadReady
                ? ready
                : inactiveBorder;
            mark.Foreground = done || current || downloadReady ? ready : muted;
            mark.Text = done ? "OK" : indexText;
            label.Foreground = done || current || downloadReady
                ? (downloadReady ? ready : (Brush)FindResource("Brush.Text.Secondary"))
                : canNavigate ? muted : dormant;
            label.Text = labelText;
            button.Opacity = 1.0;
        }

        private void UpdateFamilyStudioModelBadgeFromUrl()
        {
            if (FamilyStudioModelBadge == null) return;

            bool flashActive = IsRecommendedAiStudioModelUrl(AiWebBrowser?.Source);
            string accent = flashActive ? "#4CAF82" : "#D95558";
            Brush accentBrush = BrushFromHex(accent);

            FamilyStudioModelBadge.Background = SoftBrush(18, accent);
            FamilyStudioModelBadge.BorderBrush = SoftBrush(96, accent);
            FamilyStudioModelBadgeDot.Background = SoftBrush(28, accent);
            FamilyStudioModelBadgeDot.BorderBrush = accentBrush;
            FamilyStudioModelBadgeMark.Foreground = accentBrush;
            FamilyStudioModelBadgeMark.Text = flashActive ? "OK" : "ER";
            FamilyStudioModelBadgeText.Foreground = accentBrush;
            FamilyStudioModelBadgeText.Text = flashActive
                ? "Gemini 3.5 Flash"
                : "Gemini 3.5 Flash (Recommended AI model)";
            FamilyStudioModelActionButton.Content = flashActive ? "Flash Active" : "Use Flash";
            FamilyStudioModelActionButton.Background = flashActive ? SoftBrush(34, "#4CAF82") : SoftBrush(34, "#D95558");
            FamilyStudioModelActionButton.BorderBrush = flashActive ? SoftBrush(96, "#4CAF82") : SoftBrush(96, "#D95558");
            FamilyStudioModelActionButton.Foreground = accentBrush;
            FamilyStudioModelActionButton.ToolTip = flashActive
                ? "AI Studio URL includes the Gemini 3.5 Flash model parameter"
                : "Reopen AI Studio with the Gemini 3.5 Flash model parameter";
        }

        private void UpdateFamilyPreviewBadgeVisual()
        {
            if (FamilyPreviewBadge == null) return;

            bool hasJsonFile = !string.IsNullOrWhiteSpace(_familyJsonPath) && File.Exists(_familyJsonPath);
            string text = _familyJsonPreviewValid ? "Active" : hasJsonFile ? "Ready" : "Waiting";
            string accent = _familyJsonPreviewValid ? "#4CAF82" : hasJsonFile ? "#C89A3C" : "#7A848D";

            FamilyPreviewBadge.Background = SoftBrush(22, accent);
            FamilyPreviewBadge.BorderBrush = SoftBrush(88, accent);
            FamilyPreviewBadgeText.Foreground = BrushFromHex(accent);
            FamilyPreviewBadgeText.Text = text;
        }

        public void NotifyRevitContextChanged()
        {
            if (_isDisposed || App.IsShuttingDown || Dispatcher.HasShutdownStarted) return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(NotifyRevitContextChanged), DispatcherPriority.Background);
                return;
            }

            UpdateControlAvailability();
            InvalidateMeasure();
            InvalidateArrange();
            InvalidateVisual();
            AiWebBrowser?.InvalidateVisual();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_isDisposed || App.IsShuttingDown || Dispatcher.HasShutdownStarted) return;

                try
                {
                    UpdateLayout();
                    if (!_isBrowserInitialized && !_isInitializing)
                    {
                        InitializeWebBrowser();
                    }
                    else if (_isBrowserInitialized && CanRefreshBrowserStateAfterContextSwitch())
                    {
                        _ = RefreshBrowserStateAsync();
                    }
                }
                catch
                {
                    // Ignore layout refresh races while Revit is rehosting dockable panes.
                }
            }), DispatcherPriority.Render);
        }

        private bool CanRefreshBrowserStateAfterContextSwitch()
        {
            return _paneState == PaneState.BrowserLoading ||
                   _paneState == PaneState.LoginRequired ||
                   _paneState == PaneState.Ready ||
                   _paneState == PaneState.Sent ||
                   _paneState == PaneState.UnsupportedPage;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _sentStateTimer.Stop();
            _familyJsonReadinessTimer.Stop();
            Loaded -= AiDockablePane_Loaded;
            Unloaded -= AiDockablePane_Unloaded;

            if (Instance == this)
            {
                Instance = null;
            }

            try
            {
                App.Instance?.DetachPane(this);
            }
            catch
            {
                // Ignore lifecycle cleanup errors
            }

            try
            {
                DisposePrimaryBrowserInstance(AiWebBrowser);
                DisposeFamilyPreviewBrowserInstance();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        public void PrepareForRevitShutdown()
        {
            if (!Dispatcher.CheckAccess())
            {
                try
                {
                    Dispatcher.Invoke(new Action(PrepareForRevitShutdown), DispatcherPriority.Send);
                }
                catch
                {
                }
                return;
            }

            try
            {
                if (_isDisposed) return;
                _isDisposed = true;
                _sentStateTimer.Stop();
                _familyJsonReadinessTimer.Stop();
                Loaded -= AiDockablePane_Loaded;
                Unloaded -= AiDockablePane_Unloaded;

                if (Instance == this)
                {
                    Instance = null;
                }

                DisposePrimaryBrowserInstance(AiWebBrowser);
                DisposeFamilyPreviewBrowserInstance();
            }
            catch
            {
                // Revit is shutting down; never let browser host cleanup escape.
            }
        }

        public void ResetBrowserForRevitHostTransition()
        {
            if (_isDisposed || App.IsShuttingDown || Dispatcher.HasShutdownStarted) return;

            if (!Dispatcher.CheckAccess())
            {
                try
                {
                    Dispatcher.Invoke(new Action(ResetBrowserForRevitHostTransition), DispatcherPriority.Send);
                }
                catch
                {
                }
                return;
            }

            try
            {
                DisposeBrowserControl(recreate: true);
            }
            catch
            {
                // Browser recreation is defensive; Revit can still recreate the pane later.
            }
        }

        private void DisposeBrowserControl(bool recreate)
        {
            _isBrowserInitialized = false;
            _isInitializing = false;

            var oldBrowser = AiWebBrowser;
            if (oldBrowser == null) return;

            Panel? parent = oldBrowser.Parent as Panel;
            int childIndex = parent?.Children.IndexOf(oldBrowser) ?? -1;
            int row = Grid.GetRow(oldBrowser);
            int column = Grid.GetColumn(oldBrowser);
            int rowSpan = Grid.GetRowSpan(oldBrowser);
            int columnSpan = Grid.GetColumnSpan(oldBrowser);
            HorizontalAlignment horizontalAlignment = oldBrowser.HorizontalAlignment;
            VerticalAlignment verticalAlignment = oldBrowser.VerticalAlignment;
            Visibility visibility = oldBrowser.Visibility;

            try
            {
                oldBrowser.NavigationCompleted -= AiWebBrowser_NavigationCompleted;
                oldBrowser.WebMessageReceived -= AiWebBrowser_WebMessageReceived;
                if (oldBrowser.CoreWebView2 != null)
                {
                    oldBrowser.CoreWebView2.DownloadStarting -= AiWebBrowser_DownloadStarting;
                }
            }
            catch
            {
            }

            if (parent != null)
            {
                parent.Children.Remove(oldBrowser);
            }

            try
            {
                oldBrowser.Dispose();
            }
            catch
            {
            }

            if (!recreate || parent == null || App.IsShuttingDown)
            {
                return;
            }

            var replacement = new Microsoft.Web.WebView2.Wpf.WebView2
            {
                HorizontalAlignment = horizontalAlignment,
                VerticalAlignment = verticalAlignment,
                Visibility = visibility
            };

            Grid.SetRow(replacement, row);
            Grid.SetColumn(replacement, column);
            Grid.SetRowSpan(replacement, rowSpan);
            Grid.SetColumnSpan(replacement, columnSpan);

            AiWebBrowser = replacement;
            if (childIndex >= 0 && childIndex <= parent.Children.Count)
            {
                parent.Children.Insert(childIndex, replacement);
            }
            else
            {
                parent.Children.Add(replacement);
            }
        }

        private void AiDockablePane_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isDisposed) return;

            InitializeWebBrowser();
            NotifyRevitContextChanged();
        }

        private void DisposePrimaryBrowserInstance(Microsoft.Web.WebView2.Wpf.WebView2? browser)
        {
            if (browser == null) return;

            DetachPrimaryBrowserEvents(browser);

            try
            {
                browser.Dispose();
            }
            catch
            {
            }
        }

        private void DetachPrimaryBrowserEvents(Microsoft.Web.WebView2.Wpf.WebView2? browser)
        {
            if (browser == null) return;

            try
            {
                browser.NavigationCompleted -= AiWebBrowser_NavigationCompleted;
                browser.WebMessageReceived -= AiWebBrowser_WebMessageReceived;
            }
            catch
            {
            }

            try
            {
                if (browser.CoreWebView2 != null)
                {
                    browser.CoreWebView2.DownloadStarting -= AiWebBrowser_DownloadStarting;
                }
            }
            catch
            {
            }
        }

        private void DisposeFamilyPreviewBrowserInstance()
        {
            DetachFamilyPreviewBrowserEvents();
            _familyPreviewWebViewService.Reset();
            _isFamilyPreviewBrowserInitialized = false;
            _isFamilyPreviewBrowserInitializing = false;

            try
            {
                FamilyPreviewBrowser?.Dispose();
            }
            catch
            {
            }
        }

        private void DetachFamilyPreviewBrowserEvents()
        {
            try
            {
                if (FamilyPreviewBrowser == null) return;
                FamilyPreviewBrowser.WebMessageReceived -= FamilyPreviewBrowser_WebMessageReceived;
                _isFamilyPreviewBrowserInitialized = false;
            }
            catch
            {
            }
        }

        private async Task InitializeFamilyPreviewBrowserAsync()
        {
            if (_isDisposed || App.IsShuttingDown || Dispatcher.HasShutdownStarted || FamilyPreviewBrowser == null) return;
            if (_isFamilyPreviewBrowserInitialized) return;
            if (_isFamilyPreviewBrowserInitializing)
            {
                while (_isFamilyPreviewBrowserInitializing && !_isDisposed && !App.IsShuttingDown && !Dispatcher.HasShutdownStarted)
                {
                    await Task.Delay(50);
                }

                return;
            }

            _isFamilyPreviewBrowserInitializing = true;

            try
            {
                await WebView2Bootstrapper.InitializeAsync(FamilyPreviewBrowser);
                if (_isDisposed || App.IsShuttingDown || Dispatcher.HasShutdownStarted || FamilyPreviewBrowser.CoreWebView2 == null) return;

                FamilyPreviewBrowser.WebMessageReceived -= FamilyPreviewBrowser_WebMessageReceived;
                FamilyPreviewBrowser.WebMessageReceived += FamilyPreviewBrowser_WebMessageReceived;
                await _familyPreviewWebViewService.InitializeAsync(FamilyPreviewBrowser);
                if (_isDisposed || App.IsShuttingDown || Dispatcher.HasShutdownStarted) return;

                await _familyPreviewWebViewService.ApplyThemeAsync(FamilyPreviewBrowser, SettingsManager.Settings.IsDarkTheme);
                if (_isDisposed || App.IsShuttingDown || Dispatcher.HasShutdownStarted) return;
                _isFamilyPreviewBrowserInitialized = true;
            }
            catch
            {
                try
                {
                    if (!_isDisposed && !App.IsShuttingDown && !Dispatcher.HasShutdownStarted)
                    {
                        FamilyPreviewVisualText.Text = "The local 3D previewer could not start. Text fallback remains available.";
                    }
                }
                catch
                {
                }
            }
            finally
            {
                _isFamilyPreviewBrowserInitializing = false;
            }
        }

        private void AiDockablePane_Unloaded(object sender, RoutedEventArgs e)
        {
            if (App.IsShuttingDown)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (App.IsShuttingDown || _isDisposed || IsLoaded) return;
                    if (App.Instance?.MainPane != this)
                    {
                        Dispose();
                    }
                }
                catch
                {
                    // Revit may unload/rehost panes during document activation.
                }
            }), DispatcherPriority.ApplicationIdle);
        }

        private bool _isInitializing;

        private async void InitializeWebBrowser()
        {
            if (_isDisposed) return;
            if (_isBrowserInitialized || _isInitializing) return;
            _isInitializing = true;

            try
            {
                await WebView2Bootstrapper.InitializeAsync(AiWebBrowser);
                if (_isDisposed) return;

                AiWebBrowser.NavigationCompleted += AiWebBrowser_NavigationCompleted;
                AiWebBrowser.WebMessageReceived += AiWebBrowser_WebMessageReceived;
                AiWebBrowser.CoreWebView2.DownloadStarting += AiWebBrowser_DownloadStarting;
                await _webService.ApplyPreferredColorSchemeAsync(AiWebBrowser, SettingsManager.Settings.IsDarkTheme);
                _webService.NavigateToTarget(AiWebBrowser, _activeTarget);
                _isBrowserInitialized = true;
                SetPaneState(PaneState.BrowserLoading, GetTargetLoadingLabel(_activeTarget));
            }
            catch
            {
                if (!_isDisposed)
                {
                    SetPaneState(PaneState.UnsupportedPage, "Browser failed to start");
                }
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private async void AiWebBrowser_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_isDisposed || AiWebBrowser == null || AiWebBrowser.CoreWebView2 == null) return;
            try
            {
                if (e.IsSuccess)
                {
                    await _webService.ApplyPreferredColorSchemeAsync(AiWebBrowser, SettingsManager.Settings.IsDarkTheme);
                    await RefreshBrowserStateAsync();
                    UpdateFamilyStudioModelBadgeFromUrl();
                    if (_familyUiState == 3)
                    {
                        SetFamilyJsonAcquireReady(false);
                        await InstallFamilyJsonReadinessProbeAsync();
                    }

                    if (_pendingFamilyReferenceSendToStudio)
                    {
                        await SendFamilyReferenceToStudioAsync(false);
                    }
                }
                else
                {
                    SetPaneState(PaneState.UnsupportedPage, $"{GetTargetDisplayName(_activeTarget)} failed to load");
                }
            }
            catch
            {
                // Ignore navigation/script injection errors gracefully
            }
        }

        private async Task RefreshBrowserStateAsync()
        {
            if (_isDisposed || App.IsShuttingDown || AiWebBrowser?.CoreWebView2 == null) return;

            if (!AiBrowserService.IsExpectedHost(AiWebBrowser.Source, _activeTarget))
            {
                SetPaneState(PaneState.UnsupportedPage);
                return;
            }

            if (_activeTarget == AiServiceTarget.Renders)
            {
                string loginStatus = await _webService.CheckLoginStatusAsync(AiWebBrowser);
                if (_isDisposed || App.IsShuttingDown) return;
                SetPaneState(loginStatus == "NOT_LOGGED_IN" ? PaneState.LoginRequired : PaneState.Ready);
                return;
            }

            SetPaneState(PaneState.Ready, GetTargetReadyLabel(_activeTarget));
        }

        private void AiWebBrowser_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (_isDisposed) return;
            try
            {
                var message = JObject.Parse(e.WebMessageAsJson);
                string source = message.Value<string>("source") ?? string.Empty;
                if (string.Equals(source, "musait-family-json-readiness", StringComparison.Ordinal))
                {
                    HandleFamilyJsonReadinessMessage(message, e.Source);
                    return;
                }
            }
            catch
            {
            }

            try
            {
                string theme = e.TryGetWebMessageAsString();
                if (theme == "DARK" || theme == "LIGHT")
                {
                    return;
                }
            }
            catch { }
        }

        private void HandleFamilyJsonReadinessMessage(JObject message, string messageSource)
        {
            if (_familyUiState != 3 || _activeTarget != AiServiceTarget.Build) return;
            if (!Uri.TryCreate(messageSource, UriKind.Absolute, out Uri? sourceUri) ||
                !AiBrowserService.IsExpectedHost(sourceUri, AiServiceTarget.Build))
            {
                return;
            }

            string text = message.Value<string>("text") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                SetFamilyJsonCandidateDetected(false);
                return;
            }

            if (TryPrepareFamilyJson(text, out _, out _, out _))
            {
                SetFamilyJsonAcquireReady(true, "Complete Family JSON detected. Acquire latest is ready.");
            }
            else
            {
                SetFamilyJsonCandidateDetected(true, "JSON-like response detected. Waiting for complete valid Family JSON.");
            }
        }

        private void FamilyPreviewBrowser_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (_isDisposed || !FamilyPreviewWebViewService.IsTrustedSource(e.Source)) return;

            try
            {
                var message = JObject.Parse(e.WebMessageAsJson);
                string action = message.Value<string>("action") ?? string.Empty;
                string type = message.Value<string>("type") ?? string.Empty;
                if (string.Equals(action, "create-rfa", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(type, "create-rfa", StringComparison.OrdinalIgnoreCase))
                {
                    HandleFamilyPreviewCreateRequest(message);
                    return;
                }

                if (!string.Equals(action, "confirmPreview", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                string jsonPath = message.Value<string>("jsonPath") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(_familyJsonPath) &&
                    string.Equals(Path.GetFullPath(jsonPath), Path.GetFullPath(_familyJsonPath), StringComparison.OrdinalIgnoreCase))
                {
                    _familyJsonPreviewValid = true;
                    SetPaneState(PaneState.Ready, "Family preview confirmed");
                    UpdateFamilyFooterStepper();
                }
            }
            catch
            {
                SetPaneState(PaneState.CaptureFailed, "Preview confirmation could not be read");
            }
        }

        private void HandleFamilyPreviewCreateRequest(JObject message)
        {
            string payload = message.Value<string>("payload") ?? message["payload"]?.ToString(Formatting.None) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(payload))
            {
                SetPaneState(PaneState.CaptureFailed, "Preview did not include Family JSON");
                return;
            }

            if (!TryPrepareFamilyJson(payload, out string normalizedJson, out FamilyDefinition definition, out string feedback))
            {
                ShowFamilyJsonImportFailure("Preview JSON needs fixes", feedback);
                return;
            }

            _familyJsonPath = SaveFamilyJsonFile(normalizedJson);
            _currentFamilyBuildPlan = FamilyBuildPlanFactory.Create(definition);
            _familyJsonPreviewValid = true;
            FamilyPipelinePreviewText.Text = BuildFamilyPreviewSummary(definition);
            FamilyPreviewVisualText.Text = BuildFamilyPreviewHero(definition);
            FamilyPreviewComponentsText.Text = BuildFamilyComponentPreview(definition);
            FamilyPreviewParametersText.Text = BuildFamilyParameterPreview(definition);
            UpdateFamilyFooterStepper();
            SetPaneState(PaneState.Ready, "Preview edits accepted");

            FamilyCreateRfaButton_Click(sender: FamilyFooterCreateButton, new RoutedEventArgs());
        }

        private void AiWebBrowser_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
        {
            if (_activeTarget != AiServiceTarget.Build ||
                !string.Equals(_buildFunctionId, "family", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(e.ResultFilePath) ||
                !string.Equals(Path.GetExtension(e.ResultFilePath), ".json", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string jsonPath = e.ResultFilePath;
            var operation = e.DownloadOperation;
            operation.StateChanged += (_, _) =>
            {
                if (operation.State != CoreWebView2DownloadState.Completed)
                {
                    return;
                }

                Dispatcher.BeginInvoke(new Action(() => HandleFamilyJsonPath(jsonPath, "AI Studio JSON downloaded")), DispatcherPriority.Background);
            };
        }

        private void HandleFamilyJsonPath(string jsonPath, string status)
        {
            _familyJsonPath = jsonPath;
            _familyJsonPreviewValid = false;
            _familyPipelineStep = 2;
            _familyUiState = 2;
            _activeTarget = AiServiceTarget.Build;
            _buildFunctionId = "family";
            HideComposer();
            HideSettingsPanel();
            HideAboutPanel();
            ShowBuildPipelinePanel("family");
            FamilyPipelineStatusText.Text = status + ". Checking Family JSON...";
            FamilyPipelinePreviewText.Text = string.Empty;
            UpdateFamilyPipelineUi();
            PreviewFamilyJson(status);
        }

        public void HandleCapturedImage(string base64Image)
        {
            HandleCapturedImage(base64Image, string.Empty);
        }

        public async void HandleCapturedImage(string base64Image, string revitContext)
        {
            if (_isDisposed || AiWebBrowser == null || AiWebBrowser.CoreWebView2 == null)
            {
                SetPaneState(PaneState.UnsupportedPage, "Browser not ready");
                return;
            }

            try
            {
                SetPaneState(PaneState.ProcessingCapture);
                string prompt = ComposePrompt(revitContext);
                if (SettingsManager.Settings.AutoSendCaptures)
                {
                    await SendCaptureAsync(base64Image, prompt);
                    return;
                }

                ShowComposer(base64Image, revitContext, prompt);
            }
            catch
            {
                SetPaneState(PaneState.SendFailed, "Could not prepare capture");
            }
        }

        private async Task SendCaptureAsync(string base64Image, string prompt)
        {
            if (_isDisposed || AiWebBrowser == null || AiWebBrowser.CoreWebView2 == null)
            {
                SetPaneState(PaneState.UnsupportedPage, "Browser not ready");
                return;
            }

            try
            {
                SetPaneState(PaneState.Sending);
                await _webService.InjectPromptAndImageAsync(AiWebBrowser, base64Image, prompt, _activeTarget);
                SetPaneState(PaneState.Sent, string.IsNullOrWhiteSpace(prompt) ? "Capture sent" : $"Prompt + capture sent to {GetTargetDisplayName(_activeTarget)}");
                HideComposer();
                if (_activeTarget == AiServiceTarget.Build &&
                    string.Equals(_buildFunctionId, "family", StringComparison.OrdinalIgnoreCase))
                {
                    EnterFamilyStudioAcquireState("Answer AI Studio questions until the complete Family JSON appears.");
                }
            }
            catch
            {
                SetPaneState(PaneState.SendFailed, $"Send failed. Check {GetTargetDisplayName(_activeTarget)} and try again.");
            }
        }

        private void ShowComposer(string base64Image, string revitContext, string prompt)
        {
            HideBuildPipelinePanel();
            _pendingBase64Image = base64Image;
            _pendingRevitContext = revitContext ?? string.Empty;
            if (_activeTarget == AiServiceTarget.Build &&
                string.Equals(_buildFunctionId, "family", StringComparison.OrdinalIgnoreCase))
            {
                _familyReferenceBase64Image = base64Image;
                if (string.IsNullOrWhiteSpace(_familyReferencePath))
                {
                    _familyReferencePath = "Captured reference";
                }
            }

            _isContextIncludedInComposer = SettingsManager.Settings.IncludeRevitContext && !string.IsNullOrWhiteSpace(_pendingRevitContext);
            _composerPresetId = _activeTarget switch
            {
                AiServiceTarget.Build => PromptPresetCatalog.GetById(SettingsManager.Settings.BuildFunctionId).Mode == PromptMode.Build
                    ? SettingsManager.Settings.BuildFunctionId
                    : PromptPresetCatalog.GetDefaultForMode(PromptMode.Build).Id,
                _ => DefaultRendersPresetId
            };
            ResetPromptModifiers();
            _selectedStudioPresetId = null;
            _isModifiersExpanded = false;
            ApplyModifierExpansion();

            ComposerImage.Source = CreateBitmapImage(base64Image);
            SettingsPanel.Visibility = Visibility.Collapsed;
            ComposerPanel.Visibility = Visibility.Visible;
            PromptMode initialMode = _activeTarget switch
            {
                AiServiceTarget.Build => PromptMode.Build,
                _ => ResolveInitialComposerMode(PromptPresetCatalog.GetById(DefaultRendersPresetId))
            };
            SetComposerMode(initialMode, true);
            SetPaneState(PaneState.Composing);
        }

        private void HideComposer()
        {
            _pendingBase64Image = null;
            _pendingRevitContext = string.Empty;
            _selectedStudioPresetId = null;
            ComposerImage.Source = null;
            CollapsePromptPreview();
            ComposerPanel.Visibility = Visibility.Collapsed;
            UpdateWebViewVisibility();
        }

        private void ShowSettingsPanel()
        {
            LoadSettingsControls();
            HideComposer();
            HideBuildPipelinePanel();
            HideAboutPanel();
            SettingsPanel.Visibility = Visibility.Visible;
            UpdateControlAvailability();
        }

        private void HideSettingsPanel()
        {
            bool wasVisible = SettingsPanel.Visibility == Visibility.Visible;
            SettingsPanel.Visibility = Visibility.Collapsed;
            if (wasVisible && _activeTarget == AiServiceTarget.Build && BuildPipelinePanel.Visibility != Visibility.Visible)
            {
                ShowBuildPipelinePanel(_buildFunctionId);
            }
            UpdateControlAvailability();
        }

        private void ShowAboutPanel()
        {
            LoadAboutPanel();
            HideComposer();
            HideBuildPipelinePanel();
            HideSettingsPanel();
            AboutPanel.Visibility = Visibility.Visible;
            UpdateControlAvailability();
        }

        private void HideAboutPanel()
        {
            bool wasVisible = AboutPanel.Visibility == Visibility.Visible;
            AboutPanel.Visibility = Visibility.Collapsed;
            if (wasVisible && _activeTarget == AiServiceTarget.Build && BuildPipelinePanel.Visibility != Visibility.Visible)
            {
                ShowBuildPipelinePanel(_buildFunctionId);
            }
            UpdateControlAvailability();
        }

        private static void OpenExternalUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // Opening the external browser is optional; keep Revit responsive if Windows rejects it.
            }
        }

        private static string BuildContextSummary(string context)
        {
            if (string.IsNullOrWhiteSpace(context))
            {
                return "No Revit context captured for this image.";
            }

            string normalized = context.Replace("\r", string.Empty).Replace("\n", " · ");
            return normalized.Length > 120 ? normalized.Substring(0, 117) + "..." : normalized;
        }

        private static BitmapImage CreateBitmapImage(string base64Image)
        {
            byte[] bytes = Convert.FromBase64String(base64Image);
            var bmpImage = new BitmapImage();
            using (var ms = new MemoryStream(bytes))
            {
                bmpImage.BeginInit();
                bmpImage.CacheOption = BitmapCacheOption.OnLoad;
                bmpImage.StreamSource = ms;
                bmpImage.EndInit();
            }

            bmpImage.Freeze();
            return bmpImage;
        }

        private void CaptureBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isDisposed || App.IsShuttingDown) return;
            if (!CanStartCapture() || IsPanelOpen()) return;
            HideComposer();

            if (App.Instance.TryRaiseCapture(false, out string message))
            {
                SetPaneState(PaneState.Capturing, message);
            }
            else
            {
                SetPaneState(PaneState.CaptureFailed, message);
            }
        }


        private void BuildPipelinePrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDisposed || App.IsShuttingDown) return;

            AttachFamilyReference();
        }

        private void FamilyFromImageButton_Click(object sender, RoutedEventArgs e)
        {
            _familyUiState = 1;
            UpdateFamilyPipelineUi();
        }

        private void FamilyAttachAnyButton_Click(object sender, RoutedEventArgs e)
        {
            AttachFamilyReference();
        }

        private void FamilyFromJsonButton_Click(object sender, RoutedEventArgs e)
        {
            _familyUiState = 2;
            UpdateFamilyPipelineUi();
        }

        private void FamilyBackToSourceButton_Click(object sender, RoutedEventArgs e)
        {
            _familyUiState = 0;
            _familyPipelineStep = 1;
            _familyJsonPreviewValid = false;
            UpdateFamilyPipelineUi();
        }

        private void FamilyBackToJsonButton_Click(object sender, RoutedEventArgs e)
        {
            _familyUiState = string.IsNullOrWhiteSpace(_familyJsonPath) ? 0 : 2;
            _familyPipelineStep = string.IsNullOrWhiteSpace(_familyJsonPath) ? 1 : 2;
            UpdateFamilyPipelineUi();
        }

        private void FamilyBackToImageButton_Click(object sender, RoutedEventArgs e)
        {
            _familyUiState = 1;
            UpdateFamilyPipelineUi();
        }

        private async void FamilySendToStudioButton_Click(object sender, RoutedEventArgs e)
        {
            await SendFamilyReferenceToStudioAsync(true);
        }

        private async Task SendFamilyReferenceToStudioAsync(bool allowNavigate)
        {
            if (string.IsNullOrWhiteSpace(_familyReferenceBase64Image))
            {
                AttachFamilyReference();
                return;
            }

            if (AiWebBrowser?.CoreWebView2 == null)
            {
                SetPaneState(PaneState.UnsupportedPage, "AI Studio is not ready");
                return;
            }

            if (allowNavigate && RequiresTargetNavigation(AiServiceTarget.Build))
            {
                _pendingFamilyReferenceSendToStudio = true;
                EnsureRecommendedAiStudioTarget();
                SetPaneState(PaneState.BrowserLoading, "Loading AI Studio");
                return;
            }

            _pendingFamilyReferenceSendToStudio = false;
            try
            {
                SetPaneState(PaneState.Sending);
                string prompt = string.IsNullOrWhiteSpace(FamilyImagePromptTextBox.Text)
                    ? PromptPresetCatalog.BuildPrompt("family", string.Empty, false)
                    : FamilyImagePromptTextBox.Text;
                await _webService.InjectPromptAndImageAsync(AiWebBrowser, _familyReferenceBase64Image!, prompt, AiServiceTarget.Build);
                EnterFamilyStudioAcquireState("Answer AI Studio questions until the complete Family JSON appears.");
                SetPaneState(PaneState.Sent, "Family reference sent to AI Studio");
            }
            catch
            {
                _familyUiState = 1;
                ShowBuildPipelinePanel("family");
                SetPaneState(PaneState.SendFailed, "Send failed. Check AI Studio and try again.");
            }
        }

        private void EnterFamilyStudioAcquireState(string status)
        {
            _activeTarget = AiServiceTarget.Build;
            _buildFunctionId = "family";
            _familyUiState = 3;
            _familyPipelineStep = 2;
            _familyJsonReadyToAcquire = false;
            _familyJsonCandidateDetected = false;
            if (FamilyStudioFooterStatusText != null)
            {
                FamilyStudioFooterStatusText.Text = status;
            }

            UpdateServiceTargetVisuals();
            UpdateFamilyPipelineUi();
            _ = InstallFamilyJsonReadinessProbeAsync();
        }

        private void FamilyUseInlineJsonButton_Click(object sender, RoutedEventArgs e)
        {
            string text = FamilyJsonTextBox.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowFamilyJsonImportFailure("Family JSON import failed", "Paste a Family JSON object first.");
                return;
            }

            EnsureRecommendedAiStudioTarget();
            SaveFamilyJsonText(text, "Inline JSON imported");
        }

        private void FamilyFooterAcquireSourceButton_Click(object sender, RoutedEventArgs e)
        {
            _familyUiState = 0;
            _familyPipelineStep = 1;
            UpdateFamilyPipelineUi();
        }

        private void FamilyFooterAcquireJsonButton_Click(object sender, RoutedEventArgs e)
        {
            EnsureRecommendedAiStudioTarget();
            EnterFamilyStudioAcquireState("Answer AI Studio questions until the complete Family JSON appears.");
            SetPaneState(PaneState.Ready, "AI Studio ready for Family JSON");
        }

        private void FamilyFooterPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_familyJsonPreviewValid)
            {
                _familyUiState = 4;
                UpdateFamilyPipelineUi();
                return;
            }

            if (!string.IsNullOrWhiteSpace(_familyJsonPath) && File.Exists(_familyJsonPath))
            {
                PreviewFamilyJson();
                return;
            }

            if (!_familyJsonPreviewValid)
            {
                SetPaneState(PaneState.Ready, "Acquire or attach Family JSON first");
                return;
            }
        }

        private void FamilyFooterCreateButton_Click(object sender, RoutedEventArgs e)
        {
            FamilyCreateRfaButton_Click(sender, e);
        }

        private void FamilyFooterDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentFamilyJsonCopy();
        }

        private async void FamilyImportLatestJsonButton_Click(object sender, RoutedEventArgs e)
        {
            await ImportLatestFamilyJsonFromBrowserAsync();
        }

        private void FamilyPasteJsonButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    SetPaneState(PaneState.CaptureFailed, "Clipboard has no JSON text");
                    return;
                }

                SaveFamilyJsonText(text, "Clipboard JSON imported");
            }
            catch
            {
                SetPaneState(PaneState.CaptureFailed, "Could not read clipboard");
            }
        }

        private void FamilyAttachJsonButton_Click(object sender, RoutedEventArgs e)
        {
            AttachFamilyJsonFile();
        }

        private void FamilyBackToStudioButton_Click(object sender, RoutedEventArgs e)
        {
            _activeTarget = AiServiceTarget.Build;
            EnsureRecommendedAiStudioTarget();

            HideBuildPipelinePanel();
            SetPaneState(PaneState.Ready, "AI Studio conversation visible");
        }

        private void FamilyReopenStudioModelButton_Click(object sender, RoutedEventArgs e)
        {
            _activeTarget = AiServiceTarget.Build;
            try
            {
                if (IsRecommendedAiStudioModelUrl(AiWebBrowser?.Source))
                {
                    SetPaneState(PaneState.Ready, "Gemini 3.5 Flash already active");
                    UpdateFamilyStudioModelBadgeFromUrl();
                    return;
                }

                if (AiWebBrowser != null)
                {
                    _webService.NavigateToTarget(AiWebBrowser, AiServiceTarget.Build);
                }

                if (FamilyStudioFooterStatusText != null)
                {
                    FamilyStudioFooterStatusText.Text = "Gemini 3.5 Flash is targeted. Continue the Family JSON prompt in AI Studio.";
                }

                SetPaneState(PaneState.Ready, "AI Studio reopened with Gemini 3.5 Flash");
            }
            catch
            {
                SetPaneState(PaneState.UnsupportedPage, "AI Studio could not be reopened");
            }
        }

        private void FamilyCreateRfaButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_familyJsonPreviewValid || _currentFamilyBuildPlan == null)
            {
                SetPaneState(PaneState.Ready, "Preview valid JSON first");
                return;
            }

            _familyPipelineStep = 4;
            _familyUiState = 5;
            UpdateFamilyPipelineUi();
            FamilyCreateStatusText.Text = "Free previews Family JSON locally. Pro converts previews into RFA files.";
            SetPaneState(PaneState.Ready, "Previewed Family JSON is ready for Pro RFA conversion");
        }

        private void FamilyGetProButton_Click(object sender, RoutedEventArgs e)
        {
            OpenExternalUrl(Constants.PatreonUrl);
        }

        private void FamilyCopyJsonButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadCurrentFamilyJson(out string json, out string message))
            {
                SetPaneState(PaneState.Ready, message);
                return;
            }

            Clipboard.SetText(json);
            SetPaneState(PaneState.Ready, "Family JSON copied");
        }

        private void FamilySaveJsonButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentFamilyJsonCopy();
        }

        private void FamilyBackToPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            _familyUiState = 4;
            UpdateFamilyPipelineUi();
            SetPaneState(PaneState.Ready, "Family preview remains available");
        }

        private bool TryReadCurrentFamilyJson(out string json, out string message)
        {
            json = string.Empty;
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(_familyJsonPath) || !File.Exists(_familyJsonPath))
            {
                message = "Acquire or attach Family JSON first";
                return false;
            }

            try
            {
                json = File.ReadAllText(_familyJsonPath);
                return true;
            }
            catch (Exception ex)
            {
                message = "Could not read Family JSON: " + ex.Message;
                return false;
            }
        }

        private void SaveCurrentFamilyJsonCopy()
        {
            if (!TryReadCurrentFamilyJson(out string json, out string message))
            {
                SetPaneState(PaneState.Ready, message);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Save Family JSON",
                Filter = "JSON (*.json)|*.json",
                FileName = string.IsNullOrWhiteSpace(_familyJsonPath) ? "family.json" : Path.GetFileName(_familyJsonPath),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                OverwritePrompt = true,
                AddExtension = true,
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() != true)
            {
                SetPaneState(PaneState.Ready, "Save cancelled");
                return;
            }

            try
            {
                File.WriteAllText(dialog.FileName, json);
                SetPaneState(PaneState.Ready, "Saved Family JSON: " + dialog.FileName);
            }
            catch (Exception ex)
            {
                SetPaneState(PaneState.CaptureFailed, "Could not save Family JSON: " + ex.Message);
            }
        }

        private void PreviewFamilyJson()
        {
            PreviewFamilyJson("Family JSON");
        }

        private async void PreviewFamilyJson(string statusPrefix)
        {
            if (_isDisposed || App.IsShuttingDown || Dispatcher.HasShutdownStarted) return;

            if (string.IsNullOrWhiteSpace(_familyJsonPath) || !File.Exists(_familyJsonPath))
            {
                ShowFamilyJsonImportFailure("Family JSON file is missing", "Attach a JSON file or acquire the latest AI Studio response.");
                return;
            }

            try
            {
                string sourceText = File.ReadAllText(_familyJsonPath);
                if (!TryPrepareFamilyJson(sourceText, out string normalizedJson, out FamilyDefinition definition, out string feedback))
                {
                    ShowFamilyJsonImportFailure("Family JSON needs fixes", feedback);
                    return;
                }

                if (!IsSameJsonDocument(sourceText, normalizedJson))
                {
                    _familyJsonPath = SaveFamilyJsonFile(normalizedJson);
                }

                _currentFamilyBuildPlan = FamilyBuildPlanFactory.Create(definition);
                _familyJsonPreviewValid = true;
                _familyPipelineStep = 3;
                _familyUiState = 4;
                UpdateFamilyPipelineUi();
                FamilyPipelineStatusText.Text = string.Equals(statusPrefix, "Family JSON", StringComparison.Ordinal)
                    ? "Preview passed. RFA conversion is available in Pro."
                    : statusPrefix + ". Preview passed.";
                FamilyPipelinePreviewText.Text = BuildFamilyPreviewSummary(definition);
                FamilyPreviewVisualText.Text = BuildFamilyPreviewHero(definition);
                FamilyPreviewComponentsText.Text = BuildFamilyComponentPreview(definition);
                FamilyPreviewParametersText.Text = BuildFamilyParameterPreview(definition);
                string payloadJson = FamilyPreviewPayloadFactory.CreateJson(_currentFamilyBuildPlan, _familyJsonPath ?? string.Empty);
                await InitializeFamilyPreviewBrowserAsync();
                if (_isDisposed || App.IsShuttingDown || Dispatcher.HasShutdownStarted || FamilyPreviewBrowser?.CoreWebView2 == null) return;

                await _familyPreviewWebViewService.ApplyThemeAsync(FamilyPreviewBrowser, SettingsManager.Settings.IsDarkTheme);
                if (_isDisposed || App.IsShuttingDown || Dispatcher.HasShutdownStarted || FamilyPreviewBrowser?.CoreWebView2 == null) return;

                await _familyPreviewWebViewService.ShowPreviewAsync(FamilyPreviewBrowser, payloadJson);
                if (_isDisposed || App.IsShuttingDown || Dispatcher.HasShutdownStarted) return;

                SetPaneState(PaneState.Ready, "Family JSON preview ready");
            }
            catch
            {
                if (_isDisposed || App.IsShuttingDown || Dispatcher.HasShutdownStarted) return;
                ShowFamilyJsonImportFailure("Family JSON parse failed", "The file could not be read. Attach a valid JSON file or reacquire the latest AI Studio response.");
            }
        }

        private async Task ImportLatestFamilyJsonFromBrowserAsync()
        {
            if (_isDisposed || App.IsShuttingDown || AiWebBrowser?.CoreWebView2 == null)
            {
                if (!_isDisposed && !App.IsShuttingDown) SetPaneState(PaneState.UnsupportedPage, "AI Studio is not ready");
                return;
            }

            try
            {
                FamilyPipelineStatusText.Text = "Scanning AI Studio for the latest Family JSON...";
                FamilyPipelinePreviewText.Text = string.Empty;

                string text = await GetVisibleFamilyJsonCandidateTextAsync();
                if (_isDisposed || App.IsShuttingDown) return;

                if (string.IsNullOrWhiteSpace(text))
                {
                    ShowFamilyJsonImportFailure("No JSON response found", "Keep the AI Studio answer visible, or copy the model JSON and use Paste JSON.");
                    return;
                }

                SaveFamilyJsonText(text, "Latest AI Studio JSON acquired");
            }
            catch
            {
                if (_isDisposed || App.IsShuttingDown) return;
                ShowFamilyJsonImportFailure("Could not import JSON from AI Studio", "Copy the model JSON response, then use Paste JSON.");
            }
        }

        private async Task<string> GetVisibleFamilyJsonCandidateTextAsync()
        {
            if (AiWebBrowser?.CoreWebView2 == null) return string.Empty;

            string result = await AiWebBrowser.CoreWebView2.ExecuteScriptAsync(FamilyJsonCandidateScanScript);
            var candidates = JsonConvert.DeserializeObject<List<string>>(result) ?? new List<string>();
            return string.Join("\n\n", candidates);
        }

        private void SaveFamilyJsonText(string text, string status)
        {
            if (_isDisposed || App.IsShuttingDown) return;
            if (!TryPrepareFamilyJson(text, out string normalizedJson, out _, out string feedback))
            {
                ShowFamilyJsonImportFailure("Family JSON import failed", feedback);
                return;
            }

            string path = SaveFamilyJsonFile(normalizedJson);
            HandleFamilyJsonPath(path, status);
        }

        private void ShowFamilyJsonImportFailure(string status, string detail)
        {
            if (_isDisposed || App.IsShuttingDown) return;
            _familyJsonPreviewValid = false;
            SetFamilyJsonAcquireReady(false);
            _familyPipelineStep = string.IsNullOrWhiteSpace(_familyJsonPath) ? 1 : 2;
            UpdateFamilyPipelineUi();
            FamilyPipelineStatusText.Text = status;
            FamilyPipelinePreviewText.Text = detail;
            if (FamilyStudioFooterStatusText != null)
            {
                FamilyStudioFooterStatusText.Text = detail;
            }
            SetPaneState(PaneState.CaptureFailed, status);
        }

        private static string SaveFamilyJsonFile(string json)
        {
            Directory.CreateDirectory(GetFamilyJsonFolder());
            string path = Path.Combine(GetFamilyJsonFolder(), $"family_{DateTime.Now:yyyyMMdd_HHmmss_fff}.json");
            File.WriteAllText(path, json);
            return path;
        }

        private static string GetFamilyJsonFolder()
        {
            return Path.Combine(Constants.AppDataFolder, "FamilyJson");
        }

        private static bool TryPrepareFamilyJson(string text, out string normalizedJson, out FamilyDefinition definition, out string feedback)
        {
            return new FamilyDefinitionNormalizer().TryNormalize(text, out normalizedJson, out definition, out feedback);
        }

        private static bool TryNormalizeFamilyJsonCandidate(string candidate, out string normalizedJson, out FamilyDefinition definition, out string feedback)
        {
            normalizedJson = string.Empty;
            definition = null!;
            feedback = string.Empty;

            try
            {
                var token = JToken.Parse(candidate);
                if (token is not JObject obj)
                {
                    feedback = "Family JSON must be a root object.";
                    return false;
                }

                JObject normalizedObject = NormalizeFamilyJsonObject(obj);
                definition = normalizedObject.ToObject<FamilyDefinition>() ?? new FamilyDefinition();
                var validation = FamilyDefinitionValidator.Validate(definition);
                if (!validation.IsValid)
                {
                    feedback = FormatFamilyValidationErrors(validation.Errors);
                    definition = null!;
                    return false;
                }

                normalizedJson = normalizedObject.ToString(Formatting.Indented);
                return true;
            }
            catch (JsonReaderException ex)
            {
                feedback = "JSON syntax is invalid: " + ex.Message;
                return false;
            }
            catch (JsonSerializationException ex)
            {
                feedback = "Family JSON values need cleanup: " + ex.Message;
                return false;
            }
            catch
            {
                feedback = "Family JSON could not be read.";
                return false;
            }
        }

        private static IReadOnlyList<string> ExtractJsonObjectCandidates(string text)
        {
            var candidates = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return candidates;

            string cleaned = Regex.Replace(text.Trim(), "```(?:json)?", string.Empty, RegexOptions.IgnoreCase);
            int start = -1;
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < cleaned.Length; i++)
            {
                char ch = cleaned[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (ch == '\\')
                    {
                        escaped = true;
                    }
                    else if (ch == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                    continue;
                }

                if (ch == '{')
                {
                    if (depth == 0)
                    {
                        start = i;
                    }

                    depth++;
                    continue;
                }

                if (ch == '}' && depth > 0)
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        candidates.Add(cleaned.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }

            return candidates.Distinct().ToList();
        }

        private static int ScoreFamilyJsonCandidate(string candidate)
        {
            string lower = candidate.ToLowerInvariant();
            int score = 0;
            if (lower.Contains("\"components\"")) score += 8;
            if (lower.Contains("\"category\"")) score += 5;
            if (lower.Contains("\"parameters\"")) score += 4;
            if (lower.Contains("\"dims\"") || lower.Contains("\"dimensions\"")) score += 4;
            if (lower.Contains("\"geometry\"")) score += 3;
            if (lower.Contains("\"host\"")) score += 2;
            if (lower.Contains("\"units\"")) score += 2;
            return score;
        }

        private static JObject NormalizeFamilyJsonObject(JObject raw)
        {
            JObject root = UnwrapFamilyJsonRoot((JObject)raw.DeepClone());

            MoveAlias(root, "category", "familyCategory", "revitCategory", "categoryName");
            MoveAlias(root, "host", "hosting", "hostType");
            MoveAlias(root, "units", "unit", "unitSystem");
            MoveAlias(root, "components", "component", "elements", "parts", "geometryElements");
            MoveAlias(root, "parameters", "params", "familyParameters");

            NormalizeStringProperty(root, "category", NormalizeFamilyCategory);
            NormalizeStringProperty(root, "host", NormalizeHost);
            NormalizeStringProperty(root, "units", NormalizeUnits);

            if (FindProperty(root, "host") == null) root["host"] = "non-hosted";
            if (FindProperty(root, "units") == null) root["units"] = "mm";
            if (FindProperty(root, "parameters") == null) root["parameters"] = new JArray();

            if (GetPropertyValue(root, "components") is JObject singleComponent)
            {
                root["components"] = new JArray(singleComponent);
            }

            if (GetPropertyValue(root, "components") is JArray components)
            {
                foreach (JObject component in components.OfType<JObject>())
                {
                    NormalizeFamilyComponent(component);
                }
            }

            if (GetPropertyValue(root, "parameters") is JObject singleParameter)
            {
                root["parameters"] = new JArray(singleParameter);
            }

            if (GetPropertyValue(root, "parameters") is JArray parameters)
            {
                foreach (JObject parameter in parameters.OfType<JObject>())
                {
                    NormalizeFamilyParameter(parameter);
                }
            }

            return root;
        }

        private static JObject UnwrapFamilyJsonRoot(JObject root)
        {
            foreach (string wrapper in new[] { "family", "familyDefinition", "family_definition", "definition", "data", "result" })
            {
                if (GetPropertyValue(root, wrapper) is JObject child &&
                    ScoreFamilyJsonCandidate(child.ToString(Formatting.None)) >= ScoreFamilyJsonCandidate(root.ToString(Formatting.None)))
                {
                    return (JObject)child.DeepClone();
                }
            }

            var properties = root.Properties().ToList();
            if (properties.Count == 1 &&
                properties[0].Value is JObject onlyChild &&
                ScoreFamilyJsonCandidate(onlyChild.ToString(Formatting.None)) > 0)
            {
                return (JObject)onlyChild.DeepClone();
            }

            return root;
        }

        private static void NormalizeFamilyComponent(JObject component)
        {
            MoveAlias(component, "id", "name", "label", "componentId");
            MoveAlias(component, "geometry", "geometryType", "type");
            MoveAlias(component, "dims", "dimensions", "size", "bounds");
            MoveAlias(component, "material", "materialName", "finish");

            NormalizeStringProperty(component, "geometry", value =>
                string.Equals(value, "extrude", StringComparison.OrdinalIgnoreCase) ? "extrusion" : value);

            if (FindProperty(component, "geometry") == null)
            {
                component["geometry"] = "extrusion";
            }

            if (GetPropertyValue(component, "dims") is not JObject dims)
            {
                dims = new JObject();
                component["dims"] = dims;
            }

            CopyDimensionAlias(component, dims, "w", "width", "Width");
            CopyDimensionAlias(component, dims, "d", "depth", "Depth");
            CopyDimensionAlias(component, dims, "h", "height", "Height");
            MoveAlias(dims, "w", "width", "Width");
            MoveAlias(dims, "d", "depth", "Depth");
            MoveAlias(dims, "h", "height", "Height");
            NormalizeNumberProperty(dims, "w");
            NormalizeNumberProperty(dims, "d");
            NormalizeNumberProperty(dims, "h");
        }

        private static void NormalizeFamilyParameter(JObject parameter)
        {
            MoveAlias(parameter, "name", "id", "label", "parameterName");
            MoveAlias(parameter, "type", "parameterType", "valueType", "kind");
            MoveAlias(parameter, "default", "defaultValue", "value");
            MoveAlias(parameter, "instance", "isInstance", "instanceParameter");

            NormalizeStringProperty(parameter, "type", NormalizeParameterType);
            NormalizeNumberProperty(parameter, "default");
            NormalizeBooleanProperty(parameter, "instance");

            if (FindProperty(parameter, "instance") == null)
            {
                parameter["instance"] = false;
            }
        }

        private static void CopyDimensionAlias(JObject source, JObject dims, string canonical, params string[] aliases)
        {
            if (FindProperty(dims, canonical) != null) return;

            foreach (string alias in aliases)
            {
                var prop = FindProperty(source, alias);
                if (prop != null)
                {
                    dims[canonical] = prop.Value.DeepClone();
                    prop.Remove();
                    return;
                }
            }
        }

        private static void MoveAlias(JObject obj, string canonical, params string[] aliases)
        {
            var canonicalProp = FindProperty(obj, canonical);
            if (canonicalProp != null)
            {
                if (!string.Equals(canonicalProp.Name, canonical, StringComparison.Ordinal))
                {
                    var value = canonicalProp.Value.DeepClone();
                    canonicalProp.Remove();
                    obj[canonical] = value;
                }

                RemoveAliases(obj, aliases);
                return;
            }

            foreach (string alias in aliases)
            {
                var prop = FindProperty(obj, alias);
                if (prop == null) continue;
                obj[canonical] = prop.Value.DeepClone();
                prop.Remove();
                RemoveAliases(obj, aliases);
                return;
            }
        }

        private static void RemoveAliases(JObject obj, IEnumerable<string> aliases)
        {
            foreach (string alias in aliases)
            {
                var prop = FindProperty(obj, alias);
                if (prop != null)
                {
                    prop.Remove();
                }
            }
        }

        private static JProperty? FindProperty(JObject obj, string name)
        {
            return obj.Properties().FirstOrDefault(prop => string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static JToken? GetPropertyValue(JObject obj, string name)
        {
            return FindProperty(obj, name)?.Value;
        }

        private static void NormalizeStringProperty(JObject obj, string name, Func<string, string> normalize)
        {
            var prop = FindProperty(obj, name);
            if (prop?.Value.Type != JTokenType.String) return;
            string value = prop.Value.ToString().Trim();
            prop.Value = normalize(value);
        }

        private static void NormalizeNumberProperty(JObject obj, string name)
        {
            var prop = FindProperty(obj, name);
            if (prop == null) return;

            if (prop.Value.Type == JTokenType.Integer || prop.Value.Type == JTokenType.Float)
            {
                return;
            }

            if (prop.Value.Type == JTokenType.String &&
                TryParseNumberWithUnits(prop.Value.ToString(), out double number))
            {
                prop.Value = number;
            }
        }

        private static void NormalizeBooleanProperty(JObject obj, string name)
        {
            var prop = FindProperty(obj, name);
            if (prop == null) return;

            if (prop.Value.Type == JTokenType.Boolean)
            {
                return;
            }

            if (prop.Value.Type != JTokenType.String) return;
            string value = prop.Value.ToString().Trim();
            if (value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("instance", StringComparison.OrdinalIgnoreCase))
            {
                prop.Value = true;
            }
            else if (value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                     value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                     value.Equals("type", StringComparison.OrdinalIgnoreCase))
            {
                prop.Value = false;
            }
        }

        private static bool TryParseNumberWithUnits(string text, out double number)
        {
            number = 0;
            var match = Regex.Match(text, @"-?\d+(?:[.,]\d+)?");
            if (!match.Success) return false;

            string value = match.Value;
            int commaIndex = value.IndexOf(',');
            if (commaIndex >= 0 && !value.Contains('.'))
            {
                int digitsAfterComma = value.Length - commaIndex - 1;
                value = digitsAfterComma == 3 ? value.Replace(",", string.Empty) : value.Replace(",", ".");
            }
            else
            {
                value = value.Replace(",", string.Empty);
            }

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

        private static string NormalizeFamilyCategory(string value)
        {
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["cabinetry"] = "Casework",
                ["casework"] = "Casework",
                ["electrical"] = "Electrical Fixtures",
                ["electrical fixture"] = "Electrical Fixtures",
                ["electrical fixtures"] = "Electrical Fixtures",
                ["entourage"] = "Entourage",
                ["furniture"] = "Furniture",
                ["furniture system"] = "Furniture Systems",
                ["furniture systems"] = "Furniture Systems",
                ["generic"] = "Generic Model",
                ["generic model"] = "Generic Model",
                ["generic models"] = "Generic Model",
                ["lighting"] = "Lighting Fixtures",
                ["lighting fixture"] = "Lighting Fixtures",
                ["lighting fixtures"] = "Lighting Fixtures",
                ["mechanical"] = "Mechanical Equipment",
                ["mechanical equipment"] = "Mechanical Equipment",
                ["planting"] = "Planting",
                ["plumbing"] = "Plumbing Fixtures",
                ["plumbing fixture"] = "Plumbing Fixtures",
                ["plumbing fixtures"] = "Plumbing Fixtures",
                ["special equipment"] = "Specialty Equipment",
                ["specialty equipment"] = "Specialty Equipment"
            };

            return aliases.TryGetValue(value, out string? canonical) ? canonical : value;
        }

        private static string NormalizeHost(string value)
        {
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["none"] = "non-hosted",
                ["non hosted"] = "non-hosted",
                ["non-hosted"] = "non-hosted",
                ["standalone"] = "non-hosted",
                ["wall"] = "wall-hosted",
                ["wall hosted"] = "wall-hosted",
                ["wall-hosted"] = "wall-hosted",
                ["floor"] = "floor-hosted",
                ["floor hosted"] = "floor-hosted",
                ["floor-hosted"] = "floor-hosted",
                ["ceiling"] = "ceiling-hosted",
                ["ceiling hosted"] = "ceiling-hosted",
                ["ceiling-hosted"] = "ceiling-hosted",
                ["face"] = "face-based",
                ["face based"] = "face-based",
                ["face-based"] = "face-based"
            };

            return aliases.TryGetValue(value, out string? canonical) ? canonical : value;
        }

        private static string NormalizeUnits(string value)
        {
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["millimeter"] = "mm",
                ["millimeters"] = "mm",
                ["mm"] = "mm",
                ["centimeter"] = "cm",
                ["centimeters"] = "cm",
                ["cm"] = "cm",
                ["meter"] = "m",
                ["meters"] = "m",
                ["m"] = "m",
                ["inch"] = "in",
                ["inches"] = "in",
                ["in"] = "in",
                ["foot"] = "ft",
                ["feet"] = "ft",
                ["ft"] = "ft"
            };

            return aliases.TryGetValue(value, out string? canonical) ? canonical : value;
        }

        private static string NormalizeParameterType(string value)
        {
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bool"] = "YesNo",
                ["boolean"] = "YesNo",
                ["integer"] = "Integer",
                ["int"] = "Integer",
                ["length"] = "Length",
                ["material"] = "Material",
                ["number"] = "Number",
                ["numeric"] = "Number",
                ["real"] = "Number",
                ["string"] = "Text",
                ["text"] = "Text",
                ["yes/no"] = "YesNo",
                ["yesno"] = "YesNo",
                ["yes-no"] = "YesNo"
            };

            return aliases.TryGetValue(value, out string? canonical) ? canonical : value;
        }

        private static bool IsSameJsonDocument(string left, string right)
        {
            try
            {
                return JToken.DeepEquals(JToken.Parse(left), JToken.Parse(right));
            }
            catch
            {
                return string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);
            }
        }

        private static string BuildFamilyPreviewSummary(FamilyDefinition definition)
        {
            int componentCount = definition.Components?.Count ?? 0;
            int parameterCount = definition.Parameters?.Count ?? 0;
            return $"Category: {definition.Category} | Host: {definition.Host} | Units: {definition.Units} | Components: {componentCount} | Parameters: {parameterCount}";
        }

        private static string BuildFamilyPreviewHero(FamilyDefinition definition)
        {
            int componentCount = definition.Components?.Count ?? 0;
            int parameterCount = definition.Parameters?.Count ?? 0;
            return $"{definition.Category} | {definition.Host} | {definition.Units}\n{componentCount} components and {parameterCount} parameters validated for local preview.";
        }

        private static string BuildFamilyComponentPreview(FamilyDefinition definition)
        {
            var components = definition.Components ?? new List<FamilyComponentDefinition>();
            if (components.Count == 0) return "No components";

            return string.Join("\n", components
                .Take(4)
                .Select(component => $"{component.Id} | {component.Geometry} | {component.Dimensions.Width:g} x {component.Dimensions.Depth:g} x {component.Dimensions.Height:g}"));
        }

        private static string BuildFamilyParameterPreview(FamilyDefinition definition)
        {
            var parameters = definition.Parameters ?? new List<FamilyParameterDefinition>();
            if (parameters.Count == 0) return "No parameters";

            return string.Join("\n", parameters
                .Take(4)
                .Select(parameter => $"{parameter.Name} | {parameter.Type} | {(parameter.Instance ? "instance" : "type")}"));
        }

        private static string FormatFamilyValidationErrors(IReadOnlyList<string> errors)
        {
            return string.Join(" ", errors.Take(4));
        }

        private void AttachFamilyReference()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Attach Family reference",
                Filter = "Family inputs (*.png;*.jpg;*.jpeg;*.json)|*.png;*.jpg;*.jpeg;*.json|Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|JSON (*.json)|*.json|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            string path = dialog.FileName;
            string extension = Path.GetExtension(path).ToLowerInvariant();
            HandleFamilyReferencePath(path);
        }

        private void HandleFamilyReferencePath(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
            {
                HandleFamilyJsonPath(path, "Family JSON attached");
                return;
            }

            if (!IsFamilyReferenceImagePath(path))
            {
                SetPaneState(PaneState.CaptureFailed, "Attach an image or Family JSON file");
                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                string base64Image = Convert.ToBase64String(bytes);
                _familyReferenceBase64Image = base64Image;
                _familyReferencePath = path;
                _familyUiState = 1;
                _familyPipelineStep = 1;
                HideComposer();
                ShowBuildPipelinePanel("family");
                SetPaneState(PaneState.Ready, "Family reference attached");
                UpdateFamilyPipelineUi();
            }
            catch
            {
                SetPaneState(PaneState.CaptureFailed, "Could not attach reference");
            }
        }

        private static bool IsFamilyReferenceImagePath(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg";
        }

        private void FamilyBrowseReferenceButton_Click(object sender, RoutedEventArgs e)
        {
            AttachFamilyReference();
        }

        private void FamilyReferenceDropZone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            AttachFamilyReference();
        }

        private void FamilyReferenceDropZone_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files &&
                files.Length > 0)
            {
                string extension = Path.GetExtension(files[0]).ToLowerInvariant();
                if (extension == ".json" || IsFamilyReferenceImagePath(files[0]))
                {
                    e.Effects = DragDropEffects.Copy;
                }
            }

            e.Handled = true;
        }

        private void FamilyReferenceDropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files &&
                files.Length > 0)
            {
                HandleFamilyReferencePath(files[0]);
            }

            e.Handled = true;
        }

        private void AttachFamilyJsonFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Attach Family JSON",
                Filter = "JSON (*.json)|*.json|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                HandleFamilyJsonPath(dialog.FileName, "Family JSON attached");
            }
        }

        private void SnipBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isDisposed || App.IsShuttingDown) return;
            if (!CanStartCapture() || IsPanelOpen()) return;
            HideComposer();

            if (App.Instance.TryRaiseCapture(true, out string message))
            {
                SetPaneState(PaneState.Capturing, message);
            }
            else
            {
                SetPaneState(PaneState.CaptureFailed, message);
            }
        }

        private void ServiceTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string targetName) return;

            AiServiceTarget target = targetName switch
            {
                "Build" => AiServiceTarget.Build,
                _ => AiServiceTarget.Renders
            };

            SelectServiceTarget(target);
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isDisposed) return;
            if (!CanOpenSettings()) return;
            ShowSettingsPanel();
        }

        private void AboutBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isDisposed) return;
            if (!CanOpenAbout()) return;
            ShowAboutPanel();
        }

        private void PromptModifierChip_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is WpfToggleButton chip)
            {
                UpdatePromptModifierChipVisual(chip);
                UpdateModifierSummary();
            }

            if (_paneState == PaneState.Composing)
            {
                UpdateComposerPromptFromCurrentSelection();
            }
        }

        private void ComposerPromptText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingComposerPrompt || _composerPresetId != CustomPromptPresetId)
            {
                return;
            }

            SetCurrentCustomPromptText(ComposerPromptText.Text ?? string.Empty);
        }

        private void ComposerModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string modeName) return;

            PromptMode mode = modeName switch
            {
                "Trends" => PromptMode.Trends,
                "Build" => PromptMode.Build,
                _ => PromptMode.Visualize
            };

            SetComposerMode(mode, true);
        }

        private void RendersModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string modeName) return;

            PromptMode mode = PromptMode.Visualize;

            var preset = PromptPresetCatalog.GetDefaultForMode(mode);
            _composerPresetId = preset.Id;
            SelectActionPreset(preset);
            UpdateRendersModeSegment(mode);

            if (_paneState == PaneState.Composing && _activeTarget == AiServiceTarget.Renders)
            {
                SetComposerMode(mode, true);
            }
        }

        private void PresetCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string presetId) return;

            var preset = PromptPresetCatalog.GetById(presetId);
            if (_activeTarget == AiServiceTarget.Renders && preset.Mode != PromptMode.Visualize)
            {
                return;
            }

            _composerPresetId = preset.Id;
            SelectActionPreset(preset);
            if (_activeTarget == AiServiceTarget.Build && preset.Mode == PromptMode.Build)
            {
                ShowBuildPipelinePanel(preset.Id);
            }

            if (preset.Id == CustomPromptPresetId)
            {
                CollapsePromptPreview();
                UpdateSelectedPresetCard(preset.Id);
                UpdateComposerPromptFromCurrentSelection();
                return;
            }

            if (preset.Mode != _currentComposerMode)
            {
                SetComposerMode(preset.Mode, false);
            }
            else
            {
                UpdateSelectedPresetCard(preset.Id);
                UpdateComposerPromptFromCurrentSelection();
            }
        }

        private void StudioCard_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTarget != AiServiceTarget.Renders) return;
            if (sender is not Button button || button.Tag is not string presetId) return;

            var preset = PromptPresetCatalog.GetById(presetId);
            if (preset.Mode != PromptMode.Trends) return;

            _selectedStudioPresetId = string.Equals(_selectedStudioPresetId, presetId, StringComparison.Ordinal)
                ? null
                : presetId;
            UpdateStudioStyleVisuals();
            UpdateComposerPromptFromCurrentSelection();
        }

        private void ScrollPresetStripButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string tag) return;

            ScrollViewer? scroller = tag.StartsWith("Studio", StringComparison.Ordinal)
                ? TrendsPresetScroll
                : VisualizePresetScroll;
            double delta = tag.EndsWith("Left", StringComparison.Ordinal) ? -66 : 66;
            scroller.ScrollToHorizontalOffset(Math.Max(0, scroller.HorizontalOffset + delta));
        }

        private void PromptExpandButton_Click(object sender, RoutedEventArgs e)
        {
            _isPromptExpanded = !_isPromptExpanded;
            ApplyPromptExpansion();
        }

        private void ToggleModifiersButton_Click(object sender, RoutedEventArgs e)
        {
            _isModifiersExpanded = !_isModifiersExpanded;
            ApplyModifierExpansion();
        }

        private async void SendComposerBtn_Click(object sender, RoutedEventArgs e)
        {
            string? pendingImage = _pendingBase64Image;
            if (pendingImage == null || pendingImage.Length == 0) return;
            await SendCaptureAsync(pendingImage, GetComposerSendPrompt());
        }

        private void CancelComposerBtn_Click(object sender, RoutedEventArgs e)
        {
            HideComposer();
            if (_activeTarget == AiServiceTarget.Build &&
                string.Equals(_buildFunctionId, "family", StringComparison.OrdinalIgnoreCase))
            {
                _familyUiState = 1;
                _familyPipelineStep = 1;
                ShowBuildPipelinePanel("family");
                SetPaneState(PaneState.Ready, string.IsNullOrWhiteSpace(_familyReferenceBase64Image)
                    ? "Attach a Family reference"
                    : "Family reference ready");
                return;
            }

            SetPaneState(PaneState.Ready);
        }

        private void RetakeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTarget == AiServiceTarget.Build &&
                string.Equals(_buildFunctionId, "family", StringComparison.OrdinalIgnoreCase))
            {
                HideComposer();
                _familyUiState = 1;
                _familyPipelineStep = 1;
                ShowBuildPipelinePanel("family");
                AttachFamilyReference();
                return;
            }

            HideComposer();
            SetPaneState(PaneState.Ready);
            CaptureBtn_Click(sender, e);
        }

        private void ToggleContextBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_pendingRevitContext))
            {
                UpdateComposerContextCard();
                return;
            }

            _isContextIncludedInComposer = !_isContextIncludedInComposer;
            UpdateComposerPromptFromCurrentSelection();
        }

        private void UpdateComposerContextCard()
        {
            bool hasContext = !string.IsNullOrWhiteSpace(_pendingRevitContext);
            string summary = BuildContextSummary(_pendingRevitContext);

            if (!hasContext)
            {
                ComposerContextTitle.Text = "No spatial reference captured";
                ComposerContextText.Text = "No view metadata was captured for this image.";
                ComposerContextSummary.Text = ComposerContextText.Text;
                ComposerContextButton.Content = "Unavailable";
                ComposerContextButton.IsEnabled = false;
                return;
            }

            ComposerContextText.Text = summary;
            ComposerContextSummary.Text = summary;
            ComposerContextButton.IsEnabled = true;

            if (_isContextIncludedInComposer)
            {
                ComposerContextTitle.Text = "Spatial reference included";
                ComposerContextButton.Content = "Remove";
            }
            else
            {
                ComposerContextTitle.Text = "Spatial reference not included";
                ComposerContextButton.Content = "Add";
            }
        }

        private async void InlineBannerAction_Click(object sender, RoutedEventArgs e)
        {
            if (_paneState == PaneState.LoginRequired || _paneState == PaneState.UnsupportedPage)
            {
                try
                {
                    _webService.NavigateToTarget(AiWebBrowser, _activeTarget);
                    SetPaneState(PaneState.BrowserLoading, GetTargetLoadingLabel(_activeTarget));
                }
                catch
                {
                    SetPaneState(PaneState.UnsupportedPage, $"{GetTargetDisplayName(_activeTarget)} could not be opened.");
                }
                return;
            }

            if (_paneState == PaneState.CaptureFailed)
            {
                if (_activeTarget == AiServiceTarget.Build && string.Equals(_buildFunctionId, "family", StringComparison.OrdinalIgnoreCase))
                {
                    if (_familyUiState == 3)
                    {
                        await ImportLatestFamilyJsonFromBrowserAsync();
                        return;
                    }
                    if (_familyUiState == 2)
                    {
                        FamilyUseInlineJsonButton_Click(sender, e);
                        return;
                    }
                }

                CaptureBtn_Click(sender, e);
                return;
            }

            string? pendingImage = _pendingBase64Image;
            if (_paneState == PaneState.SendFailed && pendingImage != null && pendingImage.Length > 0)
            {
                await SendCaptureAsync(pendingImage, GetComposerSendPrompt());
            }
        }

        private void CancelSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            HideSettingsPanel();
            SetPaneState(PaneState.Ready);
        }

        private void CloseAboutBtn_Click(object sender, RoutedEventArgs e)
        {
            HideAboutPanel();
            SetPaneState(PaneState.Ready);
        }

        private void SupportDevelopmentBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenExternalUrl(Constants.PatreonUrl);
        }

        private void RepositoryBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenExternalUrl(Constants.RepositoryUrl);
        }

        private void ReleasesBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenExternalUrl(Constants.ReleasesUrl);
        }

        private async void CheckUpdatesBtn_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync();
        }

        private async void DownloadUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            await DownloadUpdateAsync();
        }

        private void SaveSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsControls();
            HideSettingsPanel();
            SetPaneState(PaneState.Ready);
        }

        private void ResetSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var defaults = new AppSettings();
            var settings = SettingsManager.Settings;
            settings.IsDarkTheme = defaults.IsDarkTheme;
            settings.CaptureResolutionIndex = defaults.CaptureResolutionIndex;
            settings.AutoArchiveCaptures = defaults.AutoArchiveCaptures;
            settings.ForceWhiteBackground = defaults.ForceWhiteBackground;
            settings.ChangeDisplayMode = defaults.ChangeDisplayMode;
            settings.TargetDisplayStyleInt = defaults.TargetDisplayStyleInt;
            settings.IncludeRevitContext = defaults.IncludeRevitContext;
            settings.PromptPresetId = defaults.PromptPresetId;
            settings.AutoSendCaptures = defaults.AutoSendCaptures;
            settings.DefaultPromptMode = defaults.DefaultPromptMode;
            settings.CustomPromptText = defaults.CustomPromptText;
            settings.VisualizeCustomPromptText = defaults.VisualizeCustomPromptText;
            settings.TrendsCustomPromptText = defaults.TrendsCustomPromptText;
            settings.ShowTrendsInComposer = true;
            settings.ShowTrendsInActionBar = defaults.ShowTrendsInActionBar;
            settings.BuildFunctionId = defaults.BuildFunctionId;
            settings.RequireFamilyPreviewBeforeGenerate = defaults.RequireFamilyPreviewBeforeGenerate;
            SettingsManager.Save();
            ApplyTheme(settings.IsDarkTheme);
            LoadSettingsControls();
            LoadPromptControls();
        }

        private void CopyPromptBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(GetComposerSendPrompt());
                SetStatus("Prompt copied");
            }
            catch
            {
                SetStatus("Could not copy prompt");
            }
        }

        public void NotifyCaptureFailed(string message)
        {
            if (_isDisposed) return;
            SetPaneState(PaneState.CaptureFailed, string.IsNullOrWhiteSpace(message) ? "Capture failed" : message);
        }

        public void ShowSnipCropWindow(string base64Image, string revitContext)
        {
            byte[] bytes = Convert.FromBase64String(base64Image);
            var bmpImage = new BitmapImage();
            using (var ms = new MemoryStream(bytes))
            {
                bmpImage.BeginInit();
                bmpImage.CacheOption = BitmapCacheOption.OnLoad;
                bmpImage.StreamSource = ms;
                bmpImage.EndInit();
            }

            Brush backgroundPrimary = (Brush)FindResource("Brush.Background.Primary");
            Brush backgroundSecondary = (Brush)FindResource("Brush.Background.Secondary");
            Brush backgroundCanvas = (Brush)FindResource("Brush.Background.Canvas");
            Brush borderTertiary = (Brush)FindResource("Brush.Border.Tertiary");
            Brush textPrimary = (Brush)FindResource("Brush.Text.Primary");
            Brush accentPrimary = (Brush)FindResource("Brush.Accent.Primary");
            Brush accentPrimaryText = (Brush)FindResource("Brush.Accent.PrimaryText");
            Brush selectionBrush = (Brush)FindResource("Brush.State.Warning");

            var cropWindow = new Window
            {
                Title = "Crop Capture - Drag mouse to select area",
                Width = Math.Min(900, bmpImage.PixelWidth + 40),
                Height = Math.Min(700, bmpImage.PixelHeight + 120),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResize,
                Owner = Window.GetWindow(this),
                Background = backgroundPrimary,
                Foreground = textPrimary,
                FontFamily = new FontFamily("Segoe UI")
            };

            var grid = new Grid { Background = backgroundPrimary };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var containerGrid = new Grid { Background = backgroundCanvas };
            var imgControl = new Image { Source = bmpImage, Stretch = System.Windows.Media.Stretch.Uniform };
            containerGrid.Children.Add(imgControl);

            var overlay = new Canvas { Background = System.Windows.Media.Brushes.Transparent };
            var rect = new System.Windows.Shapes.Rectangle
            {
                Stroke = selectionBrush,
                StrokeThickness = 2,
                Visibility = Visibility.Collapsed
            };
            overlay.Children.Add(rect);
            containerGrid.Children.Add(overlay);
            grid.Children.Add(containerGrid);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = backgroundSecondary,
                Margin = new Thickness(10)
            };
            var cropBtn = new Button
            {
                Content = "Crop & Inject",
                Padding = new Thickness(15, 6, 15, 6),
                Margin = new Thickness(5),
                Background = accentPrimary,
                Foreground = accentPrimaryText,
                BorderBrush = accentPrimary,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            var cancelBtn = new Button
            {
                Content = "Cancel",
                Padding = new Thickness(15, 6, 15, 6),
                Margin = new Thickness(5),
                Background = backgroundSecondary,
                Foreground = textPrimary,
                BorderBrush = borderTertiary,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            buttonPanel.Children.Add(cropBtn);
            buttonPanel.Children.Add(cancelBtn);
            grid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 1);

            bool isDragging = false;
            bool cropAccepted = false;
            Point startPoint = new Point();
            double rectX = 0, rectY = 0, rectW = 0, rectH = 0;

            overlay.MouseDown += (s, ev) =>
            {
                if (ev.LeftButton == MouseButtonState.Pressed)
                {
                    isDragging = true;
                    startPoint = ev.GetPosition(overlay);
                    rect.Visibility = Visibility.Visible;
                    overlay.CaptureMouse();
                }
            };

            overlay.MouseMove += (s, ev) =>
            {
                if (isDragging)
                {
                    Point currentPoint = ev.GetPosition(overlay);
                    rectX = Math.Min(startPoint.X, currentPoint.X);
                    rectY = Math.Min(startPoint.Y, currentPoint.Y);
                    rectW = Math.Abs(currentPoint.X - startPoint.X);
                    rectH = Math.Abs(currentPoint.Y - startPoint.Y);

                    Canvas.SetLeft(rect, rectX);
                    Canvas.SetTop(rect, rectY);
                    rect.Width = rectW;
                    rect.Height = rectH;
                }
            };

            overlay.MouseUp += (s, ev) =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    overlay.ReleaseMouseCapture();
                }
            };

            cropBtn.Click += (s, ev) =>
            {
                if (rectW <= 5 || rectH <= 5)
                {
                    MessageBox.Show("Draw a selection box first by dragging the mouse over the image.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                try
                {
                    double dispWidth = overlay.ActualWidth;
                    double dispHeight = overlay.ActualHeight;

                    double imgRatio = bmpImage.Width / bmpImage.Height;
                    double containerRatio = dispWidth / dispHeight;

                    double displayedImgW, displayedImgH;
                    double offsetX = 0, offsetY = 0;

                    if (imgRatio > containerRatio)
                    {
                        displayedImgW = dispWidth;
                        displayedImgH = dispWidth / imgRatio;
                        offsetY = (dispHeight - displayedImgH) / 2;
                    }
                    else
                    {
                        displayedImgH = dispHeight;
                        displayedImgW = dispHeight * imgRatio;
                        offsetX = (dispWidth - displayedImgW) / 2;
                    }

                    double relativeSelX = (rectX - offsetX) / displayedImgW;
                    double relativeSelY = (rectY - offsetY) / displayedImgH;
                    double relativeSelW = rectW / displayedImgW;
                    double relativeSelH = rectH / displayedImgH;

                    int pixelX = (int)Math.Max(0, Math.Round(relativeSelX * bmpImage.PixelWidth));
                    int pixelY = (int)Math.Max(0, Math.Round(relativeSelY * bmpImage.PixelHeight));
                    int pixelW = (int)Math.Min(bmpImage.PixelWidth - pixelX, Math.Round(relativeSelW * bmpImage.PixelWidth));
                    int pixelH = (int)Math.Min(bmpImage.PixelHeight - pixelY, Math.Round(relativeSelH * bmpImage.PixelHeight));

                    if (pixelW <= 0 || pixelH <= 0)
                    {
                        MessageBox.Show("Invalid crop selection.");
                        return;
                    }

                    using var sourceBmp = new System.Drawing.Bitmap(new MemoryStream(bytes));
                    using var croppedBmp = new System.Drawing.Bitmap(pixelW, pixelH);
                    using (var g = System.Drawing.Graphics.FromImage(croppedBmp))
                    {
                        g.DrawImage(sourceBmp, new System.Drawing.Rectangle(0, 0, pixelW, pixelH),
                                     new System.Drawing.Rectangle(pixelX, pixelY, pixelW, pixelH),
                                     System.Drawing.GraphicsUnit.Pixel);
                    }

                    using var ms = new MemoryStream();
                    croppedBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    string base64Cropped = Convert.ToBase64String(ms.ToArray());

                    cropAccepted = true;
                    HandleCapturedImage(base64Cropped, revitContext);
                    cropWindow.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Crop failed: " + ex.Message);
                }
            };

            cancelBtn.Click += (s, ev) => cropWindow.Close();
            cropWindow.Closed += (s, ev) =>
            {
                if (!cropAccepted && !_isDisposed)
                {
                    SetPaneState(PaneState.Ready);
                }
            };

            cropWindow.Content = grid;
            cropWindow.ShowDialog();
        }
    }
}

