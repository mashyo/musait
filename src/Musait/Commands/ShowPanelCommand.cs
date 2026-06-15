// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Musait.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowPanelCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                DockablePane dockablePane = commandData.Application.GetDockablePane(App.PaneId);
                if (dockablePane != null)
                {
                    dockablePane.Show();
                }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}

