// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Musait.Models;

namespace Musait.Services
{
    public static class FamilyDefinitionValidator
    {
        private static readonly HashSet<string> SupportedCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "Casework",
            "Electrical Fixtures",
            "Entourage",
            "Furniture",
            "Furniture Systems",
            "Specialty Equipment",
            "Generic Model",
            "Lighting Fixtures",
            "Mechanical Equipment",
            "Planting",
            "Plumbing Fixtures"
        };

        private const string SupportedCategoryText =
            "Casework, Electrical Fixtures, Entourage, Furniture, Furniture Systems, Generic Model, Lighting Fixtures, Mechanical Equipment, Planting, Plumbing Fixtures, or Specialty Equipment";

        private static readonly HashSet<string> SupportedUnits = new(StringComparer.OrdinalIgnoreCase)
        {
            "mm",
            "cm",
            "m",
            "in",
            "ft"
        };

        private static readonly HashSet<string> SupportedHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "non-hosted",
            "wall-hosted",
            "floor-hosted",
            "ceiling-hosted",
            "face-based"
        };

        private static readonly HashSet<string> SupportedGeometry = new(StringComparer.OrdinalIgnoreCase)
        {
            "box",
            "extrusion",
            "cylinder",
            "revolution"
        };

        private static readonly HashSet<string> SupportedParameterTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Length",
            "Number",
            "Integer",
            "Text",
            "YesNo",
            "Material"
        };

        public static FamilyDefinitionValidationResult Validate(FamilyDefinition? definition)
        {
            var errors = new List<string>();
            if (definition == null)
            {
                errors.Add("Family definition is empty.");
                return new FamilyDefinitionValidationResult(errors);
            }

            if (string.IsNullOrWhiteSpace(definition.Category))
            {
                errors.Add("Category is required.");
            }
            else if (!SupportedCategories.Contains(definition.Category.Trim()))
            {
                errors.Add($"Category must be {SupportedCategoryText}.");
            }

            if (!SupportedUnits.Contains((definition.Units ?? string.Empty).Trim()))
            {
                errors.Add("Units must be mm, cm, m, in, or ft.");
            }

            if (!SupportedHosts.Contains((definition.Host ?? string.Empty).Trim()))
            {
                errors.Add("Host must be non-hosted, wall-hosted, floor-hosted, ceiling-hosted, or face-based.");
            }

            if (definition.Components == null || definition.Components.Count == 0)
            {
                errors.Add("At least one component is required.");
            }
            else
            {
                ValidateComponents(definition.Components, errors);
                ValidateAggregateExtents(definition, errors);
            }

            if (definition.Parameters != null)
            {
                ValidateParameters(definition.Parameters, errors);
            }

            if (definition.Bindings != null)
            {
                ValidateBindings(definition, errors);
            }

            return new FamilyDefinitionValidationResult(errors);
        }

        private static void ValidateComponents(IEnumerable<FamilyComponentDefinition> components, ICollection<string> errors)
        {
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var occupiedSolidBoxes = new Dictionary<(double X, double Y, double Z, double W, double D, double H), string>();
            foreach (var component in components)
            {
                string id = component.Id?.Trim() ?? string.Empty;
                if (id.Length == 0)
                {
                    errors.Add("Every component requires an id.");
                }
                else if (!seenIds.Add(id))
                {
                    errors.Add($"Duplicate component id: {id}.");
                }

                string geometry = (component.Geometry ?? string.Empty).Trim();
                if (!SupportedGeometry.Contains(geometry))
                {
                    errors.Add($"Component {id} uses unsupported geometry. Use extrusion, box, cylinder, or revolution.");
                }

                var dims = component.Dimensions;
                if (dims == null || dims.Width <= 0 || dims.Depth <= 0 || dims.Height <= 0)
                {
                    errors.Add($"Component {id} requires positive w, d, and h dimensions.");
                }
                else if (IsCylinderGeometry(geometry))
                {
                    if (component.Radius.HasValue && component.Radius.Value <= 0)
                    {
                        errors.Add($"Component {id} cylinder radius must be positive.");
                    }

                    double diameterDifference = Math.Abs(dims.Width - dims.Depth);
                    double largestDiameter = Math.Max(Math.Abs(dims.Width), Math.Abs(dims.Depth));
                    if (largestDiameter > 0 && diameterDifference / largestDiameter > 0.05)
                    {
                        errors.Add($"Component {id} cylinder dims.w and dims.d must describe the same diameter.");
                    }
                }

                if (string.IsNullOrWhiteSpace(component.Material))
                {
                    errors.Add($"Component {id} requires a material name.");
                }

                if (component.IsVisible && !component.IsVoid && dims != null && dims.Width > 0 && dims.Depth > 0 && dims.Height > 0)
                {
                    var boundsKey = CreateBoundsKey(component);
                    if (occupiedSolidBoxes.TryGetValue(boundsKey, out string? existingId))
                    {
                        errors.Add($"Components {existingId} and {id} occupy the same origin and dimensions.");
                    }
                    else
                    {
                        occupiedSolidBoxes[boundsKey] = id.Length == 0 ? "<unnamed>" : id;
                    }
                }
            }
        }

        private static void ValidateParameters(IEnumerable<FamilyParameterDefinition> parameters, ICollection<string> errors)
        {
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in parameters)
            {
                string name = parameter.Name?.Trim() ?? string.Empty;
                if (name.Length == 0)
                {
                    errors.Add("Every parameter requires a name.");
                }
                else if (!seenNames.Add(name))
                {
                    errors.Add($"Duplicate parameter name: {name}.");
                }
                else if (!IsRevitSafeIdentifier(name))
                {
                    errors.Add($"Parameter {name} must use letters, numbers, spaces, underscores, hyphens, or periods.");
                }

                if (!SupportedParameterTypes.Contains((parameter.Type ?? string.Empty).Trim()))
                {
                    errors.Add($"Parameter {name} has an unsupported type.");
                }
            }
        }

        private static void ValidateAggregateExtents(FamilyDefinition definition, ICollection<string> errors)
        {
            var components = (definition.Components ?? new List<FamilyComponentDefinition>())
                .Where(component => component.IsVisible)
                .ToList();
            if (components.Count == 0) return;

            double maxX = components.Max(component => (component.Origin?.X ?? 0) + (component.Dimensions?.Width ?? 0));
            double maxY = components.Max(component => (component.Origin?.Y ?? 0) + (component.Dimensions?.Depth ?? 0));
            double maxZ = components.Max(component => (component.Origin?.Z ?? 0) + (component.Dimensions?.Height ?? 0));
            double minX = components.Min(component => component.Origin?.X ?? 0);
            double minY = components.Min(component => component.Origin?.Y ?? 0);
            double minZ = components.Min(component => component.Origin?.Z ?? 0);

            if (maxX - minX <= 0 || maxY - minY <= 0 || maxZ - minZ <= 0)
            {
                errors.Add("Aggregate family extents must be nonzero.");
            }

            double longest = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
            if (longest > 100000)
            {
                errors.Add("Aggregate family extents are too large for the v1 generator.");
            }
        }

        private static void ValidateBindings(FamilyDefinition definition, ICollection<string> errors)
        {
            var parameterNames = new HashSet<string>(
                (definition.Parameters ?? new List<FamilyParameterDefinition>()).Select(parameter => parameter.Name ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);
            var componentIds = new HashSet<string>(
                (definition.Components ?? new List<FamilyComponentDefinition>()).Select(component => component.Id ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);
            var allowedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "dims.w",
                "dims.d",
                "dims.h",
                "origin.x",
                "origin.y",
                "origin.z",
                "rotation.z",
                "material"
            };

            foreach (var binding in definition.Bindings)
            {
                string parameter = binding.Parameter?.Trim() ?? string.Empty;
                if (parameter.Length == 0)
                {
                    errors.Add("Every binding requires a parameter name.");
                }
                else if (!parameterNames.Contains(parameter))
                {
                    errors.Add($"Binding references unknown parameter: {parameter}.");
                }

                if (binding.Targets == null || binding.Targets.Count == 0)
                {
                    errors.Add($"Binding {parameter} requires at least one target.");
                    continue;
                }

                foreach (var target in binding.Targets)
                {
                    string component = target.Component?.Trim() ?? string.Empty;
                    string path = target.Path?.Trim() ?? string.Empty;
                    string expression = target.Expression?.Trim() ?? string.Empty;
                    if (!componentIds.Contains(component))
                    {
                        errors.Add($"Binding {parameter} references unknown component: {component}.");
                    }

                    if (!allowedPaths.Contains(path))
                    {
                        errors.Add($"Binding {parameter} target path {path} is unsupported.");
                    }

                    if (expression.Length == 0 || !Regex.IsMatch(expression, @"^[\w .+\-*/()]+$"))
                    {
                        errors.Add($"Binding {parameter} has an invalid expression.");
                    }
                }
            }
        }

        private static bool IsRevitSafeIdentifier(string name)
        {
            return Regex.IsMatch(name, @"^[\w .-]+$");
        }

        private static bool IsCylinderGeometry(string geometry)
        {
            return string.Equals(geometry, "cylinder", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(geometry, "revolution", StringComparison.OrdinalIgnoreCase);
        }

        private static (double X, double Y, double Z, double W, double D, double H) CreateBoundsKey(FamilyComponentDefinition component)
        {
            var origin = component.Origin;
            var dims = component.Dimensions;
            return (
                RoundForGeometryKey(origin?.X ?? 0),
                RoundForGeometryKey(origin?.Y ?? 0),
                RoundForGeometryKey(origin?.Z ?? 0),
                RoundForGeometryKey(dims?.Width ?? 0),
                RoundForGeometryKey(dims?.Depth ?? 0),
                RoundForGeometryKey(dims?.Height ?? 0));
        }

        private static double RoundForGeometryKey(double value)
        {
            return Math.Round(value, 6, MidpointRounding.AwayFromZero);
        }
    }
}
