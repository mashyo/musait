// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Musait.Models;

namespace Musait.Services
{
    public sealed class FamilyDefinitionV1ToV2Adapter
    {
        public FamilyDefinition ToV1(FamilyDefinitionV2 definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            var numericValues = CreateNumericValues(definition);
            var planeOffsets = ResolvePlaneOffsets(definition, numericValues);
            foreach (var pair in planeOffsets)
            {
                AddAliases(numericValues, pair.Key, pair.Value);
            }

            var components = new List<FamilyComponentDefinition>();
            var diagnostics = new List<FamilyRigDiagnostic>(definition.Diagnostics ?? new List<FamilyRigDiagnostic>());
            foreach (var element in definition.Geometry ?? new List<FamilyGeometryElementDefinition>())
            {
                if (!TryCreateComponent(element, definition, numericValues, out var component, out string error))
                {
                    diagnostics.Add(new FamilyRigDiagnostic
                    {
                        Severity = "warning",
                        Message = error,
                        Component = element.Id
                    });
                    continue;
                }

                components.Add(component);
            }

            diagnostics.Add(new FamilyRigDiagnostic
            {
                Severity = "info",
                Message = $"Family v2 schema imported as {NormalizeCapability(definition.Capability)}. Musait Free validates this definition for local preview."
            });

            foreach (string todo in definition.Todo ?? new List<string>())
            {
                diagnostics.Add(new FamilyRigDiagnostic { Severity = "warning", Message = "_todo: " + todo });
            }

            return new FamilyDefinition
            {
                Category = definition.Category,
                Host = definition.Host,
                Units = definition.Units,
                Schema = definition.Schema,
                Capability = definition.Capability,
                Archetype = definition.Archetype,
                Components = components,
                Parameters = (definition.Parameters ?? new List<FamilyParameterDefinitionV2>()).Select(ToV1Parameter).ToList(),
                Bindings = CreateBindings(definition),
                Diagnostics = diagnostics
            };
        }

        private static Dictionary<string, double> CreateNumericValues(FamilyDefinitionV2 definition)
        {
            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in definition.Parameters ?? new List<FamilyParameterDefinitionV2>())
            {
                if (TryReadDouble(parameter.DefaultValue, out double value))
                {
                    AddAliases(values, parameter.Name, value);
                }
            }

            return values;
        }

        private static Dictionary<string, double> ResolvePlaneOffsets(FamilyDefinitionV2 definition, IReadOnlyDictionary<string, double> numericValues)
        {
            var resolved = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var plane in definition.ReferencePlanes ?? new List<FamilyReferencePlaneDefinition>())
            {
                if (TryEvaluateToken(plane.Offset, Merge(numericValues, resolved), out double value))
                {
                    AddAliases(resolved, plane.Name, value);
                }
            }

            for (int pass = 0; pass < 4; pass++)
            {
                foreach (var plane in definition.ReferencePlanes ?? new List<FamilyReferencePlaneDefinition>())
                {
                    if (resolved.ContainsKey(plane.Name)) continue;
                    if (TryEvaluateToken(plane.Offset, Merge(numericValues, resolved), out double value))
                    {
                        AddAliases(resolved, plane.Name, value);
                    }
                }
            }

