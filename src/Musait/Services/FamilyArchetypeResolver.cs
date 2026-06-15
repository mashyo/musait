// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using Musait.Models;

namespace Musait.Services
{
    public static class FamilyArchetypeResolver
    {
        public static FamilyDefinitionV2 Resolve(FamilyDefinitionV2 definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            if (string.Equals(definition.Archetype, "casework.wardrobe", StringComparison.OrdinalIgnoreCase) &&
                (definition.Geometry == null || definition.Geometry.Count == 0))
            {
                return CaseworkWardrobeRecipe.Materialize(definition);
            }

            return definition;
        }
    }
}
