// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Musait.Models;

namespace Musait.Services
{
    public sealed class FamilyDefinitionV2Normalizer
    {
        public FamilyDefinitionV2 Normalize(JObject raw)
        {
            var root = Unwrap((JObject)raw.DeepClone());
            MoveAlias(root, "reference_planes", "referencePlanes", "planes");
            MoveAlias(root, "instance_or_type", "instanceOrType");
            if (root["schema"] == null) root["schema"] = FamilyDefinitionV2.SchemaId;
            if (root["host"] == null) root["host"] = "non-hosted";
            if (root["units"] == null) root["units"] = "mm";
            if (root["capability"] == null) root["capability"] = "static";
            if (root["reference_planes"] == null) root["reference_planes"] = new JArray();
            if (root["parameters"] == null) root["parameters"] = new JArray();
            if (root["geometry"] == null) root["geometry"] = new JArray();
            if (root["constraints"] == null) root["constraints"] = new JArray();

            NormalizeArrayObjectAliases(root, "parameters", parameter =>
            {
                MoveAlias(parameter, "default_value", "default", "defaultValue", "value");
                MoveAlias(parameter, "instance_or_type", "instanceOrType", "scope");
                if (parameter["instance_or_type"] == null) parameter["instance_or_type"] = "type";
                if (parameter["group"] == null) parameter["group"] = GuessParameterGroup(parameter["type"]?.ToString());
                parameter["type"] = NormalizeParameterType(parameter["type"]?.ToString() ?? "length");
            });

            NormalizeArrayObjectAliases(root, "reference_planes", plane =>
            {
                MoveAlias(plane, "is_reference", "isReference");
                plane["direction"] = NormalizeDirection(plane["direction"]?.ToString() ?? "x");
                if (plane["offset"] == null) plane["offset"] = 0;
            });

            NormalizeArrayObjectAliases(root, "geometry", geometry =>
            {
                MoveAlias(geometry, "solid_or_void", "solidOrVoid", "solidVoid");
                if (geometry["kind"] == null) geometry["kind"] = "extrusion";
                if (geometry["solid_or_void"] == null) geometry["solid_or_void"] = "solid";
                if (geometry["bounds"] == null) geometry["bounds"] = new JObject();
            });

            var definition = root.ToObject<FamilyDefinitionV2>() ?? new FamilyDefinitionV2();
            return FamilyArchetypeResolver.Resolve(definition);
        }

        public static bool IsV2(JObject obj)
        {
            obj = Unwrap(obj);
            string schema = obj["schema"]?.ToString() ?? string.Empty;
            return string.Equals(schema, FamilyDefinitionV2.SchemaId, StringComparison.OrdinalIgnoreCase) ||
                   obj["reference_planes"] != null ||
                   obj["referencePlanes"] != null ||
                   (obj["geometry"] is JArray && obj["constraints"] is JArray);
        }

        private static JObject Unwrap(JObject root)
        {
            foreach (string wrapper in new[] { "family", "familyDefinition", "family_definition", "definition", "data", "result" })
            {
                if (root[wrapper] is JObject child &&
                    (child["schema"] != null || child["reference_planes"] != null || child["referencePlanes"] != null))
                {
                    return (JObject)child.DeepClone();
                }
            }

            return root;
        }

        private static void NormalizeArrayObjectAliases(JObject root, string arrayName, Action<JObject> normalize)
        {
            if (root[arrayName] is JObject single) root[arrayName] = new JArray(single);
            if (root[arrayName] is not JArray array) return;
            foreach (var item in array.OfType<JObject>())
            {
                normalize(item);
            }
        }

        private static void MoveAlias(JObject obj, string canonical, params string[] aliases)
        {
            if (obj[canonical] != null) return;
            foreach (string alias in aliases)
            {
                if (obj[alias] == null) continue;
                obj[canonical] = obj[alias]!.DeepClone();
                obj.Remove(alias);
                return;
            }
        }

        private static string NormalizeDirection(string value)
        {
            value = value.Trim().ToLowerInvariant();
            return value is "y" or "depth" ? "y" : value is "z" or "height" ? "z" : "x";
        }

        private static string NormalizeParameterType(string value)
        {
            return value.Trim().ToLowerInvariant() switch
            {
                "bool" or "boolean" or "yesno" or "yes/no" or "yes-no" => "yes_no",
                "int" => "integer",
                "string" => "text",
                "material" => "material",
                "angle" => "angle",
                "number" => "number",
                "integer" => "integer",
                "text" => "text",
                _ => "length"
            };
        }

        private static string GuessParameterGroup(string? type)
        {
            return (type ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "length" or "angle" => "Dimensions",
                "material" => "Materials and Finishes",
                "yes_no" or "yesno" => "Visibility",
                _ => "Data"
            };
        }
    }
}
