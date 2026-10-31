using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CircuitCanvas.Models
{
    public class FormulaSolver
    {
        public static string Evaluate(string expression, Dictionary<string, double> circuitVariables)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return "Введите или нарисуйте формулу (например: I1 + I2 = )";

            string cleanExpr = expression.Trim();
            if (cleanExpr.EndsWith("="))
            {
                cleanExpr = cleanExpr.Substring(0, cleanExpr.Length - 1).Trim();
            }

            // Определение единицы измерения результата
            string resultUnit = "";
            string lowerExpr = cleanExpr.ToLower();
            if (lowerExpr.Contains("i1") || lowerExpr.Contains("i2") || lowerExpr.Contains("i3") || lowerExpr.StartsWith("i"))
            {
                if (lowerExpr.Contains("*") && (lowerExpr.Contains("r") || lowerExpr.Contains("100") || lowerExpr.Contains("10")))
                {
                    resultUnit = "В (Вольт)";
                }
                else if (lowerExpr.Contains("*") && lowerExpr.Contains("u"))
                {
                    resultUnit = "Вт (Ватт)";
                }
                else
                {
                    resultUnit = "А (Ампер)";
                }
            }
            else if (lowerExpr.Contains("u1") || lowerExpr.Contains("u2") || lowerExpr.Contains("v1") || lowerExpr.StartsWith("u") || lowerExpr.StartsWith("v"))
            {
                if (lowerExpr.Contains("/") && lowerExpr.Contains("r"))
                {
                    resultUnit = "А (Ампер)";
                }
                else
                {
                    resultUnit = "В (Вольт)";
                }
            }
            else if (lowerExpr.Contains("r1") || lowerExpr.Contains("r2") || lowerExpr.Contains("r"))
            {
                resultUnit = "Ом";
            }
            else if (lowerExpr.Contains("p1") || lowerExpr.Contains("p"))
            {
                resultUnit = "Вт (Ватт)";
            }

            string evaluatedExpr = cleanExpr;

            var sortedVars = new List<KeyValuePair<string, double>>(circuitVariables);
            sortedVars.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));

            foreach (var kvp in sortedVars)
            {
                string pattern = @"\b" + Regex.Escape(kvp.Key) + @"\b";
                evaluatedExpr = Regex.Replace(
                    evaluatedExpr,
                    pattern,
                    kvp.Value.ToString("G6", CultureInfo.InvariantCulture),
                    RegexOptions.IgnoreCase
                );
            }

            evaluatedExpr = evaluatedExpr.Replace("×", "*").Replace("÷", "/").Replace(",", ".");

            try
            {
                var dt = new DataTable();
                var computeRes = dt.Compute(evaluatedExpr, null);
                double val = Convert.ToDouble(computeRes, CultureInfo.InvariantCulture);

                string unitSuffix = string.IsNullOrEmpty(resultUnit) ? "" : $" {resultUnit}";
                return $"{cleanExpr} = {val:G6}{unitSuffix}";
            }
            catch (Exception ex)
            {
                return $"Ошибка формулы ({cleanExpr}): {ex.Message}";
            }
        }
    }
}
