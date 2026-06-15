// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Linq;
using Newtonsoft.Json;
using Musait.Models;

namespace Musait.Services
{
    public static class FamilyPreviewPayloadFactory
    {
        public static string CreateJson(FamilyBuildPlan plan, string jsonPath, string[]? warnings = null)
        {
            var payload = new
            {
                category = plan.Category,
                host = plan.Host,
                units = plan.DisplayUnits,
                schema = plan.Schema,
                capability = plan.Capability,
                archetype = plan.Archetype,
                components = plan.Components.Select(component => new
                {
                    id = component.Id,
                    geometry = component.Geometry,
                    role = component.Role,
                    origin = new
                    {
                        x = ToDisplay(component.OriginXFeet, plan.DisplayUnits),
                        y = ToDisplay(component.OriginYFeet, plan.DisplayUnits),
                        z = ToDisplay(component.OriginZFeet, plan.DisplayUnits)
                    },
                    rotation = new
                    {
                        z = component.RotationZDegrees
                    },
                    dims = new
                    {
                        w = ToDisplay(component.WidthFeet, plan.DisplayUnits),
                        d = ToDisplay(component.DepthFeet, plan.DisplayUnits),
                        h = ToDisplay(component.HeightFeet, plan.DisplayUnits)
                    },
                    material = component.Material,
                    finish = component.Finish,
                    radius = ToDisplay(component.RadiusFeet, plan.DisplayUnits),
                    isVoid = component.IsVoid,
                    isVisible = component.IsVisible
                }),
                parameters = plan.Parameters.Select(parameter => new
                {
                    name = parameter.Name,
                    type = parameter.Type,
                    @default = parameter.Default,
                    instance = parameter.Instance
                }),
                repeaters = plan.Repeaters.Select(repeater => new
                {
                    id = repeater.Id,
                    templateComponent = repeater.TemplateComponent,
                    countParameter = repeater.CountParameter,
                    axis = repeater.Axis,
                    start = repeater.Start,
                    spacing = repeater.Spacing
                }),
                bindings = plan.Bindings.Select(binding => new
                {
                    parameter = binding.Parameter,
                    inferred = binding.Inferred,
                    targets = binding.Targets.Select(target => new
                    {
                        component = target.Component,
                        path = target.Path,
                        expression = target.Expression
                    })
                }),
                diagnostics = plan.Diagnostics.Concat((warnings ?? Array.Empty<string>()).Select(warning => new FamilyRigDiagnostic
                {
                    Severity = "warning",
                    Message = warning
                })).Select(diagnostic => new
                {
                    severity = diagnostic.Severity,
                    message = diagnostic.Message,
                    component = diagnostic.Component,
                    parameter = diagnostic.Parameter
                })
            };

            return JsonConvert.SerializeObject(payload);
        }

        private static double ToDisplay(double feet, string units)
        {
            return Math.Round(RevitUnitConverter.FromFeet(feet, units), 4);
        }
    }
}
