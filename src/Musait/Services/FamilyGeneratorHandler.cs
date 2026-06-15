// Copyright (c) 2026 Mashyo. All Rights Reserved.

using Autodesk.Revit.UI;
using Musait.Models;
using Musait.UI.Views;

namespace Musait.Services
{
    public sealed class FamilyGeneratorHandler : IExternalEventHandler
    {
        public FamilyGeneratorRequest? PendingRequest { get; set; }

        public void Execute(UIApplication app)
        {
            PendingRequest = null;
            NotifyCompleted(FamilyGeneratorResult.Failure(
                MusaitCapabilities.CanCreateRfa
                    ? "Family generation is not available in this build."
                    : "Create RFA is available in Musait Pro."));
            App.Instance?.CompleteFamilyGenerationRequest();
        }

        public string GetName()
        {
            return "Musait Family RFA Generator Event";
        }

        private static void NotifyCompleted(FamilyGeneratorResult result)
        {
            if (App.IsShuttingDown) return;
            var pane = AiDockablePane.Instance;
            if (pane == null || pane.Dispatcher.HasShutdownStarted) return;

            try
            {
                pane.Dispatcher.Invoke(() => pane.NotifyFamilyGenerationCompleted(result));
            }
            catch
            {
            }
        }
    }
}
