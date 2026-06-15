// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Reflection;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Musait.Services
{
    public static class RevitContextExtractor
    {
        public static string GetCaptureContext(UIApplication app)
        {
            try
            {
                var uiDoc = app.ActiveUIDocument;
                var doc = uiDoc?.Document;
                var view = uiDoc?.ActiveView;
                if (doc == null || view == null) return string.Empty;

                var sb = new StringBuilder();
                sb.AppendLine("[Revit capture context]");
                sb.AppendLine("Image is the primary source of truth.");
                sb.AppendLine($"Project: {doc.Title}");
                sb.AppendLine($"View: {view.Name}");
                sb.AppendLine($"View type: {view.ViewType}");
                AppendUnits(sb, doc);

                if (view.Scale > 0)
                {
                    sb.AppendLine($"Scale: 1:{view.Scale}");
                }

                string selectionContext = GetSelectionContext(app);
                if (!string.IsNullOrWhiteSpace(selectionContext))
                {
                    sb.AppendLine();
                    sb.Append(selectionContext.Trim());
                }

                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string GetSelectionContext(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument?.Document;
                var selection = app.ActiveUIDocument?.Selection;
                if (doc == null || selection == null) return string.Empty;

                var selectedIds = selection.GetElementIds();
                if (selectedIds.Count == 0) return string.Empty;

                var sb = new StringBuilder();
                sb.AppendLine("[Context from selected Revit elements]");
                sb.AppendLine($"Selected count: {selectedIds.Count}");

                int limit = 10;
                int count = 0;
                foreach (var id in selectedIds)
                {
                    var elem = doc.GetElement(id);
                    if (elem == null) continue;

                    sb.AppendLine($"- Element ID: {id}, Category: {elem.Category?.Name ?? "None"}, Name: {elem.Name}");
                    count++;
                    if (count >= limit)
                    {
                        sb.AppendLine("- ... (additional selected elements truncated)");
                        break;
                    }
                }
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void AppendUnits(StringBuilder sb, Document doc)
        {
            string units = TryGetLengthUnits(doc);
            if (!string.IsNullOrWhiteSpace(units))
            {
                sb.AppendLine($"Units: {units}");
            }
        }

        private static string TryGetLengthUnits(Document doc)
        {
            try
            {
                object units = doc.GetUnits();
                Type? specTypeId = Type.GetType("Autodesk.Revit.DB.SpecTypeId, RevitAPI");
                object? lengthSpec = specTypeId?.GetProperty("Length", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (lengthSpec == null) return string.Empty;

                MethodInfo? getFormatOptions = units.GetType().GetMethod("GetFormatOptions", new[] { lengthSpec.GetType() });
                object? formatOptions = getFormatOptions?.Invoke(units, new[] { lengthSpec });
                object? unitTypeId = formatOptions?.GetType().GetMethod("GetUnitTypeId", Type.EmptyTypes)?.Invoke(formatOptions, Array.Empty<object>());
                string? typeId = unitTypeId?.GetType().GetProperty("TypeId")?.GetValue(unitTypeId)?.ToString();
                return FormatUnitName(string.IsNullOrWhiteSpace(typeId) ? unitTypeId?.ToString() ?? string.Empty : typeId ?? string.Empty);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string FormatUnitName(string typeId)
        {
            if (string.IsNullOrWhiteSpace(typeId)) return string.Empty;

            const string prefix = "autodesk.unit.unit:";
            string text = typeId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? typeId.Substring(prefix.Length)
                : typeId;

            int versionSeparator = text.IndexOf('-');
            if (versionSeparator > 0)
            {
                text = text.Substring(0, versionSeparator);
            }

            return text.Replace('_', ' ');
        }
    }
}

