using System;
using System.Collections.Generic;
using System.Linq;

namespace CircuitCanvas.Models
{
    public class SimulationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<int, double> NodeVoltages { get; set; } = new Dictionary<int, double>();
        public List<ComponentReport> Reports { get; set; } = new List<ComponentReport>();
    }

    public class ComponentReport
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double ParamValue { get; set; }
        public string Unit { get; set; } = string.Empty;
        public double Voltage { get; set; }
        public double Current { get; set; }
        public double Power { get; set; }
    }

    public class MnaSolver
    {
        public static SimulationResult Solve(List<CircuitComponent> components, List<Wire> wires)
        {
            var res = new SimulationResult();
            if (components.Count == 0)
            {
                res.Success = false;
                res.Message = "Схема пуста. Добавьте элементы из боковой панели.";
                return res;
            }

            var allPins = components.SelectMany(c => c.Pins).ToList();
            if (allPins.Count == 0)
            {
                res.Success = false;
                res.Message = "В схеме нет контактов.";
                return res;
            }

            // Union-find для узлов
            var parentPin = new Dictionary<Pin, Pin>();
            Pin FindRoot(Pin p)
            {
                if (!parentPin.ContainsKey(p)) parentPin[p] = p;
                if (parentPin[p] == p) return p;
                return parentPin[p] = FindRoot(parentPin[p]);
            }

            void UnionPins(Pin p1, Pin p2)
            {
                var r1 = FindRoot(p1);
                var r2 = FindRoot(p2);
                if (r1 != r2) parentPin[r1] = r2;
            }

            foreach (var pin in allPins) parentPin[pin] = pin;

            foreach (var wire in wires)
            {
                if (wire.Pin1 != null && wire.Pin2 != null)
                {
                    UnionPins(wire.Pin1, wire.Pin2);
                }
            }

            var rootToNodeIndex = new Dictionary<Pin, int>();
            int nextNodeIdx = 1;

            var gndComp = components.FirstOrDefault(c => c.Type == ComponentType.Ground);
            Pin? gndRoot = null;
            if (gndComp != null && gndComp.Pins.Count > 0)
            {
                gndRoot = FindRoot(gndComp.Pins[0]);
                rootToNodeIndex[gndRoot] = 0; // Опорный узел 0 В
            }

            foreach (var pin in allPins)
            {
                var r = FindRoot(pin);
                if (!rootToNodeIndex.ContainsKey(r))
                {
                    if (gndRoot == null)
                    {
                        gndRoot = r;
                        rootToNodeIndex[r] = 0;
                    }
                    else
                    {
                        rootToNodeIndex[r] = nextNodeIdx++;
                    }
                }
            }

            int n = nextNodeIdx - 1;

            // Источники напряжения
            var vSources = components.Where(c => IsVoltageSource(c.Type)).ToList();
            int m = vSources.Count;
            int totalDim = n + m;

            if (totalDim == 0)
            {
                res.Success = false;
                res.Message = "Недостаточно узлов или источников для расчета.";
                return res;
            }

            double[,] A = new double[totalDim, totalDim];
            double[] z = new double[totalDim];

            int GetPinNode(Pin p) => rootToNodeIndex[FindRoot(p)];

            foreach (var comp in components)
            {
                double r = GetEffectiveResistance(comp);
                bool isResistive = r > 0 && !IsVoltageSource(comp.Type) && comp.Type != ComponentType.CurrentSource1A;

                if (comp.Type == ComponentType.CurrentSource1A)
                {
                    double I = comp.Value;
                    int nA = GetPinNode(comp.Pins[0]);
                    int nB = GetPinNode(comp.Pins[1]);
                    if (nA > 0) z[nA - 1] -= I;
                    if (nB > 0) z[nB - 1] += I;
                }
                else if (isResistive && comp.Pins.Count >= 2)
                {
                    if (r <= 1e-9) r = 1e-9;
                    double g = 1.0 / r;

                    int nodeA = GetPinNode(comp.Pins[0]);
                    int nodeB = GetPinNode(comp.Pins[1]);

                    if (nodeA > 0) A[nodeA - 1, nodeA - 1] += g;
                    if (nodeB > 0) A[nodeB - 1, nodeB - 1] += g;
                    if (nodeA > 0 && nodeB > 0)
                    {
                        A[nodeA - 1, nodeB - 1] -= g;
                        A[nodeB - 1, nodeA - 1] -= g;
                    }
                }
            }

            for (int k = 0; k < m; k++)
            {
                var vs = vSources[k];
                int vIdx = n + k;
                int nodePlus = GetPinNode(vs.Pins[0]);
                int nodeMinus = GetPinNode(vs.Pins[1]);

                if (nodePlus > 0)
                {
                    A[nodePlus - 1, vIdx] += 1;
                    A[vIdx, nodePlus - 1] += 1;
                }
                if (nodeMinus > 0)
                {
                    A[nodeMinus - 1, vIdx] -= 1;
                    A[vIdx, nodeMinus - 1] -= 1;
                }

                z[vIdx] = vs.Value;
            }

            // Стабилизация диагонали
            for (int i = 0; i < n; i++)
            {
                A[i, i] += 1e-12;
            }

            double[] xVec;
            try
            {
                xVec = SolveLinearSystem(A, z, totalDim);
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.Message = "Ошибка расчета: " + ex.Message;
                return res;
            }

            res.NodeVoltages[0] = 0.0;
            for (int i = 0; i < n; i++)
            {
                res.NodeVoltages[i + 1] = xVec[i];
            }

            for (int k = 0; k < m; k++)
            {
                var vs = vSources[k];
                vs.CalculatedCurrent = -xVec[n + k];
                int nPlus = GetPinNode(vs.Pins[0]);
                int nMinus = GetPinNode(vs.Pins[1]);
                double vP = (nPlus == 0) ? 0 : xVec[nPlus - 1];
                double vM = (nMinus == 0) ? 0 : xVec[nMinus - 1];
                vs.CalculatedVoltage = vP - vM;

                res.Reports.Add(new ComponentReport
                {
                    Name = vs.Label,
                    Type = "Источник питания",
                    ParamValue = vs.Value,
                    Unit = "В",
                    Voltage = vs.CalculatedVoltage,
                    Current = vs.CalculatedCurrent,
                    Power = Math.Abs(vs.CalculatedVoltage * vs.CalculatedCurrent)
                });
            }

            foreach (var comp in components)
            {
                if (comp.Pins.Count >= 2 && !IsVoltageSource(comp.Type) && comp.Type != ComponentType.Ground)
                {
                    int nA = GetPinNode(comp.Pins[0]);
                    int nB = GetPinNode(comp.Pins[1]);
                    double vA = (nA == 0) ? 0 : xVec[nA - 1];
                    double vB = (nB == 0) ? 0 : xVec[nB - 1];
                    comp.CalculatedVoltage = vA - vB;

                    if (comp.Type == ComponentType.CurrentSource1A)
                    {
                        comp.CalculatedCurrent = comp.Value;
                    }
                    else
                    {
                        double effR = GetEffectiveResistance(comp);
                        comp.CalculatedCurrent = comp.CalculatedVoltage / (effR <= 1e-9 ? 1e-9 : effR);
                    }

                    res.Reports.Add(new ComponentReport
                    {
                        Name = comp.Label,
                        Type = comp.Category.ToString(),
                        ParamValue = comp.Value,
                        Unit = comp.Unit,
                        Voltage = comp.CalculatedVoltage,
                        Current = comp.CalculatedCurrent,
                        Power = Math.Abs(comp.CalculatedVoltage * comp.CalculatedCurrent)
                    });
                }
            }

            res.Success = true;
            res.Message = "Схема успешно рассчитана!";
            return res;
        }

        public static bool IsVoltageSource(ComponentType t)
        {
            return t == ComponentType.VoltageSource12V ||
                   t == ComponentType.VoltageSource5V ||
                   t == ComponentType.VoltageSource9V ||
                   t == ComponentType.VoltageSource24V;
        }

        public static double GetEffectiveResistance(CircuitComponent comp)
        {
            switch (comp.Category)
            {
                case ComponentCategory.Resistors:
                    return comp.Value;
                case ComponentCategory.Capacitors:
                    return 1e9; // В установившемся DC - разрыв
                case ComponentCategory.Inductors:
                    return 1e-4; // В DC режиме - проводник
                case ComponentCategory.Lighting:
                    return comp.Value > 0 ? comp.Value : 50.0;
                case ComponentCategory.Semiconductors:
                    return 15.0; // Прямое сопротивление диода/перехода
                case ComponentCategory.Meters:
                    if (comp.Type == ComponentType.Voltmeter) return 10_000_000.0;
                    if (comp.Type == ComponentType.Ammeter) return 1e-4;
                    if (comp.Type == ComponentType.Ohmmeter) return 100_000.0;
                    return 1e-4;
                case ComponentCategory.Switches:
                    if (comp.Type == ComponentType.SwitchClosed || comp.Type == ComponentType.PushNormallyClosed) return 1e-4;
                    return 1e9;
                case ComponentCategory.Protection:
                    if (comp.Type.ToString().StartsWith("Fuse")) return 1e-4;
                    return comp.Value > 0 ? comp.Value : 1000.0;
                case ComponentCategory.LogicGates:
                    return 10_000.0;
                default:
                    return comp.Value <= 0 ? 100.0 : comp.Value;
            }
        }

        private static double[] SolveLinearSystem(double[,] A, double[] b, int size)
        {
            double[,] M = (double[,])A.Clone();
            double[] x = (double[])b.Clone();

            for (int k = 0; k < size; k++)
            {
                int maxRow = k;
                double maxVal = Math.Abs(M[k, k]);
                for (int i = k + 1; i < size; i++)
                {
                    if (Math.Abs(M[i, k]) > maxVal)
                    {
                        maxVal = Math.Abs(M[i, k]);
                        maxRow = i;
                    }
                }

                if (maxVal < 1e-14)
                {
                    throw new InvalidOperationException("Цепь содержит изолированные узлы.");
                }

                if (maxRow != k)
                {
                    for (int j = k; j < size; j++)
                    {
                        double tmp = M[k, j];
                        M[k, j] = M[maxRow, j];
                        M[maxRow, j] = tmp;
                    }
                    double tmpB = x[k];
                    x[k] = x[maxRow];
                    x[maxRow] = tmpB;
                }

                for (int i = k + 1; i < size; i++)
                {
                    double factor = M[i, k] / M[k, k];
                    for (int j = k; j < size; j++)
                    {
                        M[i, j] -= factor * M[k, j];
                    }
                    x[i] -= factor * x[k];
                }
            }

            double[] result = new double[size];
            for (int i = size - 1; i >= 0; i--)
            {
                double sum = 0;
                for (int j = i + 1; j < size; j++)
                {
                    sum += M[i, j] * result[j];
                }
                result[i] = (x[i] - sum) / M[i, i];
            }

            return result;
        }
    }
}
