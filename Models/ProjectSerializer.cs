using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CircuitCanvas.Models
{
    public class CircuitProjectDto
    {
        public string Version { get; set; } = "1.0";
        public string Name { get; set; } = "Новая схема";
        public DateTime SavedAt { get; set; } = DateTime.Now;
        public List<ComponentDto> Components { get; set; } = new List<ComponentDto>();
        public List<WireDto> Wires { get; set; } = new List<WireDto>();
        public List<StrokeDto> Strokes { get; set; } = new List<StrokeDto>();
    }

    public class ComponentDto
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public string Label { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int RotationAngle { get; set; }
        public List<PinDto> Pins { get; set; } = new List<PinDto>();
    }

    public class PinDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double RelX { get; set; }
        public double RelY { get; set; }
    }

    public class WireDto
    {
        public string Id { get; set; }
        public string Pin1Id { get; set; }
        public string Pin2Id { get; set; }
    }

    public class StrokeDto
    {
        public List<PointDto> Points { get; set; } = new List<PointDto>();
        public string ColorHex { get; set; } = "#38BDF8";
        public double Thickness { get; set; } = 2.4;
    }

    public class PointDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public static class ProjectSerializer
    {
        public static string SaveToJson(List<CircuitComponent> components, List<Wire> wires, List<Polyline> strokes, string projectName = "Схема")
        {
            var project = new CircuitProjectDto
            {
                Name = projectName,
                SavedAt = DateTime.Now,
                Version = "1.0"
            };

            foreach (var c in components)
            {
                var cDto = new ComponentDto
                {
                    Id = c.Id,
                    Type = c.Type.ToString(),
                    Category = c.Category.ToString(),
                    Label = c.Label,
                    Value = c.Value,
                    Unit = c.Unit,
                    X = c.X,
                    Y = c.Y,
                    Width = c.Width,
                    Height = c.Height,
                    RotationAngle = c.RotationAngle
                };

                foreach (var p in c.Pins)
                {
                    cDto.Pins.Add(new PinDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        RelX = p.RelX,
                        RelY = p.RelY
                    });
                }
                project.Components.Add(cDto);
            }

            foreach (var w in wires)
            {
                if (w.Pin1 != null && w.Pin2 != null)
                {
                    project.Wires.Add(new WireDto
                    {
                        Id = w.Id,
                        Pin1Id = w.Pin1.Id,
                        Pin2Id = w.Pin2.Id
                    });
                }
            }

            foreach (var s in strokes)
            {
                var strokeDto = new StrokeDto
                {
                    Thickness = s.StrokeThickness
                };
                if (s.Stroke is SolidColorBrush scb)
                {
                    strokeDto.ColorHex = $"#{scb.Color.R:X2}{scb.Color.G:X2}{scb.Color.B:X2}";
                }
                foreach (Point pt in s.Points)
                {
                    strokeDto.Points.Add(new PointDto { X = pt.X, Y = pt.Y });
                }
                project.Strokes.Add(strokeDto);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(project, options);
        }

        public static bool LoadFromJson(string json, out List<CircuitComponent> components, out List<Wire> wires, out List<Polyline> strokes)
        {
            components = new List<CircuitComponent>();
            wires = new List<Wire>();
            strokes = new List<Polyline>();

            try
            {
                var project = JsonSerializer.Deserialize<CircuitProjectDto>(json);
                if (project == null) return false;

                var pinLookup = new Dictionary<string, Pin>();

                foreach (var cDto in project.Components)
                {
                    if (Enum.TryParse<ComponentType>(cDto.Type, out var type) &&
                        Enum.TryParse<ComponentCategory>(cDto.Category, out var cat))
                    {
                        var comp = new CircuitComponent(type, cat, cDto.X, cDto.Y, cDto.Label, cDto.Value, cDto.Unit, cDto.Width, cDto.Height)
                        {
                            Id = cDto.Id,
                            RotationAngle = cDto.RotationAngle
                        };

                        comp.Pins.Clear();
                        if (cDto.Pins != null && cDto.Pins.Count > 0)
                        {
                            foreach (var pDto in cDto.Pins)
                            {
                                var pin = new Pin(comp, pDto.Name, pDto.RelX, pDto.RelY)
                                {
                                    Id = pDto.Id
                                };
                                comp.Pins.Add(pin);
                                pinLookup[pin.Id] = pin;
                            }
                        }
                        else
                        {
                            comp.InitPins();
                            foreach (var p in comp.Pins)
                            {
                                pinLookup[p.Id] = p;
                            }
                        }

                        components.Add(comp);
                    }
                }

                if (project.Wires != null)
                {
                    foreach (var wDto in project.Wires)
                    {
                        if (pinLookup.TryGetValue(wDto.Pin1Id, out var p1) &&
                            pinLookup.TryGetValue(wDto.Pin2Id, out var p2))
                        {
                            var wire = new Wire(p1, p2) { Id = wDto.Id };
                            wires.Add(wire);
                        }
                    }
                }

                if (project.Strokes != null)
                {
                    foreach (var sDto in project.Strokes)
                    {
                        Color c = Color.FromRgb(56, 189, 248);
                        try
                        {
                            if (!string.IsNullOrEmpty(sDto.ColorHex))
                                c = (Color)ColorConverter.ConvertFromString(sDto.ColorHex);
                        }
                        catch { }

                        var poly = new Polyline
                        {
                            Stroke = new SolidColorBrush(c),
                            StrokeThickness = sDto.Thickness > 0 ? sDto.Thickness : 2.4,
                            StrokeLineJoin = PenLineJoin.Round,
                            StrokeStartLineCap = PenLineCap.Round,
                            StrokeEndLineCap = PenLineCap.Round
                        };

                        foreach (var ptDto in sDto.Points)
                        {
                            poly.Points.Add(new Point(ptDto.X, ptDto.Y));
                        }
                        strokes.Add(poly);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
