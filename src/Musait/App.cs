// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Musait.Services;
using Musait.UI.Views;

namespace Musait
{
    public class App : IExternalApplication
    {
        public static readonly DockablePaneId PaneId = new DockablePaneId(new Guid("7C9A4B2E-3D1F-4E8C-A5B6-9F0D2E1C4A3B"));
        public static App Instance { get; private set; } = null!;
        public static bool IsShuttingDown { get; private set; } = false;
        private AiDockablePaneProvider? _paneProvider;
        private RenderEventHandler? _renderHandler;
        private ExternalEvent? _renderEvent;
        private bool _isCaptureRequestPending;
        public AiDockablePane? MainPane { get; private set; }

        public Result OnStartup(UIControlledApplication application)
        {
            Instance = this;
            
            try
            {
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    try
                    {
                        LogException(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
                    }
                    catch
                    {
                    }
                };

                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    try
                    {
                        LogException(e.Exception, "TaskScheduler.UnobservedTaskException");
                        e.SetObserved();
                    }
                    catch
                    {
                    }
                };
            }
            catch
            {
            }

            try
            {
                CreateRibbonAndPanel(application);
                CreateCaptureEvent();
                RegisterDockablePanel(application);
                SubscribeRevitContextEvents(application);
            }
            catch (Exception ex)
            {
                LogException(ex, "OnStartup.Register");
                string logPath = Path.Combine(Path.GetTempPath(), "Musait_StartupError.log");
                File.WriteAllText(logPath, ex.ToString());
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        private static void LogException(Exception? ex, string context)
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), "Musait_Error.log");
                string text = $"[{DateTime.Now:u}] Context: {context} | Process: {Process.GetCurrentProcess().ProcessName} | CLR: {Environment.Version}\r\n";
                string text2 = ex?.ToString() ?? "(no exception object)";
                File.AppendAllText(path, text + text2 + "\r\n--------------------------------------------------------\r\n");
            }
            catch
            {
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            IsShuttingDown = true;

            try
            {
                (MainPane ?? AiDockablePane.Instance)?.PrepareForRevitShutdown();
            }
            catch (Exception ex)
            {
                LogException(ex, "OnShutdown.PrepareForRevitShutdown");
            }

            try
            {
                UnsubscribeRevitContextEvents(application);
            }
            catch
            {
                // Ignore event unsubscription errors during shutdown
            }

            DisposeExternalEvents();
            MainPane = null;
            _renderHandler = null;
            return Result.Succeeded;
        }

        private void CreateCaptureEvent()
        {
            _renderHandler = new RenderEventHandler();
            _renderEvent = ExternalEvent.Create(_renderHandler);
        }

        private void DisposeExternalEvents()
        {
            try
            {
                _renderEvent?.Dispose();
            }
            catch
            {
            }
            finally
            {
                _renderEvent = null;
            }
        }

        internal bool TryRaiseCapture(bool isSnipRequested, out string message)
        {
            message = isSnipRequested ? "Capturing snip" : "Capturing view";

            if (IsShuttingDown)
            {
                message = "Musait is shutting down.";
                return false;
            }

            if (_renderHandler == null || _renderEvent == null)
            {
                message = "Capture service is not ready. Restart Revit and try again.";
                return false;
            }

            if (_isCaptureRequestPending)
            {
                message = "Capture is already queued. Wait for Revit to finish the current action.";
                return false;
            }

            _renderHandler.IsSnipRequested = isSnipRequested;

            ExternalEventRequest request;
            try
            {
                request = _renderEvent.Raise();
            }
            catch (Exception ex)
            {
                _renderHandler.IsSnipRequested = false;
                LogException(ex, "TryRaiseCapture.Raise");
                message = "Capture could not be queued.";
                return false;
            }

            if (request == ExternalEventRequest.Accepted)
            {
                _isCaptureRequestPending = true;
                return true;
            }

            _renderHandler.IsSnipRequested = false;
            message = request switch
            {
                ExternalEventRequest.Pending => "Capture is already queued. Wait for Revit to finish the current action.",
                ExternalEventRequest.Denied => "Revit rejected the capture request. Try again after switching to an active project view.",
                ExternalEventRequest.TimedOut => "Revit is busy. Finish the current command, then try again.",
                _ => "Capture could not be queued."
            };

            return false;
        }

        internal void CompleteCaptureRequest()
        {
            _isCaptureRequestPending = false;
        }

        private void RegisterDockablePanel(UIControlledApplication application)
        {
            _paneProvider = new AiDockablePaneProvider(this);
            application.RegisterDockablePane(PaneId, "Musait", _paneProvider);
        }

        internal void AttachPane(AiDockablePane pane)
        {
            var previous = MainPane;
            MainPane = pane;

            if (previous != null && previous != pane && !previous.IsLoaded)
            {
                try
                {
                    previous.Dispose();
                }
                catch
                {
                    // Ignore disposal errors for stale pane instances
                }
            }
        }

        internal void DetachPane(AiDockablePane pane)
        {
            if (MainPane == pane)
            {
                MainPane = null;
            }
        }

        private void SubscribeRevitContextEvents(UIControlledApplication application)
        {
            application.ApplicationClosing += OnApplicationClosing;
            application.ViewActivated += OnViewActivated;
            application.ControlledApplication.DocumentClosing += OnDocumentClosing;
            application.ControlledApplication.DocumentOpened += OnDocumentOpened;
            application.ControlledApplication.DocumentCreated += OnDocumentCreated;
            application.ControlledApplication.DocumentClosed += OnDocumentClosed;
        }

        private void UnsubscribeRevitContextEvents(UIControlledApplication application)
        {
            application.ApplicationClosing -= OnApplicationClosing;
            application.ViewActivated -= OnViewActivated;
            application.ControlledApplication.DocumentClosing -= OnDocumentClosing;
            application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
            application.ControlledApplication.DocumentCreated -= OnDocumentCreated;
            application.ControlledApplication.DocumentClosed -= OnDocumentClosed;
        }

        private void OnApplicationClosing(object? sender, ApplicationClosingEventArgs e)
        {
            IsShuttingDown = true;

            try
            {
                (MainPane ?? AiDockablePane.Instance)?.PrepareForRevitShutdown();
            }
            catch (Exception ex)
            {
                LogException(ex, "OnApplicationClosing.PrepareForRevitShutdown");
            }

            DisposeExternalEvents();

            try
            {
                LaunchPendingUpdateInstaller();
            }
            catch (Exception ex)
            {
                LogException(ex, "OnApplicationClosing.LaunchPendingUpdateInstaller");
            }
        }

        private static void LaunchPendingUpdateInstaller()
        {
            string? installerPath = UpdateService.PendingInstallerPath;
            if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
            {
                return;
            }

            Process.Start(new ProcessStartInfo(installerPath)
            {
                UseShellExecute = true
            });
            UpdateService.ClearPendingInstaller();
        }

        private void OnViewActivated(object? sender, ViewActivatedEventArgs e)
        {
            if (e.Status != RevitAPIEventStatus.Succeeded) return;
            NotifyPaneRevitContextChanged();
        }

        private void OnDocumentClosing(object? sender, DocumentClosingEventArgs e)
        {
            if (IsShuttingDown) return;

            try
            {
                if (e.Document?.Application?.Documents?.Size == 1)
                {
                    (MainPane ?? AiDockablePane.Instance)?.ResetBrowserForRevitHostTransition();
                }
            }
            catch (Exception ex)
            {
                LogException(ex, "OnDocumentClosing.ResetBrowserForRevitHostTransition");
            }
        }

        private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
        {
            if (!IsSuccessful(e)) return;
            NotifyPaneRevitContextChanged();
        }

        private void OnDocumentCreated(object? sender, DocumentCreatedEventArgs e)
        {
            if (!IsSuccessful(e)) return;
            NotifyPaneRevitContextChanged();
        }

        private void OnDocumentClosed(object? sender, DocumentClosedEventArgs e)
        {
            if (!IsSuccessful(e)) return;
            NotifyPaneRevitContextChanged();
        }

        private static bool IsSuccessful(RevitAPIPostEventArgs e)
        {
            return e.Status == RevitAPIEventStatus.Succeeded;
        }

        private void NotifyPaneRevitContextChanged()
        {
            try
            {
                var pane = MainPane ?? AiDockablePane.Instance;
                pane?.NotifyRevitContextChanged();
            }
            catch (Exception ex)
            {
                LogException(ex, "NotifyPaneRevitContextChanged");
            }
        }

        private sealed class AiDockablePaneProvider : IDockablePaneProvider, IFrameworkElementCreator
        {
            private readonly App _app;

            public AiDockablePaneProvider(App app)
            {
                _app = app;
            }

            public FrameworkElement CreateFrameworkElement()
            {
                var pane = new AiDockablePane();
                _app.AttachPane(pane);
                return pane;
            }

            public void SetupDockablePane(DockablePaneProviderData data)
            {
                data.FrameworkElement = null!;
                data.FrameworkElementCreator = this;
                data.InitialState = new DockablePaneState
                {
                    DockPosition = DockPosition.Right
                };
                data.EditorInteraction = new EditorInteraction(EditorInteractionType.KeepAlive);
            }
        }

        private void CreateRibbonAndPanel(UIControlledApplication application)
        {
            string tabName = "Mashyo Tools";
            string panelName = "Gemini Rendering";

            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Tab might already exist
            }

            var panel = application.GetRibbonPanels(tabName)
                .FirstOrDefault(p => p.Name == panelName) 
                ?? application.CreateRibbonPanel(tabName, panelName);

            string assemblyLocation = Assembly.GetExecutingAssembly().Location;

            var buttonData = new PushButtonData(
                "cmdShowAiPane",
                "Musait",
                assemblyLocation,
                "Musait.Commands.ShowPanelCommand"
            )
            {
                ToolTip = "Open the Musait chat panel."
            };

            var ribbonItem = panel.AddItem(buttonData);
            if (ribbonItem is PushButton button)
            {
                button.LargeImage = LoadRibbonImage("Musait-32x32.png");
                button.Image = LoadRibbonImage("Musait-16x16.png");
            }
        }

        private static ImageSource? LoadRibbonImage(string fileName)
        {
            try
            {
                string imagePath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                    "Assets",
                    fileName);
                if (!File.Exists(imagePath))
                {
                    return null;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}

