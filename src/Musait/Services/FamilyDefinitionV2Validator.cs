// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Musait.Models;

namespace Musait.Services
{
    public sealed class FamilyDefinitionV2Validator
    {
        private static readonly HashSet<string> Hosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "non-hosted", "wall-hosted", "floor-hosted", "ceiling-hosted", "face-based"
        };

        private static readonly HashSet<string> Units = new(StringComparer.OrdinalIgnoreCase)
        {
            "mm", "cm", "m", "in", "ft"
        };

        private static readonly HashSet<string> Capabilities = new(StringComparer.OrdinalIgnoreCase)
        {
            "static", "hybrid", "native_parametric"
        };

        private static readonly HashSet<string> ConstraintTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "align", "dimension", "formula", "visibility", "material"
        };

        public FamilyDefinitionValidationResult Validate(FamilyDefinitionV2? definition)
        {
            var errors = new List<string>();
            if (definition == null)
            {
                errors.Add("Family v2 definition is empty.");
                return new FamilyDefinitionValidationResult(errors);
            }

            if (!string.Equals(definition.Schema, FamilyDefinitionV2.SchemaId, StringComparison.OrdinalIgnoreCase))
                errors.Add($"Schema must be {FamilyDefinitionV2.SchemaId}.");
            if (string.IsNullOrWhiteSpace(definition.Name)) errors.Add("Name is required.");
            if (string.IsNullOrWhiteSpace(definition.Category)) errors.Add("Category is required.");
            if (!Hosts.Contains(definition.Host ?? string.Empty)) errors.Add("Host must be non-hosted, wall-hosted, floor-hosted, ceiling-hosted, or face-based.");
            if (!Units.Contains(definition.Units ?? string.Empty)) errors.Add("Units must be mm, cm, m, in, or ft.");
            if (!Capabilities.Contains(definition.Capability ?? string.Empty)) errors.Add("Capability must be static, hybrid, or native_parametric.");
            if (definition.ReferencePlanes == null || definition.ReferencePlanes.Count == 0) errors.Add("reference_planes is required.");
            if (definition.Parameters == null || definition.Parameters.Count == 0) errors.Add("parameters is required.");
            if (definition.Geometry == null || definition.Geometry.Count == 0) errors.Add("geometry is required.");

            ValidateNames(definition, errors);
            ValidateConstraints(definition, errors);
            return new FamilyDefinitionValidationResult(errors);
        }

        private static void ValidateNames(FamilyDefinitionV2 definition, ICollection<string> errors)
        {
            var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in definition.Parameters ?? new List<FamilyParameterDefinitionV2>())
            {
                if (string.IsNullOrWhiteSpace(parameter.Name)) errors.Add("Every v2 parameter requires a name.");
                else if (!parameterNames.Add(parameter.Name.Trim())) errors.Add($"Duplicate parameter name: {parameter.Name}.");
                if (parameter.DefaultValue == null) errors.Add($"Parameter {parameter.Name} requires default_value.");
            }

            var planeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var plane in definition.ReferencePlanes ?? new List<FamilyReferencePlaneDefinition>())
            {
                if (string.IsNullOrWhiteSpace(plane.Name)) errors.Add("Every reference plane requires a name.");
                else if (!planeNames.Add(plane.Name.Trim())) errors.Add($"Duplicate reference plane: {plane.Name}.");
                if (!new[] { "x", "y", "z" }.Contains((plane.Direction ?? string.Empty).Trim().ToLowerInvariant()))
                    errors.Add($"Reference plane {plane.Name} direction must be x, y, or z.");
            }

            var geometryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var geometry in definition.Geometry ?? new List<FamilyGeometryElementDefinition>())
            {
                if (string.IsNullOrWhiteSpace(geometry.Id)) errors.Add("Every geometry element requires an id.");
                else if (!geometryIds.Add(geometry.Id.Trim())) errors.Add($"Duplicate geometry id: {geometry.Id}.");
                if (geometry.Bounds == null || !new[] { "x0", "x1", "y0", "y1", "z0", "z1" }.All(key => geometry.Bounds[key] != null))
                    errors.Add($"Geometry {geometry.Id} requires bounds x0, x1, y0, y1, z0, and z1.");
            }
        }

        private static void ValidateConstraints(FamilyDefinitionV2 definition, ICollection<string> errors)
        {
            var planeNames = new HashSet<string>((definition.ReferencePlanes ?? new()).Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            var geometryIds = new HashSet<string>((definition.Geometry ?? new()).Select(g => g.Id), StringComparer.OrdinalIgnoreCase);
            var parameterNames = new HashSet<string>((definition.Parameters ?? new()).Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var constraint in definition.Constraints ?? new List<FamilyConstraintDefinition>())
            {
                if (!ConstraintTypes.Contains(constraint.Type ?? string.Empty))
                {
                    errors.Add($"Unsupported constraint type: {constraint.Type}.");
                    continue;
                }

                if (string.Equals(constraint.Type, "align", StringComparison.OrdinalIgnoreCase))
                {
                    if (!planeNames.Contains(constraint.ReferencePlane ?? string.Empty)) errors.Add($"Align constraint references unknown plane: {constraint.ReferencePlane}.");
                    string id = (constraint.Target ?? string.Empty).Split('.').FirstOrDefault() ?? string.Empty;
                    if (!geometryIds.Contains(id)) errors.Add($"Align constraint references unknown geometry: {constraint.Target}.");
                }

                if (string.Equals(constraint.Type, "dimension", StringComparison.OrdinalIgnoreCase))
                {
                    if (!planeNames.Contains(constraint.From ?? string.Empty)) errors.Add($"Dimension constraint references unknown from plane: {constraint.From}.");
                    if (!planeNames.Contains(constraint.To ?? string.Empty)) errors.Add($"Dimension constraint references unknown to plane: {constraint.To}.");
                    if (!parameterNames.Contains(constraint.Parameter ?? string.Empty)) errors.Add($"Dimension constraint references unknown parameter: {constraint.Parameter}.");
                }
            }
        }
    }
}