            return resolved;
        }

        private static bool TryCreateComponent(FamilyGeometryElementDefinition element, FamilyDefinitionV2 definition, IReadOnlyDictionary<string, double> values, out FamilyComponentDefinition component, out string error)
        {
            component = new FamilyComponentDefinition();
            error = string.Empty;
            if (element.Bounds == null)
            {
                error = $"Geometry {element.Id} has no bounds.";
                return false;
            }

            var bounds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (string key in new[] { "x0", "x1", "y0", "y1", "z0", "z1" })
            {
                if (!TryEvaluateToken(element.Bounds[key], values, out double value))
                {
                    error = $"Geometry {element.Id} bound {key} could not be evaluated.";
                    return false;
                }

                bounds[key] = value;
            }

            double width = bounds["x1"] - bounds["x0"];
            double depth = bounds["y1"] - bounds["y0"];
            double height = bounds["z1"] - bounds["z0"];
            if (width <= 0 || depth <= 0 || height <= 0)
            {
                error = $"Geometry {element.Id} resolves to non-positive dimensions.";
                return false;
            }

            component = new FamilyComponentDefinition
            {
                Id = element.Id,
                Geometry = NormalizeGeometry(element.Kind),
                Role = string.IsNullOrWhiteSpace(element.Subcategory) ? "component" : element.Subcategory,
                Material = ResolveMaterialName(element.Material, definition),
                IsVoid = string.Equals(element.SolidOrVoid, "void", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(element.Kind, "void_extrusion", StringComparison.OrdinalIgnoreCase),
                IsVisible = EvaluateVisibility(element.Visibility, values),
                Origin = new FamilyPointDefinition { X = bounds["x0"], Y = bounds["y0"], Z = bounds["z0"] },
                Rotation = new FamilyRotationDefinition { Z = 0 },
                Dimensions = new FamilyComponentDimensions { Width = width, Depth = depth, Height = height }
            };

            return true;
        }

        private static FamilyParameterDefinition ToV1Parameter(FamilyParameterDefinitionV2 parameter)
        {
            return new FamilyParameterDefinition
            {
                Name = parameter.Name,
                Type = parameter.Type.Trim().ToLowerInvariant() switch
                {
                    "length" => "Length",
                    "angle" => "Number",
                    "number" => "Number",
                    "integer" => "Integer",
                    "yes_no" => "YesNo",
                    "material" => "Material",
                    _ => "Text"
                },
                Default = parameter.DefaultValue is JValue value ? value.Value : parameter.DefaultValue?.ToString(),
                Instance = string.Equals(parameter.InstanceOrType, "instance", StringComparison.OrdinalIgnoreCase)
            };
        }

        private static List<FamilyParameterBindingDefinition> CreateBindings(FamilyDefinitionV2 definition)
        {
            var bindings = new List<FamilyParameterBindingDefinition>();
            string driverParameter = FindDriverParameter(definition);
            if (!string.IsNullOrWhiteSpace(driverParameter))
            {
                var targets = CreateGeometryBindingTargets(definition);
                if (targets.Count > 0)
                {
                    bindings.Add(new FamilyParameterBindingDefinition
                    {
                        Parameter = driverParameter,
                        Inferred = true,
                        Targets = targets
                    });
                }
            }

            foreach (var constraint in definition.Constraints ?? new List<FamilyConstraintDefinition>())
            {
                if (!string.Equals(constraint.Type, "material", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string[] parts = (constraint.Target ?? string.Empty).Split('.');
                if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(constraint.Parameter)) continue;
                bindings.Add(new FamilyParameterBindingDefinition
                {
                    Parameter = constraint.Parameter,
                    Inferred = false,
                    Targets = new List<FamilyParameterBindingTargetDefinition>
                    {
                        new()
                        {
                            Component = parts[0],
                            Path = "material",
                            Expression = SanitizeIdentifier(constraint.Parameter)
                        }
                    }
                });
            }

            return bindings;
        }

        private static List<FamilyParameterBindingTargetDefinition> CreateGeometryBindingTargets(FamilyDefinitionV2 definition)
        {
            var targets = new List<FamilyParameterBindingTargetDefinition>();
            var planeExpressions = CreatePlaneExpressionMap(definition);
            foreach (var element in definition.Geometry ?? new List<FamilyGeometryElementDefinition>())
            {
                if (element.Bounds == null || string.IsNullOrWhiteSpace(element.Id)) continue;
                if (!TryGetExpandedBound(element, "x0", planeExpressions, out string x0) ||
                    !TryGetExpandedBound(element, "x1", planeExpressions, out string x1) ||
                    !TryGetExpandedBound(element, "y0", planeExpressions, out string y0) ||
                    !TryGetExpandedBound(element, "y1", planeExpressions, out string y1) ||
                    !TryGetExpandedBound(element, "z0", planeExpressions, out string z0) ||
                    !TryGetExpandedBound(element, "z1", planeExpressions, out string z1))
                {
                    continue;
                }

                AddTarget(targets, element.Id, "origin.x", x0);
                AddTarget(targets, element.Id, "origin.y", y0);
                AddTarget(targets, element.Id, "origin.z", z0);
                AddTarget(targets, element.Id, "dims.w", $"({x1}) - ({x0})");
                AddTarget(targets, element.Id, "dims.d", $"({y1}) - ({y0})");
                AddTarget(targets, element.Id, "dims.h", $"({z1}) - ({z0})");
            }

            return targets;
        }

        private static Dictionary<string, string> CreatePlaneExpressionMap(FamilyDefinitionV2 definition)
        {
            var expressions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var defaultValues = CreateNumericValues(definition);
            var planes = definition.ReferencePlanes ?? new List<FamilyReferencePlaneDefinition>();

            foreach (var plane in planes)
            {
                string expression = NormalizeExpressionToken(plane.Offset);
                if (string.IsNullOrWhiteSpace(expression)) expression = "0";
                expressions[plane.Name] = expression;
                expressions[SanitizeIdentifier(plane.Name)] = expression;
            }

            RepairStandardPlaneExpressions(expressions, defaultValues);
            return expressions;
        }

        private static void RepairStandardPlaneExpressions(IDictionary<string, string> expressions, IReadOnlyDictionary<string, double> defaultValues)
        {
            RepairAxisPair(expressions, defaultValues, "Left", "Right", "Width");
            RepairAxisPair(expressions, defaultValues, "Front", "Back", "Depth");

            if (TryEvaluateExpressionExpression(expressions, "Bottom", defaultValues, out double bottom) &&
                TryEvaluateExpressionExpression(expressions, "Top", defaultValues, out double top) &&
                TryGetValue(defaultValues, "Height", out double height) &&
                NearlyEqual(bottom, 0) &&
                NearlyEqual(top - bottom, height))
            {
                SetExpression(expressions, "Bottom", "0");
                SetExpression(expressions, "Top", "Height");
            }
        }

        private static void RepairAxisPair(IDictionary<string, string> expressions, IReadOnlyDictionary<string, double> defaultValues, string minPlane, string maxPlane, string parameter)
        {
            if (!TryEvaluateExpressionExpression(expressions, minPlane, defaultValues, out double min) ||
                !TryEvaluateExpressionExpression(expressions, maxPlane, defaultValues, out double max) ||
                !TryGetValue(defaultValues, parameter, out double value) ||
                !NearlyEqual(max - min, value))
            {
                return;
            }

            if (NearlyEqual(min, 0))
            {
                SetExpression(expressions, minPlane, "0");
                SetExpression(expressions, maxPlane, parameter);
                return;
            }

            if (NearlyEqual(min, -value / 2.0) && NearlyEqual(max, value / 2.0))
            {
                SetExpression(expressions, minPlane, $"-{parameter} / 2");
                SetExpression(expressions, maxPlane, $"{parameter} / 2");
            }
        }

        private static bool TryEvaluateExpressionExpression(IDictionary<string, string> expressions, string key, IReadOnlyDictionary<string, double> values, out double value)
        {
            value = 0;
            return expressions.TryGetValue(key, out string? expression) &&
                   TryEvaluateExpression(expression, values, out value);
        }

        private static void SetExpression(IDictionary<string, string> expressions, string name, string expression)
        {
            expressions[name] = expression;
            expressions[SanitizeIdentifier(name)] = expression;
        }

        private static bool TryGetExpandedBound(FamilyGeometryElementDefinition element, string key, IReadOnlyDictionary<string, string> planeExpressions, out string expression)
        {
            expression = NormalizeExpressionToken(element.Bounds?[key]);
            if (string.IsNullOrWhiteSpace(expression)) return false;
            expression = ExpandPlaneReferences(expression, planeExpressions);
            return true;
        }

        private static string ExpandPlaneReferences(string expression, IReadOnlyDictionary<string, string> planeExpressions)
        {
            string result = expression;
            foreach (string key in planeExpressions.Keys.OrderByDescending(k => k.Length))
            {
                string replacement = $"({planeExpressions[key]})";
                result = Regex.Replace(
                    result,
                    $@"(?<![\w.]){Regex.Escape(key)}(?![\w.])",
                    replacement,
                    RegexOptions.IgnoreCase);
            }

            return Regex.Replace(result, @"\s*([()+\-*/])\s*", "$1").Trim();
        }

        private static string NormalizeExpressionToken(JToken? token)
        {
            if (token == null) return string.Empty;
            if (token.Type == JTokenType.String) return token.ToString();
            return token is JValue value
                ? Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty
                : token.ToString();
        }

        private static void AddTarget(ICollection<FamilyParameterBindingTargetDefinition> targets, string component, string path, string expression)
        {
            targets.Add(new FamilyParameterBindingTargetDefinition
            {
                Component = component,
                Path = path,
                Expression = expression
            });
        }

        private static string FindDriverParameter(FamilyDefinitionV2 definition)
        {
            var names = (definition.Parameters ?? new List<FamilyParameterDefinitionV2>())
                .Select(parameter => parameter.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
            return names.FirstOrDefault(name => string.Equals(name, "Width", StringComparison.OrdinalIgnoreCase)) ??
                   names.FirstOrDefault(name => string.Equals(name, "Depth", StringComparison.OrdinalIgnoreCase)) ??
                   names.FirstOrDefault(name => string.Equals(name, "Height", StringComparison.OrdinalIgnoreCase)) ??
                   names.FirstOrDefault() ??
                   string.Empty;
        }

        private static bool EvaluateVisibility(string expression, IReadOnlyDictionary<string, double> values)
        {
            if (string.IsNullOrWhiteSpace(expression)) return true;
            var match = Regex.Match(expression, @"^\s*(.+?)\s*(>=|<=|>|<|==)\s*(.+?)\s*$");
            if (match.Success &&
                TryEvaluateExpression(match.Groups[1].Value, values, out double left) &&
                TryEvaluateExpression(match.Groups[3].Value, values, out double right))
            {
                return match.Groups[2].Value switch
                {
                    ">=" => left >= right,
                    "<=" => left <= right,
                    ">" => left > right,
                    "<" => left < right,
                    "==" => Math.Abs(left - right) < 0.000001,
                    _ => true
                };
            }

            return !expression.Equals("false", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryEvaluateToken(JToken? token, IReadOnlyDictionary<string, double> values, out double value)
        {
            value = 0;
            if (token == null) return false;
            if (TryReadDouble(token, out value)) return true;
            return TryEvaluateExpression(token.ToString(), values, out value);
        }

        private static bool TryReadDouble(object? token, out double value)
        {
            value = 0;
            if (token is JValue jValue) token = jValue.Value;
            if (token is double d) { value = d; return true; }
            if (token is float f) { value = f; return true; }
            if (token is int i) { value = i; return true; }
            if (token is long l) { value = l; return true; }
            return double.TryParse(Convert.ToString(token, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryEvaluateExpression(string expression, IReadOnlyDictionary<string, double> values, out double result)
        {
            string normalized = NormalizeExpression(expression, values);
            return FamilyParametricExpressionEvaluator.TryEvaluate(normalized, values, out result);
        }

        private static string NormalizeExpression(string expression, IReadOnlyDictionary<string, double> values)
        {
            string result = expression ?? string.Empty;
            foreach (string key in values.Keys.OrderByDescending(k => k.Length))
            {
                string safe = SanitizeIdentifier(key);
                if (string.Equals(key, safe, StringComparison.Ordinal))
                {
                    continue;
                }

                result = Regex.Replace(
                    result,
                    $@"(?<![A-Za-z0-9_.]){Regex.Escape(key)}(?![A-Za-z0-9_.])",
                    safe,
                    RegexOptions.IgnoreCase);
            }

            return Regex.Replace(result, @"\s*([()+\-*/])\s*", "$1").Trim();
        }

        private static bool TryGetValue(IReadOnlyDictionary<string, double> values, string name, out double value)
        {
            return values.TryGetValue(name, out value) ||
                   values.TryGetValue(SanitizeIdentifier(name), out value);
        }

        private static bool NearlyEqual(double left, double right)
        {
            double tolerance = Math.Max(0.001, Math.Max(Math.Abs(left), Math.Abs(right)) * 0.000001);
            return Math.Abs(left - right) <= tolerance;
        }

        private static Dictionary<string, double> Merge(IReadOnlyDictionary<string, double> first, IReadOnlyDictionary<string, double> second)
        {
            var merged = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in first) merged[pair.Key] = pair.Value;
            foreach (var pair in second) merged[pair.Key] = pair.Value;
            return merged;
        }

        private static void AddAliases(IDictionary<string, double> values, string name, double value)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            values[name.Trim()] = value;
            values[SanitizeIdentifier(name)] = value;
        }

        private static string SanitizeIdentifier(string name)
        {
            string safe = Regex.Replace(name.Trim(), @"[^\w]+", "_");
            safe = Regex.Replace(safe, "_+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(safe) ? "Value" : safe;
        }

        private static string NormalizeGeometry(string kind)
        {
            return kind.Trim().ToLowerInvariant() switch
            {
                "void_extrusion" => "extrusion",
                "sweep" => "extrusion",
                "revolution" => "revolution",
                _ => "extrusion"
            };
        }

        private static string ResolveMaterialName(string material, FamilyDefinitionV2 definition)
        {
            if (!string.IsNullOrWhiteSpace(material)) return material.Trim();
            return definition.Materials?.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.Name))?.Name ?? "Default";
        }

        private static string NormalizeCapability(string capability)
        {
            return capability switch
            {
                "native_parametric" => "Native parametric preview request",
                "hybrid" => "Hybrid preview request",
                _ => "Static preview"
            };
        }
    }
}
