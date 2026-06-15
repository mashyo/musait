// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Musait.Services
{
    internal static class FamilyParametricExpressionEvaluator
    {
        public static bool TryEvaluate(string expression, IReadOnlyDictionary<string, double> values, out double result)
        {
            result = double.NaN;
            var normalizedValues = CreateNormalizedValues(values);
            string normalizedExpression = NormalizeExpression(expression, values);
            var tokens = Regex.Matches(normalizedExpression, @"[A-Za-z_][A-Za-z0-9_.]*|\d+(?:\.\d+)?|[()+\-*/]");
            int index = 0;

            double ParseExpression()
            {
                double value = ParseTerm();
                while (index < tokens.Count && (tokens[index].Value == "+" || tokens[index].Value == "-"))
                {
                    string op = tokens[index++].Value;
                    double right = ParseTerm();
                    value = op == "+" ? value + right : value - right;
                }

                return value;
            }

            double ParseTerm()
            {
                double value = ParseFactor();
                while (index < tokens.Count && (tokens[index].Value == "*" || tokens[index].Value == "/"))
                {
                    string op = tokens[index++].Value;
                    double right = ParseFactor();
                    value = op == "*" ? value * right : value / right;
                }

                return value;
            }

            double ParseFactor()
            {
                if (index >= tokens.Count) return double.NaN;
                string token = tokens[index++].Value;
                if (token == "(")
                {
                    double value = ParseExpression();
                    if (index < tokens.Count && tokens[index].Value == ")") index++;
                    return value;
                }

                if (token == "-") return -ParseFactor();
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return number;
                return normalizedValues.TryGetValue(token, out double parameterValue) ? parameterValue : double.NaN;
            }

            try
            {
                result = ParseExpression();
                return index == tokens.Count && !double.IsNaN(result) && !double.IsInfinity(result);
            }
            catch
            {
                result = double.NaN;
                return false;
            }
        }

        private static Dictionary<string, double> CreateNormalizedValues(IReadOnlyDictionary<string, double> values)
        {
            var normalized = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in values)
            {
                normalized[pair.Key] = pair.Value;
                normalized[SanitizeIdentifier(pair.Key)] = pair.Value;
            }

            return normalized;
        }

        private static string NormalizeExpression(string? expression, IReadOnlyDictionary<string, double> values)
        {
            string normalized = expression ?? string.Empty;
            foreach (string key in values.Keys.OrderByDescending(key => key.Length))
            {
                string safe = SanitizeIdentifier(key);
                if (string.Equals(key, safe, StringComparison.Ordinal))
                {
                    continue;
                }

                normalized = Regex.Replace(
                    normalized,
                    $@"(?<![A-Za-z0-9_.]){Regex.Escape(key)}(?![A-Za-z0-9_.])",
                    safe,
                    RegexOptions.IgnoreCase);
            }

            return normalized;
        }

        private static string SanitizeIdentifier(string name)
        {
            string safe = Regex.Replace((name ?? string.Empty).Trim(), @"[^\w.]+", "_");
            safe = Regex.Replace(safe, "_+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(safe) ? "Value" : safe;
        }
    }
}
