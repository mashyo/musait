// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Musait.Models;

namespace Musait.Services
{
    public static class CaseworkWardrobeRecipe
    {
        public static FamilyDefinitionV2 Materialize(FamilyDefinitionV2 seed)
        {
            var result = seed;
            result.Schema = FamilyDefinitionV2.SchemaId;
            result.Name = string.IsNullOrWhiteSpace(result.Name) ? "Wardrobe" : result.Name;
            result.Category = string.IsNullOrWhiteSpace(result.Category) ? "Casework" : result.Category;
            result.Host = string.IsNullOrWhiteSpace(result.Host) ? "non-hosted" : result.Host;
            result.Units = string.IsNullOrWhiteSpace(result.Units) ? "mm" : result.Units;
            result.Capability = string.IsNullOrWhiteSpace(result.Capability) ? "hybrid" : result.Capability;
            result.Archetype = "casework.wardrobe";

            EnsureParameters(result);
            EnsureReferencePlanes(result);
            EnsureGeometry(result);
            EnsureConstraints(result);
            return result;
        }

        private static void EnsureParameters(FamilyDefinitionV2 definition)
        {
            var parameters = definition.Parameters ??= new List<FamilyParameterDefinitionV2>();
            AddParameter(parameters, "Width", "length", 1800, "Dimensions");
            AddParameter(parameters, "Depth", "length", 600, "Dimensions");
            AddParameter(parameters, "Height", "length", 2200, "Dimensions");
            AddParameter(parameters, "Panel Thickness", "length", 18, "Dimensions");
            AddParameter(parameters, "Back Thickness", "length", 6, "Dimensions");
            AddParameter(parameters, "Door Gap", "length", 3, "Dimensions");
            AddParameter(parameters, "Shelf Count", "integer", 4, "Data");
            AddParameter(parameters, "Door Count", "integer", 2, "Data");
            AddParameter(parameters, "Body Material", "material", "Body Material", "Materials and Finishes");
            AddParameter(parameters, "Door Material", "material", "Door Material", "Materials and Finishes");
        }

        private static void EnsureReferencePlanes(FamilyDefinitionV2 definition)
        {
            var planes = definition.ReferencePlanes ??= new List<FamilyReferencePlaneDefinition>();
            AddPlane(planes, "Left", "x", 0, "Strong");
            AddPlane(planes, "Right", "x", "Width", "Strong");
            AddPlane(planes, "Front", "y", 0, "Strong");
            AddPlane(planes, "Back", "y", "Depth", "Strong");
            AddPlane(planes, "Bottom", "z", 0, "Strong");
            AddPlane(planes, "Top", "z", "Height", "Strong");
            AddPlane(planes, "Center Left/Right", "x", "Width / 2", "Weak");
            AddPlane(planes, "Center Front/Back", "y", "Depth / 2", "Weak");
            AddPlane(planes, "Interior Left", "x", "Panel_Thickness", "Weak");
            AddPlane(planes, "Interior Right", "x", "Width - Panel_Thickness", "Weak");
        }

        private static void EnsureGeometry(FamilyDefinitionV2 definition)
        {
            var geometry = definition.Geometry ??= new List<FamilyGeometryElementDefinition>();
            AddBox(geometry, "left_side_panel", "Carcass", "Body Material", "Left", "Left + Panel_Thickness", "Front", "Back", "Bottom", "Top");
            AddBox(geometry, "right_side_panel", "Carcass", "Body Material", "Right - Panel_Thickness", "Right", "Front", "Back", "Bottom", "Top");
            AddBox(geometry, "bottom_panel", "Carcass", "Body Material", "Left", "Right", "Front", "Back", "Bottom", "Bottom + Panel_Thickness");
            AddBox(geometry, "top_panel", "Carcass", "Body Material", "Left", "Right", "Front", "Back", "Top - Panel_Thickness", "Top");
            AddBox(geometry, "back_panel", "Back", "Body Material", "Left", "Right", "Back - Back_Thickness", "Back", "Bottom", "Top");
            AddBox(geometry, "door_left", "Doors", "Door Material", "Left + Door_Gap", "Center_Left_Right - Door_Gap / 2", "Front - Panel_Thickness", "Front", "Bottom + Door_Gap", "Top - Door_Gap");
            AddBox(geometry, "door_right", "Doors", "Door Material", "Center_Left_Right + Door_Gap / 2", "Right - Door_Gap", "Front - Panel_Thickness", "Front", "Bottom + Door_Gap", "Top - Door_Gap");

            for (int i = 1; i <= 10; i++)
            {
                string id = $"shelf_{i:00}";
                string z0 = $"Panel_Thickness + ((Height - (2 * Panel_Thickness)) / ({i + 1})) * {i}";
                string z1 = $"{z0} + Panel_Thickness";
                AddBox(geometry, id, "Shelves", "Body Material", "Interior_Left", "Interior_Right", "Front", "Back - Back_Thickness", z0, z1, $"Shelf_Count >= {i}");
            }
        }

