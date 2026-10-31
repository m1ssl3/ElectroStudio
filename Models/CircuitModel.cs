using System;
using System.Collections.Generic;
using System.Linq;

namespace CircuitCanvas.Models
{
    public enum ComponentCategory
    {
        Sources,        // Источники
        Resistors,      // Резисторы
        Capacitors,     // Конденсаторы
        Inductors,      // Индуктивности
        Semiconductors, // Полупроводники
        Lighting,       // Светотехника / Лампы
        Meters,         // Измерительные приборы
        Switches,       // Коммутация / Ключи
        LogicGates,     // Логические элементы
        Protection      // Защита и датчики
    }

    public enum ComponentType
    {
        // 1. Sources (6)
        VoltageSource12V,
        VoltageSource5V,
        VoltageSource9V,
        VoltageSource24V,
        CurrentSource1A,
        Ground,

        // 2. Resistors (8)
        Resistor10,
        Resistor100,
        Resistor220,
        Resistor470,
        Resistor1k,
        Resistor4k7,
        Resistor10k,
        Resistor100k,

        // 3. Capacitors (6)
        Capacitor100nF,
        Capacitor1uF,
        Capacitor10uF,
        Capacitor47uF,
        Capacitor100uF,
        Capacitor1000uF,

        // 4. Inductors (5)
        Inductor10uH,
        Inductor100uH,
        Inductor1mH,
        Inductor10mH,
        Inductor100mH,

        // 5. Semiconductors (6)
        Diode1N4148,
        DiodePower1N4007,
        ZenerDiode3V3,
        ZenerDiode5V1,
        SchottkyDiode,
        TransistorNPN,

        // 6. Lighting / Opto (6)
        BulbStandard,
        BulbHighPower,
        LedRed,
        LedGreen,
        LedBlue,
        LedYellow,

        // 7. Meters (5)
        Voltmeter,
        Ammeter,
        Ohmmeter,
        Wattmeter,
        FrequencyMeter,

        // 8. Switches & Relays (5)
        SwitchClosed,
        SwitchOpen,
        PushNormallyOpen,
        PushNormallyClosed,
        RelayDC,

        // 9. Logic Gates (5)
        LogicAND,
        LogicOR,
        LogicNOT,
        LogicNAND,
        LogicXOR,

        // 10. Protection & Sensors (5)
        Fuse05A,
        Fuse1A,
        Fuse2A,
        ThermistorNTC,
        PhotoresistorLDR
    }

    public class Pin
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public CircuitComponent Parent { get; set; }
        public string Name { get; set; }
        public double RelX { get; set; }
        public double RelY { get; set; }

        public double AbsoluteX => (Parent?.X ?? 0) + RelX;
        public double AbsoluteY => (Parent?.Y ?? 0) + RelY;

        public Pin(CircuitComponent parent, string name, double relX, double relY)
        {
            Parent = parent;
            Name = name;
            RelX = relX;
            RelY = relY;
        }
    }

    public class CircuitComponent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ComponentType Type { get; set; }
        public ComponentCategory Category { get; set; }
        public string Label { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 92;
        public double Height { get; set; } = 48;
        public int RotationAngle { get; set; } = 0; // 0, 90, 180, 270

        public List<Pin> Pins { get; set; } = new List<Pin>();

        // Simulation output
        public double CalculatedVoltage { get; set; }
        public double CalculatedCurrent { get; set; }
        public double CalculatedPower => Math.Abs(CalculatedVoltage * CalculatedCurrent);

        public CircuitComponent(ComponentType type, ComponentCategory cat, double x, double y, string label, double val, string unit, double w = 92, double h = 48)
        {
            Type = type;
            Category = cat;
            X = x;
            Y = y;
            Label = label;
            Value = val;
            Unit = unit;
            Width = w;
            Height = h;
            InitPins();
        }

        public void Rotate()
        {
            RotationAngle = (RotationAngle + 90) % 360;
            UpdatePinPositions();
        }

        public void InitPins()
        {
            Pins.Clear();
            if (Type == ComponentType.Ground)
            {
                Pins.Add(new Pin(this, "GND", 0, 0));
            }
            else
            {
                Pins.Add(new Pin(this, "+", 0, 0));
                Pins.Add(new Pin(this, "-", 0, 0));
            }
            UpdatePinPositions();
        }

        public void UpdatePinPositions()
        {
            if (Pins.Count == 0) return;

            if (Type == ComponentType.Ground)
            {
                if (RotationAngle == 0) { Pins[0].RelX = 0; Pins[0].RelY = -Height / 2; }
                else if (RotationAngle == 90) { Pins[0].RelX = -Width / 2; Pins[0].RelY = 0; }
                else if (RotationAngle == 180) { Pins[0].RelX = 0; Pins[0].RelY = Height / 2; }
                else { Pins[0].RelX = Width / 2; Pins[0].RelY = 0; }
            }
            else if (Pins.Count >= 2)
            {
                double halfW = Width > 0 ? Width / 2 : 46;
                double halfH = Height > 0 ? Height / 2 : 24;

                if (RotationAngle == 0)
                {
                    Pins[0].RelX = -halfW; Pins[0].RelY = 0;
                    Pins[1].RelX = halfW;  Pins[1].RelY = 0;
                }
                else if (RotationAngle == 90)
                {
                    Pins[0].RelX = 0; Pins[0].RelY = -halfH;
                    Pins[1].RelX = 0; Pins[1].RelY = halfH;
                }
                else if (RotationAngle == 180)
                {
                    Pins[0].RelX = halfW;  Pins[0].RelY = 0;
                    Pins[1].RelX = -halfW; Pins[1].RelY = 0;
                }
                else // 270
                {
                    Pins[0].RelX = 0; Pins[0].RelY = halfH;
                    Pins[1].RelX = 0; Pins[1].RelY = -halfH;
                }
            }
        }
    }

    public class Wire
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Pin Pin1 { get; set; }
        public Pin Pin2 { get; set; }

        public Wire(Pin p1, Pin p2)
        {
            Pin1 = p1;
            Pin2 = p2;
        }
    }

    public class FreehandStroke
    {
        public List<System.Windows.Point> Points { get; set; } = new List<System.Windows.Point>();
        public System.Windows.Media.Color Color { get; set; } = System.Windows.Media.Colors.DeepSkyBlue;
        public double Thickness { get; set; } = 2.0;
    }
}
