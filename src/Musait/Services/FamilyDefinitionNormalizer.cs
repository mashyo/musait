// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Musait.Models;

namespace Musait.Services
{
    public sealed class FamilyDefinitionNormalizer
    {
        public bool TryNormalize(string text, out string normalizedJson, out FamilyDefinition definition, out string feedback)
        {
            normalizedJson = string.Empty;
            definition = null!;
            feedback = string.Empty;

            var candidates = ExtractJsonObjectCandidates(text);
            if (candidates.Count == 0)
            {
                feedback = "No JSON object found. Copy the model response that starts with { and ends with }, or keep it visible and use Acquire latest.";
                return false;
            }

            var orderedCandidates = candidates
                .Select((candidate, index) => new { Candidate = candidate, Index = index, Score = ScoreFamilyJsonCandidate(candidate) })
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Index)
                .ToList();

            var errors = new List<string>();
            foreach (var item in orderedCandidates)
            {
                if (TryNormalizeCandidate(item.Candidate, out normalizedJson, out definition, out feedback))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(feedback) && !errors.Contains(feedback))
                {
                    errors.Add(feedback);
                }
            }

            feedback = errors.Count > 0 ? errors[0] : "No candidate matched the Family JSON contract.";
            return false;
        }

        public bool TryNormalizeCandidate(string candidate, out string normalizedJson, out FamilyDefinition definition, out string feedback)
        {
            normalizedJson = string.Empty;
            definition = null!;
            feedback = string.Empty;

            try
            {
                var token = JToken.Parse(candidate);
                if (token is not JObject obj)
                {
                    feedback = "Family JSON must be a root object.";
                    return false;
                }

                if (FamilyDefinitionV2Normalizer.IsV2(obj))
                {
                    var v2Definition = new FamilyDefinitionV2Normalizer().Normalize(obj);
                    var v2Validation = new FamilyDefinitionV2Validator().Validate(v2Definition);
                    if (!v2Validation.IsValid)
                    {
                        feedback = string.Join(" ", v2Validation.Errors.Take(4));
                        return false;
                    }

                    definition = new FamilyDefinitionV1ToV2Adapter().ToV1(v2Definition);
                    var adaptedValidation = FamilyDefinitionValidator.Validate(definition);
                    if (!adaptedValidation.IsValid)
                    {
                        feedback = string.Join(" ", adaptedValidation.Errors.Take(4));
                        definition = null!;
                        return false;
                    }

                    normalizedJson = JObject.FromObject(v2Definition).ToString(Formatting.Indented);
                    return true;
                }

                JObject normalizedObject = NormalizeObject(obj);
                definition = normalizedObject.ToObject<FamilyDefinition>() ?? new FamilyDefinition();
                FamilyRepeaterMaterializer.Materialize(definition);
                var validation = FamilyDefinitionValidator.Validate(definition);
                if (!validation.IsValid)
                {
                    feedback = string.Join(" ", validation.Errors.Take(4));
                    definition = null!;
                    return false;
                }

                var intent = new FamilyParametricIntentSynthesizer().Synthesize(definition);
                var flexDiagnostics = new FamilyParametricFlexValidator().Validate(definition, intent);
                validation = FamilyDefinitionValidator.Validate(definition);
                if (!validation.IsValid)
                {
                    feedback = string.Join(" ", validation.Errors.Take(4));
                    definition = null!;
                    return false;
                }

                normalizedJson = JObject.FromObject(definition).ToString(Formatting.Indented);
                if (flexDiagnostics.Any(diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase)))
                {
                    feedback = string.Join(" ", flexDiagnostics.Where(diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase)).Select(diagnostic => diagnostic.Message).Take(2));
                    definition = null!;
                    normalizedJson = string.Empty;
                    return false;
                }

                return true;
            }
            catch (JsonReaderException ex)
            {
                feedback = "JSON syntax is invalid: " + ex.Message;
                return false;
            }
            catch (JsonSerializationException ex)
            {
                feedback = "Family JSON values need cleanup: " + ex.Message;
                return false;
            }
            catch
            {
                feedback = "Family JSON could not be read.";
                return false;
            }
        }

        public static IReadOnlyList<string> ExtractJsonObjectCandidates(string text)
        {
            var candidates = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return candidates;

            string cleaned = Regex.Replace(text.Trim(), "```(?:json)?", string.Empty, RegexOptions.IgnoreCase);
            int start = -1;
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < cleaned.Length; i++)
            {
                char ch = cleaned[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (ch == '\\') escaped = true;
                    else if (ch == '"') inString = false;
                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                    continue;
                }

                if (ch == '{')
                {
                    if (depth == 0) start = i;
                    depth++;
                    continue;
                }

                if (ch == '}' && depth > 0)
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        candidates.Add(cleaned.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }

            return candidates.Distinct().ToList();
        }

        public static int ScoreFamilyJsonCandidate(string candidate)
        {
            string lower = candidate.ToLowerInvariant();
            int score = 0;
            if (lower.Contains("\"components\"")) score += 8;
            if (lower.Contains("\"musait.family.rfa.v2\"")) score += 12;
            if (lower.Contains("\"reference_planes\"") || lower.Contains("\"referenceplanes\"")) score += 9;
            if (lower.Contains("\"constraints\"")) score += 5;
            if (lower.Contains("\"capability\"")) score += 3;
            if (lower.Contains("\"archetype\"")) score += 3;
            if (lower.Contains("\"category\"")) score += 5;
            if (lower.Contains("\"parameters\"")) score += 4;
            if (lower.Contains("\"dims\"") || lower.Contains("\"dimensions\"")) score += 4;
            if (lower.Contains("\"geometry\"")) score += 3;
            if (lower.Contains("\"host\"")) score += 2;
            if (lower.Contains("\"units\"")) score += 2;
            return score;
        }

        public static JObject NormalizeObject(JObject raw)
        {
            JObject root = UnwrapFamilyJsonRoot((JObject)raw.DeepClone());

            MoveAlias(root, "category", "familyCategory", "revitCategory", "categoryName");
            MoveAlias(root, "host", "hosting", "hostType");
            MoveAlias(root, "units", "unit", "unitSystem");
            MoveAlias(root, "components", "component", "elements", "parts", "geometryElements");
            MoveAlias(root, "parameters", "params", "familyParameters");
            MoveAlias(root, "bindings", "parameterBindings", "parameter_bindings");

            NormalizeStringProperty(root, "category", NormalizeFamilyCategory);
            NormalizeStringProperty(root, "host", NormalizeHost);
            NormalizeStringProperty(root, "units", NormalizeUnits);

            if (FindProperty(root, "host") == null) root["host"] = "non-hosted";
            if (FindProperty(root, "units") == null) root["units"] = "mm";
            if (FindProperty(root, "parameters") == null) root["parameters"] = new JArray();
            if (FindProperty(root, "bindings") == null) root["bindings"] = new JArray();

            if (GetPropertyValue(root, "components") is JObject singleComponent)
            {
                root["components"] = new JArray(singleComponent);
            }

            if (GetPropertyValue(root, "components") is JArray components)
            {
                foreach (JObject component in components.OfType<JObject>())
                {
                    NormalizeFamilyComponent(component);
                }
            }

            if (GetPropertyValue(root, "parameters") is JObject singleParameter)
            {
                root["parameters"] = new JArray(singleParameter);
            }

            if (GetPropertyValue(root, "parameters") is JArray parameters)
            {
                foreach (JObject parameter in parameters.OfType<JObject>())
                {
                    NormalizeFamilyParameter(parameter);
                }
            }

            if (GetPropertyValue(root, "bindings") is JObject singleBinding)
            {
                root["bindings"] = new JArray(singleBinding);
            }

            if (GetPropertyValue(root, "bindings") is JArray bindings)
            {
                foreach (JObject binding in bindings.OfType<JObject>())
                {
                    NormalizeFamilyBinding(binding);
                }
            }

            return root;
        }

        private static JObject UnwrapFamilyJsonRoot(JObject root)
        {
            foreach (string wrapper in new[] { "family", "familyDefinition", "family_definition", "definition", "data", "result" })
            {
                if (GetPropertyValue(root, wrapper) is JObject child &&
                    ScoreFamilyJsonCandidate(child.ToString(Formatting.None)) >= ScoreFamilyJsonCandidate(root.ToString(Formatting.None)))
                {
                    return (JObject)child.DeepClone();
                }
            }

            var properties = root.Properties().ToList();
            if (properties.Count == 1 &&
                properties[0].Value is JObject onlyChild &&
                ScoreFamilyJsonCandidate(onlyChild.ToString(Formatting.None)) > 0)
            {
                return (JObject)onlyChild.DeepClone();
            }

            return root;
        }

        private static void NormalizeFamilyComponent(JObject component)
        {
            MoveAlias(component, "id", "name", "label", "componentId");
            MoveAlias(component, "geometry", "geometryType", "type");
            MoveAlias(component, "dims", "dimensions", "size", "bounds");
            MoveAlias(component, "material", "materialName");
            MoveAlias(component, "finish", "surfaceFinish", "appearance", "texture");
            MoveAlias(component, "radius", "r");
            MoveAlias(component, "origin", "position", "location");
            MoveAlias(component, "rotation", "rotate");
            MoveAlias(component, "role", "componentRole");
            MoveAlias(component, "isVoid", "void", "is_void");
            MoveAlias(component, "isVisible", "visible", "visibility", "is_visible");

            NormalizeStringProperty(component, "geometry", value =>
            {
                var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["box"] = "extrusion",
                    ["extrude"] = "extrusion",
                    ["rectangular extrusion"] = "extrusion",
                    ["circular extrusion"] = "revolution",
                    ["cylinder"] = "revolution",
                    ["cylindrical"] = "revolution",
                    ["round"] = "revolution",
                    ["revolve"] = "revolution",
                    ["revolved"] = "revolution"
                };

                return aliases.TryGetValue(value, out string? canonical) ? canonical : value;
            });

            if (FindProperty(component, "geometry") == null) component["geometry"] = "extrusion";
            if (FindProperty(component, "origin") == null) component["origin"] = new JObject();
            if (FindProperty(component, "rotation") == null) component["rotation"] = new JObject();
            if (FindProperty(component, "role") == null) component["role"] = "component";
            if (FindProperty(component, "isVoid") == null) component["isVoid"] = false;
            if (FindProperty(component, "isVisible") == null) component["isVisible"] = true;

            if (GetPropertyValue(component, "dims") is not JObject dims)
            {
                dims = new JObject();
                component["dims"] = dims;
            }

            CopyDimensionAlias(component, dims, "w", "width", "Width");
            CopyDimensionAlias(component, dims, "d", "depth", "Depth");
            CopyDimensionAlias(component, dims, "h", "height", "Height");
            MoveAlias(dims, "w", "width", "Width");
            MoveAlias(dims, "d", "depth", "Depth");
            MoveAlias(dims, "h", "height", "Height");
            NormalizeNumberProperty(dims, "w");
            NormalizeNumberProperty(dims, "d");
            NormalizeNumberProperty(dims, "h");
            NormalizeNumberProperty(component, "radius");

            bool isRevolution = string.Equals(GetPropertyValue(component, "geometry")?.ToString(), "revolution", StringComparison.OrdinalIgnoreCase);
            if (isRevolution && GetPropertyValue(component, "radius") is JValue radiusValue &&
                radiusValue.Type != JTokenType.Null &&
                double.TryParse(radiusValue.ToString(CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out double radius) &&
                radius > 0)
            {
                if (FindProperty(dims, "w") == null || (dims["w"]?.Type == JTokenType.Integer || dims["w"]?.Type == JTokenType.Float) && dims.Value<double>("w") <= 0)
                {
                    dims["w"] = radius * 2;
                }

                if (FindProperty(dims, "d") == null || (dims["d"]?.Type == JTokenType.Integer || dims["d"]?.Type == JTokenType.Float) && dims.Value<double>("d") <= 0)
                {
                    dims["d"] = radius * 2;
                }
            }

            if (GetPropertyValue(component, "origin") is JObject origin)
            {
                CopyCoordinateAlias(component, origin, "x", "X", "originX", "positionX", "posX");
                CopyCoordinateAlias(component, origin, "y", "Y", "originY", "positionY", "posY");
                CopyCoordinateAlias(component, origin, "z", "Z", "originZ", "positionZ", "posZ");
                NormalizePoint(origin);
            }

            if (GetPropertyValue(component, "rotation") is JObject rotation)
            {
                NormalizeNumberProperty(rotation, "z");
                if (FindProperty(rotation, "z") == null) rotation["z"] = 0;
            }

            NormalizeStringProperty(component, "material", SanitizeName);
            NormalizeStringProperty(component, "finish", SanitizeName);
            NormalizeStringProperty(component, "role", SanitizeName);
            NormalizeBooleanProperty(component, "isVoid");
            NormalizeBooleanProperty(component, "isVisible");
        }

        private static void NormalizePoint(JObject point)
        {
            foreach (string axis in new[] { "x", "y", "z" })
            {
                NormalizeNumberProperty(point, axis);
                if (FindProperty(point, axis) == null) point[axis] = 0;
            }
        }

        private static void NormalizeFamilyParameter(JObject parameter)
        {
            MoveAlias(parameter, "name", "id", "label", "parameterName");
            MoveAlias(parameter, "type", "parameterType", "valueType", "kind");
            MoveAlias(parameter, "default", "defaultValue", "value");
            MoveAlias(parameter, "instance", "isInstance", "instanceParameter");

            NormalizeStringProperty(parameter, "name", SanitizeName);
            NormalizeStringProperty(parameter, "type", NormalizeParameterType);
            NormalizeNumberProperty(parameter, "default");
            NormalizeBooleanProperty(parameter, "instance");

            if (FindProperty(parameter, "instance") == null)
            {
                parameter["instance"] = false;
            }
        }

        private static void NormalizeFamilyBinding(JObject binding)
        {
            MoveAlias(binding, "parameter", "param", "parameterName", "name");
            MoveAlias(binding, "targets", "target");
            NormalizeStringProperty(binding, "parameter", SanitizeName);

            if (GetPropertyValue(binding, "targets") is JObject singleTarget)
            {
                binding["targets"] = new JArray(singleTarget);
            }

            if (GetPropertyValue(binding, "targets") is not JArray targets) return;

            var clones = new List<JObject>();
            foreach (JObject target in targets.OfType<JObject>().ToList())
            {
                MoveAlias(target, "component", "componentId", "id");
                MoveAlias(target, "path", "property", "targetPath");
                MoveAlias(target, "expression", "expr", "formula");
                NormalizeStringProperty(target, "component", SanitizeName);
                NormalizeStringProperty(target, "path", value => value.Trim());
                NormalizeStringProperty(target, "expression", value => value.Replace("\\*", "*").Trim());

                string path = target["path"]?.ToString() ?? string.Empty;
                if (path.Equals("radius", StringComparison.OrdinalIgnoreCase) || path.Equals("r", StringComparison.OrdinalIgnoreCase))
                {
                    target["path"] = "dims.w";
                    string expr = target["expression"]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(expr))
                    {
                        target["expression"] = $"({expr}) * 2";
                    }

                    var clone = (JObject)target.DeepClone();
                    clone["path"] = "dims.d";
                    clones.Add(clone);
                }
            }

            foreach (var clone in clones)
            {
                targets.Add(clone);
            }
        }

        private static void CopyDimensionAlias(JObject source, JObject dims, string canonical, params string[] aliases)
        {
            if (FindProperty(dims, canonical) != null) return;

            foreach (string alias in aliases)
            {
                var prop = FindProperty(source, alias);
                if (prop != null)
                {
                    dims[canonical] = prop.Value.DeepClone();
                    prop.Remove();
                    return;
                }
            }
        }

        private static void CopyCoordinateAlias(JObject source, JObject origin, string canonical, params string[] aliases)
        {
            if (FindProperty(origin, canonical) != null) return;

            var canonicalProp = FindProperty(source, canonical);
            if (canonicalProp != null)
            {
                origin[canonical] = canonicalProp.Value.DeepClone();
                canonicalProp.Remove();
                return;
            }

            foreach (string alias in aliases)
            {
                var prop = FindProperty(source, alias);
                if (prop == null) continue;
                origin[canonical] = prop.Value.DeepClone();
                prop.Remove();
                return;
            }
        }

        private static void MoveAlias(JObject obj, string canonical, params string[] aliases)
        {
            var canonicalProp = FindProperty(obj, canonical);
            if (canonicalProp != null)
            {
                if (!string.Equals(canonicalProp.Name, canonical, StringComparison.Ordinal))
                {
                    var value = canonicalProp.Value.DeepClone();
                    canonicalProp.Remove();
                    obj[canonical] = value;
                }

                RemoveAliases(obj, aliases);
                return;
            }

            foreach (string alias in aliases)
            {
                var prop = FindProperty(obj, alias);
                if (prop == null) continue;
                obj[canonical] = prop.Value.DeepClone();
                prop.Remove();
                RemoveAliases(obj, aliases);
                return;
            }
        }

        private static void RemoveAliases(JObject obj, IEnumerable<string> aliases)
        {
            foreach (string alias in aliases)
            {
                FindProperty(obj, alias)?.Remove();
            }
        }

        private static JProperty? FindProperty(JObject obj, string name)
        {
            return obj.Properties().FirstOrDefault(prop => string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static JToken? GetPropertyValue(JObject obj, string name)
        {
            return FindProperty(obj, name)?.Value;
        }

        private static void NormalizeStringProperty(JObject obj, string name, Func<string, string> normalize)
        {
            var prop = FindProperty(obj, name);
            if (prop?.Value.Type != JTokenType.String) return;
            prop.Value = normalize(prop.Value.ToString().Trim());
        }

        private static void NormalizeNumberProperty(JObject obj, string name)
        {
            var prop = FindProperty(obj, name);
            if (prop == null) return;
            if (prop.Value.Type == JTokenType.Integer || prop.Value.Type == JTokenType.Float) return;

            if (prop.Value.Type == JTokenType.String &&
                TryParseNumberWithUnits(prop.Value.ToString(), out double number))
            {
                prop.Value = number;
            }
        }

        private static void NormalizeBooleanProperty(JObject obj, string name)
        {
            var prop = FindProperty(obj, name);
            if (prop == null || prop.Value.Type == JTokenType.Boolean) return;
            if (prop.Value.Type != JTokenType.String) return;

            string value = prop.Value.ToString().Trim();
            if (value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("instance", StringComparison.OrdinalIgnoreCase))
            {
                prop.Value = true;
            }
            else if (value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                     value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                     value.Equals("type", StringComparison.OrdinalIgnoreCase))
            {
                prop.Value = false;
            }
        }

        private static bool TryParseNumberWithUnits(string text, out double number)
        {
            number = 0;
            var match = Regex.Match(text, @"-?\d+(?:[.,]\d+)?");
            if (!match.Success) return false;

            string value = match.Value;
            int commaIndex = value.IndexOf(',');
            if (commaIndex >= 0 && !value.Contains('.'))
            {
                int digitsAfterComma = value.Length - commaIndex - 1;
                value = digitsAfterComma == 3 ? value.Replace(",", string.Empty) : value.Replace(",", ".");
            }
            else
            {
                value = value.Replace(",", string.Empty);
            }

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

        private static string NormalizeFamilyCategory(string value)
        {
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["cabinetry"] = "Casework",
                ["casework"] = "Casework",
                ["electrical"] = "Electrical Fixtures",
                ["electrical fixture"] = "Electrical Fixtures",
                ["electrical fixtures"] = "Electrical Fixtures",
                ["entourage"] = "Entourage",
                ["furniture"] = "Furniture",
                ["furniture system"] = "Furniture Systems",
                ["furniture systems"] = "Furniture Systems",
                ["generic"] = "Generic Model",
                ["generic model"] = "Generic Model",
                ["generic models"] = "Generic Model",
                ["lighting"] = "Lighting Fixtures",
                ["lighting fixture"] = "Lighting Fixtures",
                ["lighting fixtures"] = "Lighting Fixtures",
                ["mechanical"] = "Mechanical Equipment",
                ["mechanical equipment"] = "Mechanical Equipment",
                ["planting"] = "Planting",
                ["plumbing"] = "Plumbing Fixtures",
                ["plumbing fixture"] = "Plumbing Fixtures",
                ["plumbing fixtures"] = "Plumbing Fixtures",
                ["special equipment"] = "Specialty Equipment",
                ["specialty equipment"] = "Specialty Equipment"
            };

            return aliases.TryGetValue(value, out string? canonical) ? canonical : value;
        }

        private static string NormalizeHost(string value)
        {
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["none"] = "non-hosted",
                ["non hosted"] = "non-hosted",
                ["non-hosted"] = "non-hosted",
                ["standalone"] = "non-hosted",
                ["wall"] = "wall-hosted",
                ["wall hosted"] = "wall-hosted",
                ["wall-hosted"] = "wall-hosted",
                ["floor"] = "floor-hosted",
                ["floor hosted"] = "floor-hosted",
                ["floor-hosted"] = "floor-hosted",
                ["ceiling"] = "ceiling-hosted",
                ["ceiling hosted"] = "ceiling-hosted",
                ["ceiling-hosted"] = "ceiling-hosted",
                ["face"] = "face-based",
                ["face based"] = "face-based",
                ["face-based"] = "face-based"
            };

            return aliases.TryGetValue(value, out string? canonical) ? canonical : value;
        }

        private static string NormalizeUnits(string value)
        {
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["millimeter"] = "mm",
                ["millimeters"] = "mm",
                ["mm"] = "mm",
                ["centimeter"] = "cm",
                ["centimeters"] = "cm",
                ["cm"] = "cm",
                ["meter"] = "m",
                ["meters"] = "m",
                ["m"] = "m",
                ["inch"] = "in",
                ["inches"] = "in",
                ["in"] = "in",
                ["foot"] = "ft",
                ["feet"] = "ft",
                ["ft"] = "ft"
            };

            return aliases.TryGetValue(value, out string? canonical) ? canonical : value;
        }

        private static string NormalizeParameterType(string value)
        {
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bool"] = "YesNo",
                ["boolean"] = "YesNo",
                ["integer"] = "Integer",
                ["int"] = "Integer",
                ["length"] = "Length",
                ["material"] = "Material",
                ["number"] = "Number",
                ["numeric"] = "Number",
                ["real"] = "Number",
                ["string"] = "Text",
                ["text"] = "Text",
                ["yes/no"] = "YesNo",
                ["yesno"] = "YesNo",
                ["yes-no"] = "YesNo"
            };

            return aliases.TryGetValue(value, out string? canonical) ? canonical : value;
        }

        private static string SanitizeName(string value)
        {
            string cleaned = Regex.Replace(value.Trim(), @"[^\w\s\-.]", string.Empty);
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            return string.IsNullOrWhiteSpace(cleaned) ? "Component" : cleaned;
        }
    }
}
