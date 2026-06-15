// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Musait.Models
{
    public enum FamilyRigKind
    {
        Unknown,
        Casework,
        Table
    }

    public sealed class FamilyParametricIntent
    {
        public FamilyRigKind Kind { get; set; } = FamilyRigKind.Unknown;
        public List<FamilyRigParameter> Parameters { get; set; } = new();
        public List<FamilyRigPlane> ReferencePlanes { get; set; } = new();
        public List<FamilyRigComponentBounds> ComponentBounds { get; set; } = new();
        public List<FamilyRigBinding> Bindings { get; set; } = new();
        public List<FamilyRigDiagnostic> Diagnostics { get; set; } = new();
    }

    public sealed class FamilyRigParameter
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Length";
        public object? Default { get; set; }
        public bool Inferred { get; set; }
    }

    public sealed class FamilyRigPlane
    {
        public string Id { get; set; } = string.Empty;
        public string Axis { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
    }

    public sealed class FamilyRigComponentBounds
    {
        public string Component { get; set; } = string.Empty;
        public string X0 { get; set; } = string.Empty;
        public string X1 { get; set; } = string.Empty;
        public string Y0 { get; set; } = string.Empty;
        public string Y1 { get; set; } = string.Empty;
        public string Z0 { get; set; } = string.Empty;
        public string Z1 { get; set; } = string.Empty;
    }

    public sealed class FamilyRigBinding
    {
        public string Parameter { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Expression { get; set; } = string.Empty;
        public bool Inferred { get; set; }
    }

    public sealed class FamilyRigDiagnostic
    {
        [JsonProperty("severity")]
        public string Severity { get; set; } = "info";

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("component")]
        public string Component { get; set; } = string.Empty;

        [JsonProperty("parameter")]
        public string Parameter { get; set; } = string.Empty;
    }
}