        private static void EnsureConstraints(FamilyDefinitionV2 definition)
        {
            var constraints = definition.Constraints ??= new List<FamilyConstraintDefinition>();
            AddDimension(constraints, "Overall Width", "Left", "Right", "Width");
            AddDimension(constraints, "Overall Depth", "Front", "Back", "Depth");
            AddDimension(constraints, "Overall Height", "Bottom", "Top", "Height");
            foreach (var geometry in definition.Geometry ?? Enumerable.Empty<FamilyGeometryElementDefinition>())
            {
                AddAlignsForBounds(constraints, geometry);
            }
        }

        private static void AddParameter(ICollection<FamilyParameterDefinitionV2> parameters, string name, string type, object defaultValue, string group)
        {
            if (parameters.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) return;
            parameters.Add(new FamilyParameterDefinitionV2
            {
                Name = name,
                Type = type,
                InstanceOrType = "type",
                DefaultValue = JToken.FromObject(defaultValue),
                Group = group
            });
        }

        private static void AddPlane(ICollection<FamilyReferencePlaneDefinition> planes, string name, string direction, object offset, string isReference)
        {
            if (planes.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) return;
            planes.Add(new FamilyReferencePlaneDefinition
            {
                Name = name,
                Direction = direction,
                Offset = JToken.FromObject(offset),
                IsReference = isReference
            });
        }

        private static void AddBox(ICollection<FamilyGeometryElementDefinition> geometry, string id, string subcategory, string material, string x0, string x1, string y0, string y1, string z0, string z1, string visibility = "")
        {
            if (geometry.Any(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase))) return;
            geometry.Add(new FamilyGeometryElementDefinition
            {
                Id = id,
                Kind = "extrusion",
                SolidOrVoid = "solid",
                Subcategory = subcategory,
                Material = material,
                Bounds = new JObject
                {
                    ["x0"] = x0,
                    ["x1"] = x1,
                    ["y0"] = y0,
                    ["y1"] = y1,
                    ["z0"] = z0,
                    ["z1"] = z1
                },
                Visibility = visibility
            });
        }

        private static void AddDimension(ICollection<FamilyConstraintDefinition> constraints, string name, string from, string to, string parameter)
        {
            if (constraints.Any(c => string.Equals(c.Type, "dimension", StringComparison.OrdinalIgnoreCase) &&
                                     string.Equals(c.Parameter, parameter, StringComparison.OrdinalIgnoreCase))) return;
            constraints.Add(new FamilyConstraintDefinition { Type = "dimension", Name = name, From = from, To = to, Parameter = parameter });
        }

        private static void AddAlignsForBounds(ICollection<FamilyConstraintDefinition> constraints, FamilyGeometryElementDefinition geometry)
        {
            foreach (var pair in new[] { ("x0", "Left"), ("x1", "Right"), ("y0", "Front"), ("y1", "Back"), ("z0", "Bottom"), ("z1", "Top") })
            {
                string? value = geometry.Bounds[pair.Item1]?.ToString();
                if (!string.Equals(value, pair.Item2, StringComparison.OrdinalIgnoreCase)) continue;
                string target = $"{geometry.Id}.{pair.Item1}";
                if (constraints.Any(c => string.Equals(c.Type, "align", StringComparison.OrdinalIgnoreCase) && string.Equals(c.Target, target, StringComparison.OrdinalIgnoreCase))) continue;
                constraints.Add(new FamilyConstraintDefinition { Type = "align", Target = target, ReferencePlane = pair.Item2, Locked = true });
            }
        }
    }
}
