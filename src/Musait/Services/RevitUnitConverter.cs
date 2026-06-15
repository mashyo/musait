// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;

namespace Musait.Services
{
    public static class RevitUnitConverter
    {
        public static double ToFeet(double value, string units)
        {
            return (units ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "mm" => value / 304.8,
                "cm" => value / 30.48,
                "m" => value / 0.3048,
                "in" => value / 12.0,
                "ft" => value,
                _ => throw new ArgumentOutOfRangeException(nameof(units), "Unsupported family units.")
            };
        }

        public static double FromFeet(double value, string units)
        {
            return (units ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "mm" => value * 304.8,
                "cm" => value * 30.48,
                "m" => value * 0.3048,
                "in" => value * 12.0,
                "ft" => value,
                _ => throw new ArgumentOutOfRangeException(nameof(units), "Unsupported family units.")
            };
        }
    }
}
