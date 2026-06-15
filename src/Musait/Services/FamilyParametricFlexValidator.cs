// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Musait.Models;

namespace Musait.Services
{
    public sealed class FamilyParametricFlexValidator
    {
        public IReadOnlyList<FamilyRigDiagnostic> Validate(FamilyDefinition definition, FamilyParametricIntent intent)
        {
            var diagnostics = new List<FamilyRigDiagnostic>();
            if (definition.Components == null || definition.Components.Count == 0) return diagnostics;
            if (definition.Parameters == null || definition.Parameters.Count == 0) return diagnostics;
            if (definition.Bindings == null || definition.Bindings.Count == 0) return diagnostics;

            var baseline = Evaluate(definition, null, 1);
            if (baseline.Components.Count == 0) return diagnostics;

            CheckParameter(definition, baseline, "Width", "width", item => item.Width, diagnostics);
            CheckParameter(definition, baseline, "Depth", "depth", item => item.Depth, diagnostics);
            CheckParameter(definition, baseline, "Height", "height", item => item.Height, diagnostics);

            if (diagnostics.Count == 0 && intent.Kind == FamilyRigKind.Casework)
            {
                diagnostics.Add(new FamilyRigDiagnostic { Severity = "info", Message = "Width, Depth, and Height flexed the preview extents." });
            }

            definition.Diagnostics = definition.Diagnostics.Concat(diagnostics).ToList();
            return diagnostics;
        }

        private static void CheckParameter(
            FamilyDefinition definition,
            EvaluatedFamily baseline,
            string parameterName,
            string axisLabel,
            Func<EvaluatedFamily, double> axis,
            ICollection<FamilyRigDiagnostic> diagnostics)
        {
            var parameter = definition.Parameters.FirstOrDefault(item => string.Equals(item.Name, parameterName, StringComparison.OrdinalIgnoreCase));
            if (parameter == null) return;

            var low = Evaluate(definition, parameterName, 0.85);
            var high = Evaluate(definition, parameterName, 1.15);
            if (low.Components.Any(component => component.Width <= 0 || component.Depth <= 0 || component.Height <= 0) ||
                high.Components.Any(component => component.Width <= 0 || component.Depth <= 0 || component.Height <= 0))
            {
                diagnostics.Add(new FamilyRigDiagnostic { Severity = "error", Parameter = parameterName, Message = $"{parameterName} flex creates zero or negative component dimensions." });
                return;
            }

            double delta = Math.Abs(axis(high) - axis(low));
            double expected = Math.Max(1, axis(baseline) * 0.2);
            if (delta < expected * 0.2)
            {
                diagnostics.Add(new FamilyRigDiagnostic { Severity = "warning", Parameter = parameterName, Message = $"{parameterName} appears inert; aggregate {axisLabel} barely changes during flex." });
            }
        }

        private static EvaluatedFamily Evaluate(FamilyDefinition definition, string? flexParameter, double factor)
        {
            var components = definition.Components.Select(component => new EvaluatedComponent
            {
                Id = component.Id,
                X = component.Origin.X,
                Y = component.Origin.Y,
                Z = component.Origin.Z,
                Width = component.Dimensions.Width,
                Depth = component.Dimensions.Depth,
                Height = component.Dimensions.Height
            }).ToList();

            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in definition.Parameters)
            {
                if (!TryGetNumber(parameter.Default, out double value)) continue;
                values[parameter.Name] = string.Equals(parameter.Name, flexParameter, StringComparison.OrdinalIgnoreCase) ? value * factor : value;
            }

            foreach (var binding in definition.Bindings)
            {
                foreach (var target in binding.Targets)
                {
                    var component = components.FirstOrDefault(item => string.Equals(item.Id, target.Component, StringComparison.OrdinalIgnoreCase));
                    if (component == null) continue;
                    if (!FamilyParametricExpressionEvaluator.TryEvaluate(target.Expression, values, out double value)) continue;
                    SetPath(component, target.Path, value);
                }
            }

            return new EvaluatedFamily(components);
        }

        private static void SetPath(EvaluatedComponent component, string path, double value)
        {
            switch ((path ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "origin.x": component.X = value; break;
                case "origin.y": component.Y = value; break;
                case "origin.z": component.Z = value; break;
                case "dims.w": component.Width = value; break;
                case "dims.d": component.Depth = value; break;
                case "dims.h": component.Height = value; break;
            }
        }

        private static bool TryGetNumber(object? raw, out double value)
        {
            if (raw is null)
            {
                value = 0;
                return false;
            }

            return double.TryParse(Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private sealed class EvaluatedFamily
        {
            public EvaluatedFamily(IReadOnlyList<EvaluatedComponent> components)
            {
                Components = components;
                if (components.Count == 0) return;
                Width = components.Max(item => item.X + item.Width) - components.Min(item => item.X);
                Depth = components.Max(item => item.Y + item.Depth) - components.Min(item => item.Y);
                Height = components.Max(item => item.Z + item.Height) - components.Min(item => item.Z);
            }

            public IReadOnlyList<EvaluatedComponent> Components { get; }
            public double Width { get; }
            public double Depth { get; }
            public double Height { get; }
        }

        private sealed class EvaluatedComponent
        {
            public string Id { get; set; } = string.Empty;
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }
            public double Width { get; set; }
            public double Depth { get; set; }
            public double Height { get; set; }
        }
    }
}
