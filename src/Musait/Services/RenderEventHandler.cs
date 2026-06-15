// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Musait.Models;
using Musait.UI.Views;

namespace Musait.Services
{
    public class RenderEventHandler : IExternalEventHandler
    {
        public bool IsSnipRequested { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                if (App.IsShuttingDown) return;
                if (app == null) return;

                UIDocument activeUIDocument = app.ActiveUIDocument;
                if (activeUIDocument == null)
                {
                    NotifyCaptureFailed("Open a Revit document before capturing.");
                    return;
                }

                Document doc = activeUIDocument.Document;
                if (doc == null || !doc.IsValidObject)
                {
                    NotifyCaptureFailed("The active Revit document is not available.");
                    return;
                }

                View activeView = activeUIDocument.ActiveView;
                if (activeView == null || !activeView.IsValidObject)
                {
                    NotifyCaptureFailed("The active Revit view is not available.");
                    return;
                }

                string revitContext = RevitContextExtractor.GetCaptureContext(app);
                string tempFolder = Path.GetTempPath();
                string baseFileName = $"Capture_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
                
                // Set capture size based on settings resolution
                int resIndex = SettingsManager.Settings.CaptureResolutionIndex;
                GetQualitySettings(resIndex, out int pixelSizeX, out int pixelSizeY);
                int finalPixelSize = Math.Max(pixelSizeX, pixelSizeY);

                // Run export with temporary view settings if needed
                ExecuteWithTemporarySettings(doc, activeView, delegate
                {
                    if (doc == null || !doc.IsValidObject) return;

                    using (ImageExportOptions options = new ImageExportOptions())
                    {
                        options.ExportRange = ExportRange.VisibleRegionOfCurrentView;
                        options.FilePath = Path.Combine(tempFolder, baseFileName);
                        options.FitDirection = FitDirectionType.Horizontal;
                        options.HLRandWFViewsFileType = ImageFileType.PNG;
                        options.ShadowViewsFileType = ImageFileType.PNG;
                        options.PixelSize = finalPixelSize;
                        options.ZoomType = ZoomFitType.FitToPage;

                        doc.ExportImage(options);
                    }
                });

                string[] exportedFiles = Directory.GetFiles(tempFolder, baseFileName + "*.png");
                if (exportedFiles.Length == 0)
                {
                    NotifyCaptureFailed("Revit did not generate a capture image.");
                    return;
                }

                string originalImagePath = exportedFiles[0];
                string base64Image = ProcessImage(originalImagePath, pixelSizeX, pixelSizeY);

                // Delete temporary file safely
                try { File.Delete(originalImagePath); } catch {}

                // Notify UI on the main thread
                var pane = AiDockablePane.Instance;
                if (pane != null && !pane.Dispatcher.HasShutdownStarted)
                {
                    try
                    {
                        pane.Dispatcher.Invoke(() =>
                        {
                            if (pane.Dispatcher.HasShutdownStarted) return;
                            if (IsSnipRequested)
                            {
                                IsSnipRequested = false;
                                pane.ShowSnipCropWindow(base64Image, revitContext);
                            }
                            else
                            {
                                pane.HandleCapturedImage(base64Image, revitContext);
                            }
                        });
                    }
                    catch
                    {
                        // Suppress Dispatcher exceptions during shutdown
                    }
                }
            }
            catch (Exception ex)
            {
                NotifyCaptureFailed("Capture failed: " + ex.Message);
            }
            finally
            {
                App.Instance?.CompleteCaptureRequest();
                IsSnipRequested = false;
            }
        }

        private static void NotifyCaptureFailed(string message)
        {
            var pane = AiDockablePane.Instance;
            if (pane == null || pane.Dispatcher.HasShutdownStarted) return;

            try
            {
                pane.Dispatcher.Invoke(() =>
                {
                    if (!pane.Dispatcher.HasShutdownStarted)
                    {
                        pane.NotifyCaptureFailed(message);
                    }
                });
            }
            catch
            {
                // Suppress Dispatcher exceptions during shutdown
            }
        }

        private void ExecuteWithTemporarySettings(Document doc, View view, Action exportAction)
        {
            if (doc == null || !doc.IsValidObject || view == null || !view.IsValidObject)
            {
                exportAction();
                return;
            }

            var settings = SettingsManager.Settings;
            bool changed = false;
            DisplayStyle originalStyle = DisplayStyle.Wireframe;

            try
            {
                if (settings.ChangeDisplayMode)
                {
                    try
                    {
                        originalStyle = view.DisplayStyle;
                        if ((int)originalStyle != settings.TargetDisplayStyleInt)
                        {
                            using (var trans = new Transaction(doc, "Temp Display Style for Capture"))
                            {
                                trans.Start();
                                view.DisplayStyle = (DisplayStyle)settings.TargetDisplayStyleInt;
                                trans.Commit();
                            }
                            changed = true;
                        }
                    }
                    catch
                    {
                        // The view might not support DisplayStyle (e.g. schedules, drafting views)
                    }
                }

                exportAction();
            }
            finally
            {
                if (changed && doc != null && doc.IsValidObject && view != null && view.IsValidObject)
                {
                    try
                    {
                        using (var revertTrans = new Transaction(doc, "Revert Display Style"))
                        {
                            revertTrans.Start();
                            view.DisplayStyle = originalStyle;
                            revertTrans.Commit();
                        }
                    }
                    catch
                    {
                        // Ignore revert errors
                    }
                }
            }
        }

        private string ProcessImage(string imagePath, int targetWidth, int targetHeight)
        {
            using var original = new Bitmap(imagePath);
            using var resized = new Bitmap(targetWidth, targetHeight);
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(SettingsManager.Settings.ForceWhiteBackground ? System.Drawing.Color.White : System.Drawing.Color.Transparent);

                float scaleX = (float)targetWidth / original.Width;
                float scaleY = (float)targetHeight / original.Height;
                float scale = Math.Max(scaleX, scaleY);

                int w = (int)(original.Width * scale);
                int h = (int)(original.Height * scale);
                int x = (targetWidth - w) / 2;
                int y = (targetHeight - h) / 2;

                g.DrawImage(original, x, y, w, h);
            }

            // Save to archives if enabled
            if (SettingsManager.Settings.AutoArchiveCaptures)
            {
                try
                {
                    Directory.CreateDirectory(Constants.CapturesFolder);
                    string archivePath = Path.Combine(Constants.CapturesFolder, $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                    resized.Save(archivePath, ImageFormat.Png);
                }
                catch {}
            }

            using var ms = new MemoryStream();
            resized.Save(ms, ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }

        private void GetQualitySettings(int index, out int pixelSizeX, out int pixelSizeY)
        {
            switch (index)
            {
                case 0: pixelSizeX = 1024; pixelSizeY = 768; break;
                case 1: pixelSizeX = 1024; pixelSizeY = 1024; break;
                case 2: pixelSizeX = 1920; pixelSizeY = 1080; break;
                case 3: pixelSizeX = 2560; pixelSizeY = 1440; break;
                case 4: pixelSizeX = 3840; pixelSizeY = 2160; break;
                case 5: pixelSizeX = 4096; pixelSizeY = 2160; break;
                default: pixelSizeX = 1024; pixelSizeY = 768; break;
            }
        }

        public string GetName()
        {
            return "Musait Viewport Capture Event";
        }
    }
}

