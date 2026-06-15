// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Musait.Models;

namespace Musait.Services
{
    public sealed class FamilyParametricIntentSynthesizer
    {
        private static readonly string[] CaseworkTerms =
        {
            "wardrobe", "cabinet", "carcass", "side", "panel", "top", "bottom", "shelf", "door", "drawer", "back", "leg", "toe", "kick"
        };

        public FamilyParametricIntent Synthesize(FamilyDefinition definition)
        {
            var intent = new FamilyParametricIntent();
            if (definition.Components == null || definition.Components.Count == 0) return intent;

            intent.Kind = DetectKind(definition);
            if (intent.Kind != FamilyRigKind.Casework) return intent;

            var visible = definition.Components.Where(component => component.IsVisible && !component.IsVoid).ToList();
            if (visible.Count == 0) return intent;

            var extents = GetExtents(visible);
            EnsureParameter(definition, intent, "Width", extents.Width);
            EnsureParameter(definition, intent, "Depth", extents.Depth);
            EnsureParameter(definition, intent, "Height", extents.Height);
            double panelThickness = GuessPanelThickness(visible, extents);
            EnsureParameter(definition, intent, "Panel_Thickness", panelThickness);
            EnsureParameter(definition, intent, "Back_Thickness", Math.Max(1, Math.Min(panelThickness, extents.Depth * 0.08)));
            EnsureParameter(definition, intent, "Door_Gap", Math.Max(1, panelThickness * 0.15));
            EnsureParameter(definition, intent, "Base_Height", Math.Max(0, extents.MinZ));
            if (visible.Any(component => HasText(component, "leg"))) EnsureParameter(definition, intent, "Leg_Height", Math.Max(panelThickness, extents.Height * 0.12));

            var additions = new List<FamilyRigBinding>();
            foreach (var component in visible)
            {
                string role = RoleOf(component, extents);
                AddCaseworkRules(component, role, extents, additions);
            }

            MergeBindings(definition, additions, intent);
            intent.ReferencePlanes.AddRange(new[]
            {
                new FamilyRigPlane { Id = "Left", Axis = "x", Position = "0" },
                new FamilyRigPlane { Id = "Right", Axis = "x", Position = "Width" },
                new FamilyRigPlane { Id = "Front", Axis = "y", Position = "0" },
                new FamilyRigPlane { Id = "Back", Axis = "y", Position = "Depth" },
                new FamilyRigPlane { Id = "Bottom", Axis = "z", Position = "0" },
                new FamilyRigPlane { Id = "Top", Axis = "z", Position = "Height" }
            });

            intent.Diagnostics.Add(new FamilyRigDiagnostic { Severity = "info", Message = "Casework parametric bindings were checked and repaired where needed." });
            definition.Diagnostics = intent.Diagnostics;
            return intent;
        }

