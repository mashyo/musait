// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Musait.Models;

namespace Musait.Services
{
    public static class FamilyRepeaterMaterializer
    {
        public static IReadOnlyList<FamilyRigDiagnostic> Materialize(FamilyDefinition definition)
        {
            var diagnostics = new List<FamilyRigDiagnostic>();
            if (definition.Repeaters == null || definition.Repeaters.Count == 0) return diagnostics;
            if (definition.Components == null || definition.Components.Count == 0) return diagnostics;

            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in definition.Parameters ?? new List<FamilyParameterDefinition>())
            {
                if (TryGetNumber(parameter.Default, out double value)) values[parameter.Name] = value;
            }

            foreach (var repeater in definition.Repeaters)
            {
                var template = definition.Components.FirstOrDefault(component => string.Equals(component.Id, repeater.TemplateComponent, StringComparison.OrdinalIgnoreCase));
                if (template == null)
                {
                    diagnostics.Add(new FamilyRigDiagnostic { Severity = "warning", Message = $"Repeater {repeater.Id} references missing template component.", Component = repeater.TemplateComponent });
                    continue;
                }

                int count = values.TryGetValue(repeater.CountParameter, out double rawCount)
                    ? Math.Max(0, Convert.ToInt32(Math.Round(rawCount)))
                    : 0;
                if (count <= 0) continue;

                double start = FamilyParametricExpressionEvaluator.TryEvaluate(repeater.Start, values, out double startValue) ? startValue : 0;
                double spacing = FamilyParametricExpressionEvaluator.TryEvaluate(repeater.Spacing, values, out double spacingValue) ? spacingValue : 0;
                string axis = string.IsNullOrWhiteSpace(repeater.Axis) ? "z" : repeater.Axis.Trim().ToLowerInvariant();
                template.IsVisible = false;

                for (int i = 0; i < count; i++)
                {
                    var copy = CloneComponent(template);
                    copy.Id = $"{template.Id}_{i + 1}";
                    copy.IsVisible = true;
                    double offset = start + spacing * i;
                    if (axis == "x") copy.Origin.X = offset;
                    else if (axis == "y") copy.Origin.Y = offset;
                    else copy.Origin.Z = offset;
                    definition.Components.Add(copy);
                }

                diagnostics.Add(new FamilyRigDiagnostic { Severity = "info", Message = $"Materialized {count} repeated components for {repeater.Id}." });
            }

            definition.Diagnostics = (definition.Diagnostics ?? new List<FamilyRigDiagnostic>()).Concat(diagnostics).ToList();
            return diagnostics;
        }

        private static FamilyComponentDefinition CloneComponent(FamilyComponentDefinition source)
        {
            return new FamilyComponentDefinition
            {
                Id = source.Id,
                Geometry = source.Geometry,
                Dimensions = new FamilyComponentDimensions { Width = source.Dimensions.Width, Depth = source.Dimensions.Depth, Height = source.Dimensions.Height },
                Material = source.Material,
                Finish = source.Finish,
                Radius = source.Radius,
                Origin = new FamilyPointDefinition { X = source.Origin.X, Y = source.Origin.Y, Z = source.Origin.Z },
                Rotation = new FamilyRotationDefinition { Z = source.Rotation.Z },
                Role = source.Role,
                IsVoid = source.IsVoid,
                IsVisible = source.IsVisible
            };
        }

        private static bool TryGetNumber(object? raw, out double value)
        {
            return double.TryParse(Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
        }
    }
}
