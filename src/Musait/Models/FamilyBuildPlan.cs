// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System.Collections.Generic;

namespace Musait.Models
{
    public sealed class FamilyBuildPlan
    {
        public string Category { get; set; } = string.Empty;
        public string Host { get; set; } = "non-hosted";
        public string DisplayUnits { get; set; } = "mm";
        public string Schema { get; set; } = "musait.family.v1";
        public string Capability { get; set; } = "static";
        public string Archetype { get; set; } = string.Empty;
        public IReadOnlyList<FamilyBuildComponent> Components { get; set; } = new List<FamilyBuildComponent>();
        public IReadOnlyList<FamilyParameterDefinition> Parameters { get; set; } = new List<FamilyParameterDefinition>();
        public IReadOnlyList<FamilyParameterBindingDefinition> Bindings { get; set; } = new List<FamilyParameterBindingDefinition>();
        public IReadOnlyList<FamilyRepeaterDefinition> Repeaters { get; set; } = new List<FamilyRepeaterDefinition>();
        public IReadOnlyList<FamilyRigDiagnostic> Diagnostics { get; set; } = new List<FamilyRigDiagnostic>();
        public FamilyBuildExtents Extents { get; set; } = new();
    }

    public sealed class FamilyBuildComponent
    {
        public string Id { get; set; } = string.Empty;
        public string Geometry { get; set; } = "extrusion";
        public string Material { get; set; } = string.Empty;
        public string Finish { get; set; } = string.Empty;
        public string Role { get; set; } = "component";
        public bool IsVoid { get; set; }
        public bool IsVisible { get; set; } = true;
        public double WidthFeet { get; set; }
        public double DepthFeet { get; set; }
        public double HeightFeet { get; set; }
        public double RadiusFeet { get; set; }
        public double OriginXFeet { get; set; }
        public double OriginYFeet { get; set; }
        public double OriginZFeet { get; set; }
        public double RotationZDegrees { get; set; }
    }

    public sealed class FamilyBuildExtents
    {
        public double MinXFeet { get; set; }
        public double MinYFeet { get; set; }
        public double MinZFeet { get; set; }
        public double MaxXFeet { get; set; }
        public double MaxYFeet { get; set; }
        public double MaxZFeet { get; set; }
        public double WidthFeet => MaxXFeet - MinXFeet;
        public double DepthFeet => MaxYFeet - MinYFeet;
        public double HeightFeet => MaxZFeet - MinZFeet;
    }
}