        private static FamilyRigKind DetectKind(FamilyDefinition definition)
        {
            bool category = string.Equals(definition.Category, "Casework", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(definition.Category, "Furniture", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(definition.Category, "Generic Model", StringComparison.OrdinalIgnoreCase);
            if (!category) return FamilyRigKind.Unknown;

            int hits = definition.Components.Count(component => CaseworkTerms.Any(term => HasText(component, term)));
            return hits >= Math.Max(1, definition.Components.Count / 4) ? FamilyRigKind.Casework : FamilyRigKind.Unknown;
        }

        private static void AddCaseworkRules(FamilyComponentDefinition component, string role, Extents extents, ICollection<FamilyRigBinding> bindings)
        {
            string id = component.Id;
            void Add(string path, string expression) => bindings.Add(new FamilyRigBinding { Parameter = RootParameter(expression), Component = id, Path = path, Expression = expression, Inferred = true });

            switch (role)
            {
                case "left_side":
                    Add("origin.x", "0"); Add("dims.w", "Panel_Thickness"); Add("dims.d", "Depth"); Add("dims.h", "Height"); break;
                case "right_side":
                    Add("origin.x", "Width - Panel_Thickness"); Add("dims.w", "Panel_Thickness"); Add("dims.d", "Depth"); Add("dims.h", "Height"); break;
                case "top":
                    Add("origin.z", "Height - Panel_Thickness"); Add("dims.w", "Width"); Add("dims.d", "Depth"); Add("dims.h", "Panel_Thickness"); break;
                case "bottom":
                    Add("origin.z", "Base_Height"); Add("dims.w", "Width"); Add("dims.d", "Depth"); Add("dims.h", "Panel_Thickness"); break;
                case "back":
                    Add("origin.y", "Depth - Back_Thickness"); Add("dims.w", "Width"); Add("dims.d", "Back_Thickness"); Add("dims.h", "Height"); break;
                case "shelf":
                    Add("origin.x", "Panel_Thickness"); Add("dims.w", "Width - 2 * Panel_Thickness"); Add("dims.d", "Depth - Back_Thickness");
                    Add("origin.z", Format(component.Origin.Z / Math.Max(1, extents.Height)) + " * Height"); break;
                case "door":
                    bool rightDoor = component.Origin.X > extents.MinX + extents.Width / 2;
                    Add("origin.x", rightDoor ? "Width / 2 + Door_Gap / 2" : "Door_Gap / 2");
                    Add("origin.y", "0"); Add("origin.z", "Base_Height"); Add("dims.w", "Width / 2 - Door_Gap"); Add("dims.h", "Height - Base_Height"); break;
                case "leg":
                    bool right = component.Origin.X > extents.MinX + extents.Width / 2;
                    bool back = component.Origin.Y > extents.MinY + extents.Depth / 2;
                    Add("origin.x", right ? "Width - Panel_Thickness" : "0");
                    Add("origin.y", back ? "Depth - Panel_Thickness" : "0");
                    Add("dims.h", "Leg_Height"); break;
            }
        }

        private static void MergeBindings(FamilyDefinition definition, IEnumerable<FamilyRigBinding> additions, FamilyParametricIntent intent)
        {
            definition.Bindings ??= new List<FamilyParameterBindingDefinition>();
            var existing = new HashSet<string>(definition.Bindings.SelectMany(binding => binding.Targets.Select(target => Key(target.Component, target.Path))), StringComparer.OrdinalIgnoreCase);
            foreach (var group in additions.Where(addition => !existing.Contains(Key(addition.Component, addition.Path))).GroupBy(addition => addition.Parameter))
            {
                var binding = new FamilyParameterBindingDefinition { Parameter = group.Key, Inferred = true };
                foreach (var item in group)
                {
                    binding.Targets.Add(new FamilyParameterBindingTargetDefinition { Component = item.Component, Path = item.Path, Expression = item.Expression });
                    intent.Bindings.Add(item);
                    existing.Add(Key(item.Component, item.Path));
                }

                if (binding.Targets.Count > 0) definition.Bindings.Add(binding);
            }
        }

        private static void EnsureParameter(FamilyDefinition definition, FamilyParametricIntent intent, string name, double defaultValue)
        {
            definition.Parameters ??= new List<FamilyParameterDefinition>();
            var existing = definition.Parameters.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new FamilyParameterDefinition { Name = name, Type = "Length", Default = Math.Round(defaultValue, 3) };
                definition.Parameters.Add(existing);
                intent.Diagnostics.Add(new FamilyRigDiagnostic { Severity = "info", Message = $"Added inferred {name} parameter.", Parameter = name });
            }

            intent.Parameters.Add(new FamilyRigParameter { Name = existing.Name, Type = existing.Type, Default = existing.Default, Inferred = true });
        }

        private static string RoleOf(FamilyComponentDefinition component, Extents extents)
        {
            if (HasText(component, "left")) return "left_side";
            if (HasText(component, "right")) return "right_side";
            if (HasText(component, "top")) return "top";
            if (HasText(component, "bottom") || HasText(component, "base")) return "bottom";
            if (HasText(component, "back")) return "back";
            if (HasText(component, "shelf")) return "shelf";
            if (HasText(component, "door")) return "door";
            if (HasText(component, "leg") || HasText(component, "toe") || HasText(component, "kick")) return "leg";

            double x0 = component.Origin.X - extents.MinX;
            double x1 = component.Origin.X + component.Dimensions.Width - extents.MinX;
            double y1 = component.Origin.Y + component.Dimensions.Depth - extents.MinY;
            double z1 = component.Origin.Z + component.Dimensions.Height - extents.MinZ;
            if (component.Dimensions.Width <= extents.Width * 0.12 && x0 <= extents.Width * 0.12) return "left_side";
            if (component.Dimensions.Width <= extents.Width * 0.12 && x1 >= extents.Width * 0.88) return "right_side";
            if (component.Dimensions.Height <= extents.Height * 0.12 && z1 >= extents.Height * 0.88) return "top";
            if (component.Dimensions.Height <= extents.Height * 0.12 && component.Origin.Z <= extents.MinZ + extents.Height * 0.18) return "bottom";
            if (component.Dimensions.Depth <= extents.Depth * 0.12 && y1 >= extents.Depth * 0.88) return "back";
            return "component";
        }

        private static bool HasText(FamilyComponentDefinition component, string term)
        {
            string text = $"{component.Id} {component.Role} {component.Material} {component.Finish}";
            return text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Extents GetExtents(IEnumerable<FamilyComponentDefinition> components) => new()
        {
            MinX = components.Min(component => component.Origin.X),
            MinY = components.Min(component => component.Origin.Y),
            MinZ = components.Min(component => component.Origin.Z),
            MaxX = components.Max(component => component.Origin.X + component.Dimensions.Width),
            MaxY = components.Max(component => component.Origin.Y + component.Dimensions.Depth),
            MaxZ = components.Max(component => component.Origin.Z + component.Dimensions.Height)
        };

        private static double GuessPanelThickness(IEnumerable<FamilyComponentDefinition> components, Extents extents)
        {
            var candidates = components.SelectMany(component => new[] { component.Dimensions.Width, component.Dimensions.Depth, component.Dimensions.Height })
                .Where(value => value > 0 && value <= Math.Max(1, Math.Min(extents.Width, Math.Min(extents.Depth, extents.Height)) * 0.2))
                .OrderBy(value => value)
                .ToList();
            return candidates.Count > 0 ? candidates[candidates.Count / 2] : Math.Max(18, extents.Width * 0.03);
        }

        private static string RootParameter(string expression) =>
            new[] { "Width", "Depth", "Height", "Panel_Thickness", "Back_Thickness", "Door_Gap", "Base_Height", "Leg_Height" }
                .FirstOrDefault(name => expression.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) ?? "Width";

        private static string Key(string component, string path) => component + "|" + path;
        private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        private sealed class Extents
        {
            public double MinX { get; set; }
            public double MinY { get; set; }
            public double MinZ { get; set; }
            public double MaxX { get; set; }
            public double MaxY { get; set; }
            public double MaxZ { get; set; }
            public double Width => MaxX - MinX;
            public double Depth => MaxY - MinY;
            public double Height => MaxZ - MinZ;
        }
    }
}
