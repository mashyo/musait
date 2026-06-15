// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Musait.Models;

namespace Musait.Services
{
    public static class FamilyBuildPlanFactory
    {
        public static FamilyBuildPlan Create(FamilyDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            string units = string.IsNullOrWhiteSpace(definition.Units) ? "mm" : definition.Units.Trim();
            var components = (definition.Components ?? new List<FamilyComponentDefinition>())
                .Where(component => component.IsVisible)
                .Select(component => CreateComponent(component, units))
                .ToList();

            return new FamilyBuildPlan
            {
                Category = definition.Category.Trim(),
                Host = definition.Host.Trim(),
                DisplayUnits = units,
                Schema = string.IsNullOrWhiteSpace(definition.Schema) ? "musait.family.v1" : definition.Schema.Trim(),
                Capability = string.IsNullOrWhiteSpace(definition.Capability) ? "static" : definition.Capability.Trim(),
                Archetype = definition.Archetype?.Trim() ?? string.Empty,
                Components = components,
                Parameters = (definition.Parameters ?? new List<FamilyParameterDefinition>()).ToList(),
                Bindings = (definition.Bindings ?? new List<FamilyParameterBindingDefinition>()).ToList(),
                Repeaters = (definition.Repeaters ?? new List<FamilyRepeaterDefinition>()).ToList(),
                Diagnostics = (definition.Diagnostics ?? new List<FamilyRigDiagnostic>()).ToList(),
                Extents = CalculateExtents(components)
            };
        }

        private static FamilyBuildComponent CreateComponent(FamilyComponentDefinition component, string units)
        {
            string geometry = NormalizeGeometry(component.Geometry);
            double widthFeet = RevitUnitConverter.ToFeet(component.Dimensions.Width, units);
            double depthFeet = RevitUnitConverter.ToFeet(component.Dimensions.Depth, units);
            double radiusFeet = component.Radius.HasValue && component.Radius.Value > 0
                ? RevitUnitConverter.ToFeet(component.Radius.Value, units)
                : Math.Min(widthFeet, depthFeet) / 2.0;

            return new FamilyBuildComponent
            {
                Id = component.Id.Trim(),
                Geometry = geometry,
                Material = component.Material.Trim(),
                Finish = component.Finish?.Trim() ?? string.Empty,
                Role = string.IsNullOrWhiteSpace(component.Role) ? "component" : component.Role.Trim(),
                IsVoid = component.IsVoid,
                IsVisible = component.IsVisible,
                WidthFeet = widthFeet,
                DepthFeet = depthFeet,
                HeightFeet = RevitUnitConverter.ToFeet(component.Dimensions.Height, units),
                RadiusFeet = radiusFeet,
                OriginXFeet = RevitUnitConverter.ToFeet(component.Origin?.X ?? 0, units),
                OriginYFeet = RevitUnitConverter.ToFeet(component.Origin?.Y ?? 0, units),
                OriginZFeet = RevitUnitConverter.ToFeet(component.Origin?.Z ?? 0, units),
                RotationZDegrees = component.Rotation?.Z ?? 0
            };
        }

        private static string NormalizeGeometry(string geometry)
        {
            if (string.IsNullOrWhiteSpace(geometry)) return "extrusion";
            return geometry.Trim().ToLowerInvariant() switch
            {
                "box" => "extrusion",
                "cylinder" => "revolution",
                _ => geometry.Trim()
            };
        }

        private static FamilyBuildExtents CalculateExtents(IReadOnlyList<FamilyBuildComponent> components)
        {
            if (components.Count == 0)
            {
                return new FamilyBuildExtents();
            }

            return new FamilyBuildExtents
            {
                MinXFeet = components.Min(component => component.OriginXFeet),
                MinYFeet = components.Min(component => component.OriginYFeet),
                MinZFeet = components.Min(component => component.OriginZFeet),
                MaxXFeet = components.Max(component => component.OriginXFeet + component.WidthFeet),
                MaxYFeet = components.Max(component => component.OriginYFeet + component.DepthFeet),
                MaxZFeet = components.Max(component => component.OriginZFeet + component.HeightFeet)
            };
        }
    }
}
