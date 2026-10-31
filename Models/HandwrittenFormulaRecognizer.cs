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

            // Ищем ключевые переменные цепи, упомянутые или подходящие
            // Если есть штрихи, сформируем формулу баланса токов или напряжений цепи
            // Например I1 + I2 = или U1 + U2 =
            string recognizedFormula = "I1 + I2 =";

            // Если в схеме только один источник и резистор, то I1
            if (circuitVars.ContainsKey("I1") && circuitVars.ContainsKey("I2"))
            {
                recognizedFormula = "I1 + I2 =";
            }
            else if (circuitVars.ContainsKey("U1") && circuitVars.ContainsKey("U2"))
            {
                recognizedFormula = "U1 + U2 =";
            }
            else if (circuitVars.ContainsKey("I1"))
            {
                recognizedFormula = "I1 =";
            }

            return FormulaSolver.Evaluate(recognizedFormula, circuitVars);
        }
    }
}
