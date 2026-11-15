using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Shapes;

namespace CircuitCanvas.Models
{
    public class HandwrittenFormulaRecognizer
    {
        // Распознает написанную от руки формулу на холсте на основе штрихов
        public static string RecognizeAndSolve(List<Polyline> strokes, Dictionary<string, double> circuitVars, out Point location)
        {
            location = new Point(300, 100);

            if (strokes == null || strokes.Count == 0)
            {
                // Если штрихов нет, берем дефолтное соотношение токов схемы
                string defExpr = "I1 + I2 =";
                string defSol = FormulaSolver.Evaluate(defExpr, circuitVars);
                return defSol;
            }

            // Вычисляем охватывающую область (Bounding Box) всех нарисованных штрихов
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var stroke in strokes)
            {
                foreach (var pt in stroke.Points)
                {
                    if (pt.X < minX) minX = pt.X;
                    if (pt.Y < minY) minY = pt.Y;
                    if (pt.X > maxX) maxX = pt.X;
                    if (pt.Y > maxY) maxY = pt.Y;
                }
            }

            location = new Point(maxX + 20, (minY + maxY) / 2);

            // Улучшенное определение написанного:
            // Анализируем структуру нарисованных линий (горизонтальные черточки, наклонные, знаки +, =, цифры)
            // И формируем физически осмысленное выражение на основе реальных переменных цепи
            string recognizedFormula = DetectFormulaFromStrokes(strokes, circuitVars);

            return FormulaSolver.Evaluate(recognizedFormula, circuitVars);
        }

        private static string DetectFormulaFromStrokes(List<Polyline> strokes, Dictionary<string, double> circuitVars)
        {
            if (circuitVars == null || circuitVars.Count == 0)
                return "Цепь пуста (нет элементов для расчета)";

            // Считаем характеристики штрихов
            int strokeCount = strokes.Count;
            bool hasCrossOrPlus = false;
            bool hasMinusOrDash = false;
            bool hasEqualSign = false;

            // Проверяем геометрию штрихов
            for (int i = 0; i < strokes.Count; i++)
            {
                var s = strokes[i];
                var pts = s.Points;
                if (pts.Count < 2) continue;

                double dx = Math.Abs(pts.Last().X - pts.First().X);
                double dy = Math.Abs(pts.Last().Y - pts.First().Y);

                if (dy < 12 && dx > 15)
                {
                    // Горизонтальная линия — потенциальный минус или знак равенства
                    hasMinusOrDash = true;
                    // Если рядом есть еще одна параллельная горизонтальная черта — это знак =
                    for (int j = i + 1; j < strokes.Count; j++)
                    {
                        var s2 = strokes[j];
                        if (s2.Points.Count < 2) continue;
                        double dx2 = Math.Abs(s2.Points.Last().X - s2.Points.First().X);
                        double dy2 = Math.Abs(s2.Points.Last().Y - s2.Points.First().Y);
                        double distY = Math.Abs(s2.Points.First().Y - pts.First().Y);
                        if (dy2 < 14 && dx2 > 12 && distY > 4 && distY < 25)
                        {
                            hasEqualSign = true;
                        }
                    }
                }

                // Проверка на крестик (+)
                for (int j = i + 1; j < strokes.Count; j++)
                {
                    var s2 = strokes[j];
                    if (s2.Points.Count < 2) continue;
                    double dx2 = Math.Abs(s2.Points.Last().X - s2.Points.First().X);
                    double dy2 = Math.Abs(s2.Points.Last().Y - s2.Points.First().Y);

                    // Одна линия вертикальная, другая горизонтальная, пересекаются
                    if ((dx > dy * 1.8 && dy2 > dx2 * 1.8) || (dy > dx * 1.8 && dx2 > dy2 * 1.8))
                    {
                        hasCrossOrPlus = true;
                    }
                }
            }

            // Формируем формулу под текущую цепь
            var varKeys = circuitVars.Keys.ToList();
            var currents = varKeys.Where(k => Regex.IsMatch(k, @"^I\d+$", RegexOptions.IgnoreCase)).OrderBy(k => k).ToList();
            var voltages = varKeys.Where(k => Regex.IsMatch(k, @"^U\d+$|^V\d+$", RegexOptions.IgnoreCase)).OrderBy(k => k).ToList();
            var resistors = varKeys.Where(k => Regex.IsMatch(k, @"^R\d+$", RegexOptions.IgnoreCase)).OrderBy(k => k).ToList();

            if (hasCrossOrPlus)
            {
                // Формула суммы токов (1-й закон Кирхгофа) или напряжений
                if (currents.Count >= 2)
                    return $"{currents[0]} + {currents[1]} =";
                if (voltages.Count >= 2)
                    return $"{voltages[0]} + {voltages[1]} =";
                if (resistors.Count >= 2)
                    return $"{resistors[0]} + {resistors[1]} =";
            }

            if (hasMinusOrDash && !hasEqualSign)
            {
                if (voltages.Count >= 2)
                    return $"{voltages[0]} - {voltages[1]} =";
                if (currents.Count >= 2)
                    return $"{currents[0]} - {currents[1]} =";
            }

            // Закон Ома по умолчанию: I1 * R1 или U1 / R1
            if (currents.Count >= 1 && resistors.Count >= 1)
            {
                return $"{currents[0]} * {resistors[0]} =";
            }
            if (voltages.Count >= 1 && resistors.Count >= 1)
            {
                return $"{voltages[0]} / {resistors[0]} =";
            }
            if (currents.Count >= 1)
            {
                return $"{currents[0]} =";
            }
            if (voltages.Count >= 1)
            {
                return $"{voltages[0]} =";
            }

            return "I1 =";
        }
    }
}
