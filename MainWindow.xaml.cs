using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using CircuitCanvas.Models;

namespace CircuitCanvas
{
    public enum ActiveToolMode
    {
        Circuit,
        Draw,
        Eraser
    }

    public class CatalogItemDef
    {
        public ComponentType Type { get; set; }
        public ComponentCategory Category { get; set; }
        public string Title { get; set; }
        public string DefaultLabel { get; set; }
        public double DefaultValue { get; set; }
        public string Unit { get; set; }
        public string Icon { get; set; }

        public CatalogItemDef(ComponentType type, ComponentCategory cat, string title, string label, double val, string unit, string icon)
        {
            Type = type;
            Category = cat;
            Title = title;
            DefaultLabel = label;
            DefaultValue = val;
            Unit = unit;
            Icon = icon;
        }
    }

    public enum ComponentVisualStyle
    {
        Realistic,
        Schematic,
        GostTextbook
    }

    public partial class MainWindow : Window
    {
        private ActiveToolMode _activeTool = ActiveToolMode.Circuit;
        private bool _isDarkMode = true;
        private ComponentVisualStyle _visualStyle = ComponentVisualStyle.Realistic;

        private List<CircuitComponent> _components = new List<CircuitComponent>();
        private List<Wire> _wires = new List<Wire>();
        private List<Polyline> _drawnStrokes = new List<Polyline>();

        private CircuitComponent _selectedComponent = null;
        private CircuitComponent _draggedComponent = null;
        private Point _dragStartMousePos;
        private Point _compInitialPos;

        private Pin _activeWiringPin = null;
        private Line _tempWireLine = null;

        private Polyline _currentPolyline = null;

        private DispatcherTimer _animationTimer;
        private double _animPhase = 0;
        private bool _simulationRunning = false;

        private Color _canvasBg;
        private Color _gridDotColor;
        private Color _compFill;
        private Color _compBorder;
        private Color _compSelectedBorder;
        private Color _wireColor;
        private Color _textColor;

        // Full 50+ item library definition
        private List<CatalogItemDef> _catalogDefs = new List<CatalogItemDef>();

        public MainWindow()
        {
            InitializeComponent();
            InitCatalogDefinitions();
            PopulateCatalogUI();
            ApplyTheme(isDark: false);
            LoadExample();

            _animationTimer = new DispatcherTimer(DispatcherPriority.Render);
            _animationTimer.Interval = TimeSpan.FromMilliseconds(25);
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationTimer.Start();
        }

        private void InitCatalogDefinitions()
        {
            _catalogDefs = new List<CatalogItemDef>
            {
                // 1. Sources (6)
                new CatalogItemDef(ComponentType.VoltageSource12V, ComponentCategory.Sources, "Батарея 12В (DC)", "Батарея_12В", 12, "В", "🔋"),
                new CatalogItemDef(ComponentType.VoltageSource5V, ComponentCategory.Sources, "Питание 5В (USB)", "Питание_5В", 5, "В", "🔋"),
                new CatalogItemDef(ComponentType.VoltageSource9V, ComponentCategory.Sources, "Крона 9В (DC)", "Крона_9В", 9, "В", "🔋"),
                new CatalogItemDef(ComponentType.VoltageSource24V, ComponentCategory.Sources, "Блок 24В (DC)", "Блок_24В", 24, "В", "🔋"),
                new CatalogItemDef(ComponentType.CurrentSource1A, ComponentCategory.Sources, "Источник тока 1А", "Генератор_тока", 1, "А", "⚡"),
                new CatalogItemDef(ComponentType.Ground, ComponentCategory.Sources, "Земля GND (0В)", "Заземление", 0, "В", "⏚"),

                // 2. Resistors (8)
                new CatalogItemDef(ComponentType.Resistor10, ComponentCategory.Resistors, "Резистор 10 Ом", "Резистор", 10, "Ом", "〰"),
                new CatalogItemDef(ComponentType.Resistor100, ComponentCategory.Resistors, "Резистор 100 Ом", "Резистор", 100, "Ом", "〰"),
                new CatalogItemDef(ComponentType.Resistor220, ComponentCategory.Resistors, "Резистор 220 Ом (LED)", "Резистор", 220, "Ом", "〰"),
                new CatalogItemDef(ComponentType.Resistor470, ComponentCategory.Resistors, "Резистор 470 Ом", "Резистор", 470, "Ом", "〰"),
                new CatalogItemDef(ComponentType.Resistor1k, ComponentCategory.Resistors, "Резистор 1 кОм", "Резистор", 1000, "Ом", "〰"),
                new CatalogItemDef(ComponentType.Resistor4k7, ComponentCategory.Resistors, "Резистор 4.7 кОм", "Резистор", 4700, "Ом", "〰"),
                new CatalogItemDef(ComponentType.Resistor10k, ComponentCategory.Resistors, "Резистор 10 кОм (Подтяжка)", "Резистор", 10000, "Ом", "〰"),
                new CatalogItemDef(ComponentType.Resistor100k, ComponentCategory.Resistors, "Резистор 100 кОм", "Резистор", 100000, "Ом", "〰"),

                // 3. Capacitors (6)
                new CatalogItemDef(ComponentType.Capacitor100nF, ComponentCategory.Capacitors, "Конденсатор 100 нФ", "Конденсатор", 0.1, "мкФ", "╢"),
                new CatalogItemDef(ComponentType.Capacitor1uF, ComponentCategory.Capacitors, "Конденсатор 1 мкФ", "Конденсатор", 1, "мкФ", "╢"),
                new CatalogItemDef(ComponentType.Capacitor10uF, ComponentCategory.Capacitors, "Конденсатор 10 мкФ", "Конденсатор", 10, "мкФ", "╢"),
                new CatalogItemDef(ComponentType.Capacitor47uF, ComponentCategory.Capacitors, "Конденсатор 47 мкФ", "Конденсатор", 47, "мкФ", "╢"),
                new CatalogItemDef(ComponentType.Capacitor100uF, ComponentCategory.Capacitors, "Конденсатор 100 мкФ", "Конденсатор", 100, "мкФ", "╢"),
                new CatalogItemDef(ComponentType.Capacitor1000uF, ComponentCategory.Capacitors, "Конденсатор 1000 мкФ", "Конденсатор", 1000, "мкФ", "╢"),

                // 4. Inductors (5)
                new CatalogItemDef(ComponentType.Inductor10uH, ComponentCategory.Inductors, "Дроссель 10 мкГн", "Катушка_L", 10, "мкГн", "🌀"),
                new CatalogItemDef(ComponentType.Inductor100uH, ComponentCategory.Inductors, "Дроссель 100 мкГн", "Катушка_L", 100, "мкГн", "🌀"),
                new CatalogItemDef(ComponentType.Inductor1mH, ComponentCategory.Inductors, "Катушка 1 мГн", "Катушка_L", 1, "мГн", "🌀"),
                new CatalogItemDef(ComponentType.Inductor10mH, ComponentCategory.Inductors, "Катушка 10 мГн", "Катушка_L", 10, "мГн", "🌀"),
                new CatalogItemDef(ComponentType.Inductor100mH, ComponentCategory.Inductors, "Катушка 100 мГн", "Катушка_L", 100, "мГн", "🌀"),

                // 5. Semiconductors (6)
                new CatalogItemDef(ComponentType.Diode1N4148, ComponentCategory.Semiconductors, "Диод 1N4148", "Диод", 0, "", "⯈"),
                new CatalogItemDef(ComponentType.DiodePower1N4007, ComponentCategory.Semiconductors, "Выпрямитель 1N4007", "Диод", 0, "", "⯈"),
                new CatalogItemDef(ComponentType.ZenerDiode3V3, ComponentCategory.Semiconductors, "Стабилитрон 3.3В", "Стабилитрон", 3.3, "В", "⯈"),
                new CatalogItemDef(ComponentType.ZenerDiode5V1, ComponentCategory.Semiconductors, "Стабилитрон 5.1В", "Стабилитрон", 5.1, "В", "⯈"),
                new CatalogItemDef(ComponentType.SchottkyDiode, ComponentCategory.Semiconductors, "Диод Шоттки SS14", "Диод_Шоттки", 0, "", "⯈"),
                new CatalogItemDef(ComponentType.TransistorNPN, ComponentCategory.Semiconductors, "Транзистор NPN (BC547)", "Транзистор", 100, "hFE", "⏉"),

                // 6. Lighting / Opto (6)
                new CatalogItemDef(ComponentType.BulbStandard, ComponentCategory.Lighting, "Лампа 12В 40Вт", "Лампа", 50, "Ом", "💡"),
                new CatalogItemDef(ComponentType.BulbHighPower, ComponentCategory.Lighting, "Фара / Прожектор 100Вт", "Фара", 20, "Ом", "🔦"),
                new CatalogItemDef(ComponentType.LedRed, ComponentCategory.Lighting, "Светодиод Красный", "Красный_LED", 40, "Ом", "🔴"),
                new CatalogItemDef(ComponentType.LedGreen, ComponentCategory.Lighting, "Светодиод Зеленый", "Зеленый_LED", 40, "Ом", "🟢"),
                new CatalogItemDef(ComponentType.LedBlue, ComponentCategory.Lighting, "Светодиод Синий", "Синий_LED", 40, "Ом", "🔵"),
                new CatalogItemDef(ComponentType.LedYellow, ComponentCategory.Lighting, "Светодиод Желтый", "Желтый_LED", 40, "Ом", "🟡"),

                // 7. Meters (5)
                new CatalogItemDef(ComponentType.Voltmeter, ComponentCategory.Meters, "Вольтметр DC", "Вольтметр", 0, "В", "📊"),
                new CatalogItemDef(ComponentType.Ammeter, ComponentCategory.Meters, "Амперметр DC", "Амперметр", 0, "А", "⏱"),
                new CatalogItemDef(ComponentType.Ohmmeter, ComponentCategory.Meters, "Омметр", "Омметр", 0, "Ом", "⚖"),
                new CatalogItemDef(ComponentType.Wattmeter, ComponentCategory.Meters, "Ваттметр мощности", "Ваттметр", 0, "Вт", "🔌"),
                new CatalogItemDef(ComponentType.FrequencyMeter, ComponentCategory.Meters, "Частотомер / Осцилл.", "Частотомер", 0, "Гц", "📈"),

                // 8. Switches & Relays (4)
                new CatalogItemDef(ComponentType.SwitchClosed, ComponentCategory.Switches, "Ключ (Клик для вкл/выкл)", "Ключ", 1, "", "🔘"),
                new CatalogItemDef(ComponentType.PushNormallyOpen, ComponentCategory.Switches, "Кнопка NO (Норм. Разомк)", "BTN_NO", 0, "", "⏹"),
                new CatalogItemDef(ComponentType.PushNormallyClosed, ComponentCategory.Switches, "Кнопка NC (Норм. Замк)", "BTN_NC", 1, "", "⏺"),
                new CatalogItemDef(ComponentType.RelayDC, ComponentCategory.Switches, "Электромагн. реле 12В", "K_RELAY", 400, "Ом", "🔲"),

                // 9. Logic Gates (5)
                new CatalogItemDef(ComponentType.LogicAND, ComponentCategory.LogicGates, "Логика И (AND 2-вх)", "AND1", 0, "", "🔲"),
                new CatalogItemDef(ComponentType.LogicOR, ComponentCategory.LogicGates, "Логика ИЛИ (OR 2-вх)", "OR1", 0, "", "🔲"),
                new CatalogItemDef(ComponentType.LogicNOT, ComponentCategory.LogicGates, "Инвертор НЕ (NOT)", "NOT1", 0, "", "🔲"),
                new CatalogItemDef(ComponentType.LogicNAND, ComponentCategory.LogicGates, "Логика И-НЕ (NAND)", "NAND1", 0, "", "🔲"),
                new CatalogItemDef(ComponentType.LogicXOR, ComponentCategory.LogicGates, "Искл. ИЛИ (XOR)", "XOR1", 0, "", "🔲"),

                // 10. Protection & Sensors (5)
                new CatalogItemDef(ComponentType.Fuse05A, ComponentCategory.Protection, "Предохранитель 0.5А", "F0.5A", 0.5, "А", "🧯"),
                new CatalogItemDef(ComponentType.Fuse1A, ComponentCategory.Protection, "Предохранитель 1.0А", "F1.0A", 1.0, "А", "🧯"),
                new CatalogItemDef(ComponentType.Fuse2A, ComponentCategory.Protection, "Предохранитель 2.0А", "F2.0A", 2.0, "А", "🧯"),
                new CatalogItemDef(ComponentType.ThermistorNTC, ComponentCategory.Protection, "Терморезистор NTC 10k", "NTC10k", 10000, "Ом", "🌡"),
                new CatalogItemDef(ComponentType.PhotoresistorLDR, ComponentCategory.Protection, "Фоторезистор LDR", "LDR", 5000, "Ом", "☀️")
            };
        }

        private void PopulateCatalogUI(string filter = "")
        {
            if (CatalogItemsContainer == null) return;
            CatalogItemsContainer.Children.Clear();

            var grouped = _catalogDefs
                .Where(d => string.IsNullOrEmpty(filter) || d.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) || d.DefaultLabel.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .GroupBy(d => d.Category);

            foreach (var group in grouped)
            {
                var groupHeader = new TextBlock
                {
                    Text = GetCategoryTitle(group.Key).ToUpper(),
                    FontWeight = FontWeights.Bold,
                    FontSize = 10,
                    Foreground = (Brush)Application.Current.Resources["SubTextBrush"],
                    Margin = new Thickness(4, 10, 4, 4)
                };
                CatalogItemsContainer.Children.Add(groupHeader);

                foreach (var item in group)
                {
                    var btn = new Button
                    {
                        Style = (Style)FindResource("CatalogItemBtn"),
                        Tag = item
                    };

                    var sp = new StackPanel { Orientation = Orientation.Horizontal };
                    var miniPreview = CreateMiniSchemaIcon(item.Category, item.Type);
                    miniPreview.Margin = new Thickness(0, 0, 8, 0);

                    var txt = new TextBlock
                    {
                        Text = item.Title,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold
                    };

                    sp.Children.Add(miniPreview);
                    sp.Children.Add(txt);
                    btn.Content = sp;

                    btn.Click += CatalogItem_Click;
                    CatalogItemsContainer.Children.Add(btn);
                }
            }
        }

        private FrameworkElement CreateMiniSchemaIcon(ComponentCategory cat, ComponentType type)
        {
            var c = new Canvas { Width = 34, Height = 20, VerticalAlignment = VerticalAlignment.Center };
            var brush = Brushes.DodgerBlue;
            double th = 1.5;

            if (cat == ComponentCategory.Lighting)
            {
                // Мини-лампа: круг с крестиком Х
                var cir = new Ellipse { Width = 14, Height = 14, Stroke = brush, StrokeThickness = th };
                Canvas.SetLeft(cir, 10); Canvas.SetTop(cir, 3);
                var l1 = new Line { X1 = 12, Y1 = 5, X2 = 22, Y2 = 15, Stroke = brush, StrokeThickness = th };
                var l2 = new Line { X1 = 12, Y1 = 15, X2 = 22, Y2 = 5, Stroke = brush, StrokeThickness = th };
                var w1 = new Line { X1 = 0, Y1 = 10, X2 = 10, Y2 = 10, Stroke = brush, StrokeThickness = th };
                var w2 = new Line { X1 = 24, Y1 = 10, X2 = 34, Y2 = 10, Stroke = brush, StrokeThickness = th };
                c.Children.Add(cir); c.Children.Add(l1); c.Children.Add(l2); c.Children.Add(w1); c.Children.Add(w2);
            }
            else if (cat == ComponentCategory.Resistors)
            {
                // Мини-резистор: прямоугольник
                var r = new Rectangle { Width = 18, Height = 10, Stroke = brush, StrokeThickness = th };
                Canvas.SetLeft(r, 8); Canvas.SetTop(r, 5);
                var w1 = new Line { X1 = 0, Y1 = 10, X2 = 8, Y2 = 10, Stroke = brush, StrokeThickness = th };
                var w2 = new Line { X1 = 26, Y1 = 10, X2 = 34, Y2 = 10, Stroke = brush, StrokeThickness = th };
                c.Children.Add(r); c.Children.Add(w1); c.Children.Add(w2);
            }
            else if (cat == ComponentCategory.Capacitors)
            {
                // Конденсатор: ||
                var p1 = new Line { X1 = 14, Y1 = 3, X2 = 14, Y2 = 17, Stroke = brush, StrokeThickness = th + 0.5 };
                var p2 = new Line { X1 = 20, Y1 = 3, X2 = 20, Y2 = 17, Stroke = brush, StrokeThickness = th + 0.5 };
                var w1 = new Line { X1 = 0, Y1 = 10, X2 = 14, Y2 = 10, Stroke = brush, StrokeThickness = th };
                var w2 = new Line { X1 = 20, Y1 = 10, X2 = 34, Y2 = 10, Stroke = brush, StrokeThickness = th };
                c.Children.Add(p1); c.Children.Add(p2); c.Children.Add(w1); c.Children.Add(w2);
            }
            else if (cat == ComponentCategory.Inductors)
            {
                // Индуктивность: дуги
                var path = new System.Windows.Shapes.Path
                {
                    Stroke = brush, StrokeThickness = th,
                    Data = Geometry.Parse("M 4,10 A 3,3 0 0 1 10,10 A 3,3 0 0 1 16,10 A 3,3 0 0 1 22,10 A 3,3 0 0 1 28,10")
                };
                var w1 = new Line { X1 = 0, Y1 = 10, X2 = 4, Y2 = 10, Stroke = brush, StrokeThickness = th };
                var w2 = new Line { X1 = 28, Y1 = 10, X2 = 34, Y2 = 10, Stroke = brush, StrokeThickness = th };
                c.Children.Add(path); c.Children.Add(w1); c.Children.Add(w2);
            }
            else if (cat == ComponentCategory.Sources)
            {
                // Источник: круг со стрелкой
                var cir = new Ellipse { Width = 14, Height = 14, Stroke = brush, StrokeThickness = th };
                Canvas.SetLeft(cir, 10); Canvas.SetTop(cir, 3);
                var arr = new Line { X1 = 17, Y1 = 15, X2 = 17, Y2 = 5, Stroke = brush, StrokeThickness = th };
                var arrH1 = new Line { X1 = 17, Y1 = 5, X2 = 14, Y2 = 8, Stroke = brush, StrokeThickness = th };
                var arrH2 = new Line { X1 = 17, Y1 = 5, X2 = 20, Y2 = 8, Stroke = brush, StrokeThickness = th };
                var w1 = new Line { X1 = 0, Y1 = 10, X2 = 10, Y2 = 10, Stroke = brush, StrokeThickness = th };
                var w2 = new Line { X1 = 24, Y1 = 10, X2 = 34, Y2 = 10, Stroke = brush, StrokeThickness = th };
                c.Children.Add(cir); c.Children.Add(arr); c.Children.Add(arrH1); c.Children.Add(arrH2); c.Children.Add(w1); c.Children.Add(w2);
            }
            else if (cat == ComponentCategory.Meters)
            {
                // Прибор: круг со стрелкой
                var cir = new Ellipse { Width = 14, Height = 14, Stroke = brush, StrokeThickness = th };
                Canvas.SetLeft(cir, 10); Canvas.SetTop(cir, 3);
                var txt = new TextBlock { Text = type == ComponentType.Ammeter ? "A" : "V", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = brush };
                Canvas.SetLeft(txt, 14); Canvas.SetTop(txt, 3);
                var w1 = new Line { X1 = 0, Y1 = 10, X2 = 10, Y2 = 10, Stroke = brush, StrokeThickness = th };
                var w2 = new Line { X1 = 24, Y1 = 10, X2 = 34, Y2 = 10, Stroke = brush, StrokeThickness = th };
                c.Children.Add(cir); c.Children.Add(txt); c.Children.Add(w1); c.Children.Add(w2);
            }
            else
            {
                // Дефолтная чертежная рамка
                var r = new Rectangle { Width = 18, Height = 10, Stroke = brush, StrokeThickness = th };
                Canvas.SetLeft(r, 8); Canvas.SetTop(r, 5);
                var w1 = new Line { X1 = 0, Y1 = 10, X2 = 8, Y2 = 10, Stroke = brush, StrokeThickness = th };
                var w2 = new Line { X1 = 26, Y1 = 10, X2 = 34, Y2 = 10, Stroke = brush, StrokeThickness = th };
                c.Children.Add(r); c.Children.Add(w1); c.Children.Add(w2);
            }

            return c;
        }

        private string GetCategoryTitle(ComponentCategory cat)
        {
            return cat switch
            {
                ComponentCategory.Sources => "Источники энергии (DC / Ток)",
                ComponentCategory.Resistors => "Резисторы постоянные",
                ComponentCategory.Capacitors => "Конденсаторы",
                ComponentCategory.Inductors => "Катушки индуктивности и дроссели",
                ComponentCategory.Semiconductors => "Полупроводники и диоды",
                ComponentCategory.Lighting => "Освещение и LED индикация",
                ComponentCategory.Meters => "Измерительные приборы",
                ComponentCategory.Switches => "Коммутация и реле",
                ComponentCategory.LogicGates => "Цифровая логика (0 / 1)",
                ComponentCategory.Protection => "Защита и датчики",
                _ => "Компоненты"
            };
        }

        private void CatalogSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PopulateCatalogUI(CatalogSearchBox.Text.Trim());
        }

        private void CatalogItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is CatalogItemDef def)
            {
                AddComponentFromDef(def);
            }
        }

        private void AddComponentFromDef(CatalogItemDef def)
        {
            int count = _components.Count(c => c.Type == def.Type) + 1;
            double posX = 280 + (_components.Count % 5) * 55;
            double posY = 180 + (_components.Count % 6) * 45;

            var comp = new CircuitComponent(def.Type, def.Category, posX, posY, $"{def.DefaultLabel}{count}", def.DefaultValue, def.Unit);
            _components.Add(comp);
            _selectedComponent = comp;
            UpdateInspector();
            RedrawAll();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (!_simulationRunning) return;
            _animPhase = (_animPhase + 0.05) % 1.0;
            RedrawAll();
        }

        // ================= THEME =================
        private void ApplyTheme(bool isDark)
        {
            _isDarkMode = isDark;
            var res = Application.Current.Resources;

            if (isDark)
            {
                _canvasBg = Color.FromRgb(17, 20, 24);
                _gridDotColor = Color.FromArgb(30, 255, 255, 255);
                _compFill = Color.FromRgb(26, 29, 36);
                _compBorder = Color.FromRgb(45, 50, 62);
                _compSelectedBorder = Color.FromRgb(59, 130, 246);
                _wireColor = Color.FromRgb(52, 211, 153);
                _textColor = Color.FromRgb(243, 244, 246);

                res["WindowBgBrush"] = new SolidColorBrush(Color.FromRgb(17, 20, 24));
                res["HeaderBgBrush"] = new SolidColorBrush(Color.FromRgb(22, 25, 32));
                res["CanvasBgBrush"] = new SolidColorBrush(_canvasBg);
                res["SideBgBrush"] = new SolidColorBrush(Color.FromRgb(22, 25, 32));
                res["BottomBgBrush"] = new SolidColorBrush(Color.FromRgb(20, 23, 29));
                res["CardBgBrush"] = new SolidColorBrush(Color.FromRgb(29, 33, 42));
                res["SegmentBgBrush"] = new SolidColorBrush(Color.FromRgb(16, 18, 23));
                res["HoverBgBrush"] = new SolidColorBrush(Color.FromRgb(38, 43, 55));
                res["InputBgBrush"] = new SolidColorBrush(Color.FromRgb(16, 18, 23));
                res["AltRowBgBrush"] = new SolidColorBrush(Color.FromRgb(25, 28, 36));
                res["BorderBrush"] = new SolidColorBrush(Color.FromRgb(45, 50, 62));
                res["TextBrush"] = new SolidColorBrush(_textColor);
                res["SubTextBrush"] = new SolidColorBrush(Color.FromRgb(156, 163, 175));
                res["AccentBrush"] = new SolidColorBrush(Color.FromRgb(59, 130, 246));

                Background = new SolidColorBrush(_canvasBg);
                Foreground = new SolidColorBrush(_textColor);
            }
            else
            {
                _canvasBg = Color.FromRgb(248, 250, 252);
                _gridDotColor = Color.FromArgb(32, 0, 0, 0);
                _compFill = Color.FromRgb(255, 255, 255);
                _compBorder = Color.FromRgb(226, 232, 240);
                _compSelectedBorder = Color.FromRgb(37, 99, 235);
                _wireColor = Color.FromRgb(16, 185, 129);
                _textColor = Color.FromRgb(30, 41, 59);

                res["WindowBgBrush"] = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                res["HeaderBgBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                res["CanvasBgBrush"] = new SolidColorBrush(_canvasBg);
                res["SideBgBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                res["BottomBgBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                res["CardBgBrush"] = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                res["SegmentBgBrush"] = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                res["HoverBgBrush"] = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                res["InputBgBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                res["AltRowBgBrush"] = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                res["BorderBrush"] = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                res["TextBrush"] = new SolidColorBrush(_textColor);
                res["SubTextBrush"] = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                res["AccentBrush"] = new SolidColorBrush(Color.FromRgb(37, 99, 235));

                Background = new SolidColorBrush(_canvasBg);
                Foreground = new SolidColorBrush(_textColor);
            }

            RedrawAll();
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeSelector == null || !IsLoaded) return;
            ApplyTheme(ThemeSelector.SelectedIndex == 1); // 0 = Light, 1 = Dark
        }

        private void ToggleCatalog_Click(object sender, RoutedEventArgs e)
        {
            CatalogPanel.Visibility = (CatalogPanel.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ModeInteract_Checked(object sender, RoutedEventArgs e) => _activeTool = ActiveToolMode.Circuit;
        private void ModeDraw_Checked(object sender, RoutedEventArgs e) => _activeTool = ActiveToolMode.Draw;
        private void ModeErase_Checked(object sender, RoutedEventArgs e) => _activeTool = ActiveToolMode.Eraser;

        private void RenderSchematic_Checked(object sender, RoutedEventArgs e)
        {
            _visualStyle = ComponentVisualStyle.Schematic;
            RedrawAll();
        }

        private void RenderRealistic_Checked(object sender, RoutedEventArgs e)
        {
            _visualStyle = ComponentVisualStyle.Realistic;
            RedrawAll();
        }

        private void RenderGost_Checked(object sender, RoutedEventArgs e)
        {
            _visualStyle = ComponentVisualStyle.GostTextbook;
            RedrawAll();
        }

        // ================= RENDERING =================
        private void RedrawAll()
        {
            if (MainCanvas == null) return;
            MainCanvas.Children.Clear();

            // Grid
            var gridPen = new SolidColorBrush(_gridDotColor);
            for (int x = 0; x < 2800; x += 40)
            {
                var l = new Line { X1 = x, Y1 = 0, X2 = x, Y2 = 2000, Stroke = gridPen, StrokeThickness = 1 };
                MainCanvas.Children.Add(l);
            }
            for (int y = 0; y < 2000; y += 40)
            {
                var l = new Line { X1 = 0, Y1 = y, X2 = 2800, Y2 = y, Stroke = gridPen, StrokeThickness = 1 };
                MainCanvas.Children.Add(l);
            }

            // Strokes
            foreach (var stroke in _drawnStrokes)
            {
                MainCanvas.Children.Add(stroke);
            }

            // Wires
            foreach (var wire in _wires)
            {
                DrawWire(wire);
            }

            // Components
            foreach (var comp in _components)
            {
                if (_visualStyle == ComponentVisualStyle.Realistic)
                    DrawRealisticComponent(comp);
                else if (_visualStyle == ComponentVisualStyle.GostTextbook)
                    DrawGostComponent(comp);
                else
                    DrawSchematicComponent(comp);
            }

            if (_tempWireLine != null)
            {
                MainCanvas.Children.Add(_tempWireLine);
            }
        }

        private void DrawWire(Wire wire)
        {
            double x1 = wire.Pin1.AbsoluteX;
            double y1 = wire.Pin1.AbsoluteY;
            double x2 = wire.Pin2.AbsoluteX;
            double y2 = wire.Pin2.AbsoluteY;

            var baseLine = new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = new SolidColorBrush(_wireColor),
                StrokeThickness = 3.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Cursor = Cursors.Hand,
                Tag = wire
            };

            baseLine.MouseDown += (s, e) =>
            {
                if (_activeTool == ActiveToolMode.Circuit && e.RightButton == MouseButtonState.Pressed)
                {
                    _wires.Remove(wire);
                    RedrawAll();
                    e.Handled = true;
                }
            };
            MainCanvas.Children.Add(baseLine);

            if (_simulationRunning)
            {
                double dx = x2 - x1;
                double dy = y2 - y1;
                double length = Math.Sqrt(dx * dx + dy * dy);

                if (length > 20)
                {
                    int dotCount = Math.Max(2, (int)(length / 35));
                    for (int i = 0; i < dotCount; i++)
                    {
                        double t = (i / (double)dotCount + _animPhase) % 1.0;
                        double curX = x1 + dx * t;
                        double curY = y1 + dy * t;

                        var electronDot = new Ellipse
                        {
                            Width = 7,
                            Height = 7,
                            Fill = new SolidColorBrush(Color.FromRgb(255, 235, 59)),
                            Effect = new DropShadowEffect
                            {
                                Color = Color.FromRgb(255, 235, 59),
                                BlurRadius = 8,
                                ShadowDepth = 0,
                                Opacity = 0.9
                            }
                        };
                        Canvas.SetLeft(electronDot, curX - 3.5);
                        Canvas.SetTop(electronDot, curY - 3.5);
                        MainCanvas.Children.Add(electronDot);
                    }
                }
            }
        }

        private void DrawRealisticComponent(CircuitComponent comp)
        {
            bool isSel = comp == _selectedComponent;

            var container = new Border
            {
                Width = comp.Width,
                Height = comp.Height,
                Background = new SolidColorBrush(_compFill),
                BorderBrush = new SolidColorBrush(isSel ? _compSelectedBorder : _compBorder),
                BorderThickness = new Thickness(isSel ? 2.5 : 1.2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.SizeAll,
                RenderTransform = new RotateTransform(comp.RotationAngle, comp.Width / 2, comp.Height / 2),
                Effect = new DropShadowEffect
                {
                    BlurRadius = isSel ? 14 : 8,
                    Color = isSel ? _compSelectedBorder : Colors.Black,
                    Opacity = isSel ? 0.6 : 0.2,
                    ShadowDepth = 2
                }
            };

            var grid = new Grid { IsHitTestVisible = false };

            switch (comp.Category)
            {
                case ComponentCategory.Lighting:
                    bool isGlowing = _simulationRunning && comp.CalculatedPower > 0.001;
                    Color glow = comp.Type switch
                    {
                        ComponentType.LedRed => Color.FromRgb(255, 60, 60),
                        ComponentType.LedGreen => Color.FromRgb(60, 230, 80),
                        ComponentType.LedBlue => Color.FromRgb(60, 150, 255),
                        ComponentType.LedYellow => Color.FromRgb(255, 230, 40),
                        _ => Color.FromRgb(255, 220, 60)
                    };

                    var circle = new Ellipse
                    {
                        Width = 24, Height = 24,
                        Fill = new SolidColorBrush(isGlowing ? glow : Color.FromRgb(140, 140, 140)),
                        Stroke = Brushes.DimGray,
                        StrokeThickness = 1.5,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Effect = isGlowing ? new DropShadowEffect { Color = glow, BlurRadius = 24, ShadowDepth = 0, Opacity = 1.0 } : null
                    };
                    var bulbText = new TextBlock
                    {
                        Text = isGlowing ? $"{comp.CalculatedPower:F1}W" : comp.Label,
                        FontSize = 9, FontWeight = FontWeights.Bold,
                        Foreground = isGlowing ? Brushes.Gold : new SolidColorBrush(_textColor),
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 0, 2)
                    };
                    grid.Children.Add(circle);
                    grid.Children.Add(bulbText);
                    break;

                case ComponentCategory.Meters:
                    var mPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                    var mTitle = new TextBlock { Text = comp.Label, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brushes.DeepSkyBlue, HorizontalAlignment = HorizontalAlignment.Center };
                    string displayVal = comp.Type switch
                    {
                        ComponentType.Voltmeter => _simulationRunning ? $"{Math.Abs(comp.CalculatedVoltage):F2} V" : "[ V ]",
                        ComponentType.Ammeter => _simulationRunning ? $"{Math.Abs(comp.CalculatedCurrent):F3} A" : "[ A ]",
                        ComponentType.Wattmeter => _simulationRunning ? $"{comp.CalculatedPower:F2} W" : "[ W ]",
                        _ => "[ METER ]"
                    };
                    var mVal = new TextBlock { Text = displayVal, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(_textColor), HorizontalAlignment = HorizontalAlignment.Center };
                    mPanel.Children.Add(mTitle);
                    mPanel.Children.Add(mVal);
                    grid.Children.Add(mPanel);
                    break;

                default:
                    var defPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                    var defLbl = new TextBlock { Text = comp.Label, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = new SolidColorBrush(_textColor), HorizontalAlignment = HorizontalAlignment.Center };
                    var defVal = new TextBlock { Text = $"{comp.Value} {comp.Unit}", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(140, 170, 230)), HorizontalAlignment = HorizontalAlignment.Center };
                    defPanel.Children.Add(defLbl);
                    defPanel.Children.Add(defVal);
                    grid.Children.Add(defPanel);
                    break;
            }

            container.Child = grid;
            Canvas.SetLeft(container, comp.X - comp.Width / 2);
            Canvas.SetTop(container, comp.Y - comp.Height / 2);
            MainCanvas.Children.Add(container);

            DrawPins(comp);
        }

        private void DrawSchematicComponent(CircuitComponent comp)
        {
            bool isSel = comp == _selectedComponent;

            var border = new Border
            {
                Width = comp.Width, Height = comp.Height,
                Background = new SolidColorBrush(_compFill),
                BorderBrush = new SolidColorBrush(isSel ? _compSelectedBorder : _compBorder),
                BorderThickness = new Thickness(isSel ? 2 : 1),
                CornerRadius = new CornerRadius(5),
                Cursor = Cursors.SizeAll,
                RenderTransform = new RotateTransform(comp.RotationAngle, comp.Width / 2, comp.Height / 2)
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
            var lbl = new TextBlock { Text = comp.Label, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(_textColor), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center };
            var val = new TextBlock { Text = $"{comp.Value} {comp.Unit}", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(120, 160, 220)), HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(lbl);
            stack.Children.Add(val);
            border.Child = stack;

            Canvas.SetLeft(border, comp.X - comp.Width / 2);
            Canvas.SetTop(border, comp.Y - comp.Height / 2);
            MainCanvas.Children.Add(border);

            DrawPins(comp);
        }

        // --- GOST / TEXTBOOK BLACK & WHITE SCIENTIFIC SCHEMATIC (ВИД КАК ИЗ УЧЕБНИКА) ---
        private void DrawGostComponent(CircuitComponent comp)
        {
            bool isSel = comp == _selectedComponent;
            Brush strokeBrush = isSel ? Brushes.DodgerBlue : (_isDarkMode ? Brushes.White : Brushes.Black);
            double strokeThick = isSel ? 3.0 : 2.5;

            var canvasItem = new Canvas
            {
                Width = comp.Width,
                Height = comp.Height,
                Cursor = Cursors.SizeAll,
                RenderTransform = new RotateTransform(comp.RotationAngle, comp.Width / 2, comp.Height / 2)
            };

            // White / dark backing to cover wires underneath
            var bgRect = new Rectangle
            {
                Width = comp.Width,
                Height = comp.Height,
                Fill = new SolidColorBrush(_canvasBg)
            };
            canvasItem.Children.Add(bgRect);

            // Отрисовка по ГОСТ / Учебнику:
            if (comp.Category == ComponentCategory.Lighting)
            {
                // ЛАМПА ПО ГОСТ (Круг с диагональным крестом Х внутри, как на скриншоте!)
                double d = Math.Min(comp.Width, comp.Height) - 8;
                double cx = comp.Width / 2;
                double cy = comp.Height / 2;

                // Линии подводящих проводов к кругу
                var wLeft = new Line { X1 = 0, Y1 = cy, X2 = cx - d / 2, Y2 = cy, Stroke = strokeBrush, StrokeThickness = strokeThick };
                var wRight = new Line { X1 = cx + d / 2, Y1 = cy, X2 = comp.Width, Y2 = cy, Stroke = strokeBrush, StrokeThickness = strokeThick };
                canvasItem.Children.Add(wLeft);
                canvasItem.Children.Add(wRight);

                bool isGlowing = _simulationRunning && comp.CalculatedPower > 0.001;

                var circle = new Ellipse
                {
                    Width = d,
                    Height = d,
                    Stroke = strokeBrush,
                    StrokeThickness = strokeThick,
                    Fill = isGlowing ? new SolidColorBrush(Color.FromArgb(100, 255, 230, 60)) : new SolidColorBrush(_canvasBg),
                    Effect = isGlowing ? new DropShadowEffect { Color = Colors.Gold, BlurRadius = 24, ShadowDepth = 0, Opacity = 0.95 } : null
                };
                Canvas.SetLeft(circle, cx - d / 2);
                Canvas.SetTop(circle, cy - d / 2);
                canvasItem.Children.Add(circle);

                // Диагональный крест Х внутри окружности:
                double r = d / 2;
                double offset = r * 0.7071; // cos(45°) * r
                var xLine1 = new Line { X1 = cx - offset, Y1 = cy - offset, X2 = cx + offset, Y2 = cy + offset, Stroke = strokeBrush, StrokeThickness = strokeThick };
                var xLine2 = new Line { X1 = cx - offset, Y1 = cy + offset, X2 = cx + offset, Y2 = cy - offset, Stroke = strokeBrush, StrokeThickness = strokeThick };
                canvasItem.Children.Add(xLine1);
                canvasItem.Children.Add(xLine2);

                // Обозначение
                var lbl = new TextBlock
                {
                    Text = comp.Label,
                    FontFamily = new FontFamily("Times New Roman, Cambria, Serif"),
                    FontStyle = FontStyles.Italic,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = strokeBrush
                };
                Canvas.SetLeft(lbl, cx + d / 2 + 3);
                Canvas.SetTop(lbl, cy - 20);
                canvasItem.Children.Add(lbl);
            }
            else if (comp.Category == ComponentCategory.Inductors)
            {
                // КАТУШКА ИНДУКТИВНОСТИ ПО ГОСТ (4 дуги / завитка)
                double startX = 8;
                double cy = comp.Height / 2;
                double arcW = (comp.Width - 16) / 4.0;

                var wireL = new Line { X1 = 0, Y1 = cy, X2 = startX, Y2 = cy, Stroke = strokeBrush, StrokeThickness = strokeThick };
                var wireR = new Line { X1 = comp.Width - 8, Y1 = cy, X2 = comp.Width, Y2 = cy, Stroke = strokeBrush, StrokeThickness = strokeThick };
                canvasItem.Children.Add(wireL);
                canvasItem.Children.Add(wireR);

                for (int i = 0; i < 4; i++)
                {
                    double xArc = startX + i * arcW;
                    var path = new System.Windows.Shapes.Path
                    {
                        Stroke = strokeBrush,
                        StrokeThickness = strokeThick,
                        Data = Geometry.Parse($"M {xArc},{cy} A {arcW / 2},{arcW / 2} 0 0 1 {xArc + arcW},{cy}")
                    };
                    canvasItem.Children.Add(path);
                }

                var lbl = new TextBlock
                {
                    Text = "L",
                    FontFamily = new FontFamily("Times New Roman, Cambria, Serif"),
                    FontStyle = FontStyles.Italic,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = strokeBrush
                };
                Canvas.SetLeft(lbl, comp.Width / 2 - 6);
                Canvas.SetTop(lbl, cy - 24);
                canvasItem.Children.Add(lbl);
            }
            else if (comp.Category == ComponentCategory.Capacitors)
            {
                // КОНДЕНСАТОР ПО ГОСТ (Две параллельные обкладки)
                double cx = comp.Width / 2;
                double cy = comp.Height / 2;
                double gap = 6;
                double h = comp.Height - 14;

                var wireL = new Line { X1 = 0, Y1 = cy, X2 = cx - gap, Y2 = cy, Stroke = strokeBrush, StrokeThickness = strokeThick };
                var wireR = new Line { X1 = cx + gap, Y1 = cy, X2 = comp.Width, Y2 = cy, Stroke = strokeBrush, StrokeThickness = strokeThick };
                var plateL = new Line { X1 = cx - gap, Y1 = cy - h / 2, X2 = cx - gap, Y2 = cy + h / 2, Stroke = strokeBrush, StrokeThickness = strokeThick + 0.5 };
                var plateR = new Line { X1 = cx + gap, Y1 = cy - h / 2, X2 = cx + gap, Y2 = cy + h / 2, Stroke = strokeBrush, StrokeThickness = strokeThick + 0.5 };

                canvasItem.Children.Add(wireL);
                canvasItem.Children.Add(wireR);
                canvasItem.Children.Add(plateL);
                canvasItem.Children.Add(plateR);

                var lbl = new TextBlock
                {
                    Text = "C",
                    FontFamily = new FontFamily("Times New Roman, Cambria, Serif"),
                    FontStyle = FontStyles.Italic,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = strokeBrush
                };
                Canvas.SetLeft(lbl, cx + gap + 4);
                Canvas.SetTop(lbl, cy - 22);
                canvasItem.Children.Add(lbl);
            }
            else if (comp.Category == ComponentCategory.Semiconductors && comp.Type != ComponentType.TransistorNPN)
            {
                // ДИОД ПО ГОСТ (Треугольник с чертой)
                double cx = comp.Width / 2;
                double cy = comp.Height / 2;
                double dSize = 14;

                var wireL = new Line { X1 = 0, Y1 = cy, X2 = cx - dSize, Y2 = cy, Stroke = strokeBrush, StrokeThickness = strokeThick };
                var wireR = new Line { X1 = cx + dSize, Y1 = cy, X2 = comp.Width, Y2 = cy, Stroke = strokeBrush, StrokeThickness = strokeThick };
                canvasItem.Children.Add(wireL);
                canvasItem.Children.Add(wireR);

                var tri = new Polygon
                {
                    Points = new PointCollection { new Point(cx - dSize, cy - dSize), new Point(cx - dSize, cy + dSize), new Point(cx + dSize, cy) },
                    Stroke = strokeBrush,
                    StrokeThickness = strokeThick,
                    Fill = strokeBrush
                };
                var bar = new Line { X1 = cx + dSize, Y1 = cy - dSize, X2 = cx + dSize, Y2 = cy + dSize, Stroke = strokeBrush, StrokeThickness = strokeThick + 0.5 };

                canvasItem.Children.Add(tri);
                canvasItem.Children.Add(bar);

                var lbl = new TextBlock
                {
                    Text = "VD",
                    FontFamily = new FontFamily("Times New Roman, Cambria, Serif"),
                    FontStyle = FontStyles.Italic,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = strokeBrush
                };
                Canvas.SetLeft(lbl, cx - 12);
                Canvas.SetTop(lbl, cy - 25);
                canvasItem.Children.Add(lbl);
            }
            else if (comp.Category == ComponentCategory.Meters)
            {
                // ИЗМЕРИТЕЛЬ ПО ГОСТ (Круг с буквой V, A, W внутри)
                double d = Math.Min(comp.Width, comp.Height) - 8;
                double cx = comp.Width / 2;
                double cy = comp.Height / 2;

                var wLeft = new Line { X1 = 0, Y1 = cy, X2 = cx - d / 2, Y2 = cy, Stroke = strokeBrush, StrokeThickness = strokeThick };
                var wRight = new Line { X1 = cx + d / 2, Y1 = cy, X2 = comp.Width, Y2 = cy, Stroke = strokeBrush, StrokeThickness = strokeThick };
                canvasItem.Children.Add(wLeft);
                canvasItem.Children.Add(wRight);

                var circle = new Ellipse { Width = d, Height = d, Stroke = strokeBrush, StrokeThickness = strokeThick, Fill = new SolidColorBrush(_canvasBg) };
                Canvas.SetLeft(circle, cx - d / 2);
                Canvas.SetTop(circle, cy - d / 2);
                canvasItem.Children.Add(circle);

                string letter = comp.Type switch
                {
                    ComponentType.Voltmeter => "V",
                    ComponentType.Ammeter => "A",
                    ComponentType.Wattmeter => "W",
                    ComponentType.Ohmmeter => "Ω",
                    _ => "M"
                };

                var txt = new TextBlock
                {
                    Text = letter,
                    FontFamily = new FontFamily("Times New Roman, Cambria, Serif"),
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = strokeBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Canvas.SetLeft(txt, cx - 7);
                Canvas.SetTop(txt, cy - 14);
                canvasItem.Children.Add(txt);
            }
            else if (comp.Category == ComponentCategory.Switches)
            {
                // КЛЮЧ / ВЫКЛЮЧАТЕЛЬ ПО ГОСТ
                double cx = comp.Width / 2;
                double cy = comp.Height / 2;

                var wLeft = new Line { X1 = 0, Y1 = cy, X2 = cx - 14, Y2 = cy, Stroke = strokeBrush, StrokeThickness = strokeThick };
                var wRight = new Line { X1 = cx + 14, Y1 = cy, X2 = comp.Width, Y2 = cy, Stroke = strokeBrush, StrokeThickness = strokeThick };
                canvasItem.Children.Add(wLeft);
                canvasItem.Children.Add(wRight);

                var contactL = new Ellipse { Width = 6, Height = 6, Stroke = strokeBrush, StrokeThickness = 1.5, Fill = strokeBrush };
                var contactR = new Ellipse { Width = 6, Height = 6, Stroke = strokeBrush, StrokeThickness = 1.5, Fill = strokeBrush };
                Canvas.SetLeft(contactL, cx - 14 - 3); Canvas.SetTop(contactL, cy - 3);
                Canvas.SetLeft(contactR, cx + 14 - 3); Canvas.SetTop(contactR, cy - 3);
                canvasItem.Children.Add(contactL);
                canvasItem.Children.Add(contactR);

                bool isOpen = comp.Type == ComponentType.SwitchOpen || comp.Type == ComponentType.PushNormallyOpen;
                var lever = new Line
                {
                    X1 = cx - 14, Y1 = cy,
                    X2 = cx + 12, Y2 = isOpen ? (cy - 16) : cy,
                    Stroke = strokeBrush, StrokeThickness = strokeThick
                };
                canvasItem.Children.Add(lever);

                var lbl = new TextBlock { Text = "SA", FontFamily = new FontFamily("Times New Roman"), FontStyle = FontStyles.Italic, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = strokeBrush };
                Canvas.SetLeft(lbl, cx - 10); Canvas.SetTop(lbl, cy - 25);
                canvasItem.Children.Add(lbl);
            }
            else if (MnaSolver.IsVoltageSource(comp.Type) || comp.Type == ComponentType.CurrentSource1A)
            {
                // Круг с длинной стрелкой внутри (Классический источник ЭДС E или I)
                double diameter = Math.Min(comp.Width, comp.Height) - 6;
                var circle = new Ellipse
                {
                    Width = diameter,
                    Height = diameter,
                    Stroke = strokeBrush,
                    StrokeThickness = strokeThick,
                    Fill = new SolidColorBrush(_canvasBg)
                };
                Canvas.SetLeft(circle, (comp.Width - diameter) / 2);
                Canvas.SetTop(circle, (comp.Height - diameter) / 2);
                canvasItem.Children.Add(circle);

                // Стрелка внутри круга (направление ЭДС)
                double cx = comp.Width / 2;
                double cy = comp.Height / 2;
                var arrowLine = new Line
                {
                    X1 = cx, Y1 = cy + diameter / 2 - 5,
                    X2 = cx, Y2 = cy - diameter / 2 + 5,
                    Stroke = strokeBrush,
                    StrokeThickness = strokeThick
                };
                var arrowHead1 = new Line { X1 = cx, Y1 = cy - diameter / 2 + 5, X2 = cx - 5, Y2 = cy - diameter / 2 + 13, Stroke = strokeBrush, StrokeThickness = strokeThick };
                var arrowHead2 = new Line { X1 = cx, Y1 = cy - diameter / 2 + 5, X2 = cx + 5, Y2 = cy - diameter / 2 + 13, Stroke = strokeBrush, StrokeThickness = strokeThick };
                canvasItem.Children.Add(arrowLine);
                canvasItem.Children.Add(arrowHead1);
                canvasItem.Children.Add(arrowHead2);

                // Обозначение E рядом
                var txt = new TextBlock
                {
                    Text = "E",
                    FontFamily = new FontFamily("Times New Roman, Cambria, Serif"),
                    FontStyle = FontStyles.Italic,
                    FontWeight = FontWeights.Bold,
                    FontSize = 22,
                    Foreground = strokeBrush
                };
                Canvas.SetLeft(txt, comp.Width - 14);
                Canvas.SetTop(txt, 4);
                canvasItem.Children.Add(txt);
            }
            else if (comp.Category == ComponentCategory.Resistors)
            {
                // Строгий ГОСТ прямоугольник резистора (R)
                double rW = comp.Width - 24;
                double rH = comp.Height - 16;
                var rect = new Rectangle
                {
                    Width = rW,
                    Height = rH,
                    Stroke = strokeBrush,
                    StrokeThickness = strokeThick,
                    Fill = new SolidColorBrush(_canvasBg)
                };
                Canvas.SetLeft(rect, 12);
                Canvas.SetTop(rect, 8);
                canvasItem.Children.Add(rect);

                var leftPinLine = new Line { X1 = 0, Y1 = comp.Height / 2, X2 = 12, Y2 = comp.Height / 2, Stroke = strokeBrush, StrokeThickness = strokeThick };
                var rightPinLine = new Line { X1 = comp.Width - 12, Y1 = comp.Height / 2, X2 = comp.Width, Y2 = comp.Height / 2, Stroke = strokeBrush, StrokeThickness = strokeThick };
                canvasItem.Children.Add(leftPinLine);
                canvasItem.Children.Add(rightPinLine);

                var txt = new TextBlock
                {
                    Text = "R",
                    FontFamily = new FontFamily("Times New Roman, Cambria, Serif"),
                    FontStyle = FontStyles.Italic,
                    FontWeight = FontWeights.Bold,
                    FontSize = 22,
                    Foreground = strokeBrush
                };
                Canvas.SetLeft(txt, comp.Width / 2 - 8);
                Canvas.SetTop(txt, comp.Height / 2 - 14);
                canvasItem.Children.Add(txt);
            }
            else
            {
                // Базовое ГОСТ обозначение с чертой
                double rW = comp.Width - 24;
                double rH = comp.Height - 16;
                var rect = new Rectangle
                {
                    Width = rW,
                    Height = rH,
                    Stroke = strokeBrush,
                    StrokeThickness = strokeThick,
                    Fill = new SolidColorBrush(_canvasBg)
                };
                Canvas.SetLeft(rect, 12);
                Canvas.SetTop(rect, 8);
                canvasItem.Children.Add(rect);

                var leftPinLine = new Line { X1 = 0, Y1 = comp.Height / 2, X2 = 12, Y2 = comp.Height / 2, Stroke = strokeBrush, StrokeThickness = strokeThick };
                var rightPinLine = new Line { X1 = comp.Width - 12, Y1 = comp.Height / 2, X2 = comp.Width, Y2 = comp.Height / 2, Stroke = strokeBrush, StrokeThickness = strokeThick };
                canvasItem.Children.Add(leftPinLine);
                canvasItem.Children.Add(rightPinLine);

                var txt = new TextBlock
                {
                    Text = comp.Label,
                    FontFamily = new FontFamily("Times New Roman, Cambria, Serif"),
                    FontStyle = FontStyles.Italic,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = strokeBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Canvas.SetLeft(txt, 16);
                Canvas.SetTop(txt, comp.Height / 2 - 10);
                canvasItem.Children.Add(txt);
            }

            Canvas.SetLeft(canvasItem, comp.X - comp.Width / 2);
            Canvas.SetTop(canvasItem, comp.Y - comp.Height / 2);
            MainCanvas.Children.Add(canvasItem);

            DrawPins(comp);
        }

        private void DrawPins(CircuitComponent comp)
        {
            foreach (var pin in comp.Pins)
            {
                var pinColor = (pin.Name == "+") ? Color.FromRgb(239, 68, 68) :
                               (pin.Name == "-") ? Color.FromRgb(59, 130, 246) :
                               Color.FromRgb(156, 163, 175);

                var pinCircle = new Ellipse
                {
                    Width = 14, Height = 14,
                    Fill = new SolidColorBrush(pinColor),
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    Cursor = Cursors.Cross,
                    ToolTip = $"Вывод {pin.Name} (Потяните для провода)"
                };

                Canvas.SetLeft(pinCircle, pin.AbsoluteX - 7);
                Canvas.SetTop(pinCircle, pin.AbsoluteY - 7);
                MainCanvas.Children.Add(pinCircle);
            }
        }

        // ================= MOUSE =================
        private void CanvasScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                double zoomFactor = e.Delta > 0 ? 1.15 : (1.0 / 1.15);
                double newScale = CanvasScaleTransform.ScaleX * zoomFactor;
                newScale = Math.Clamp(newScale, 0.25, 4.0);

                CanvasScaleTransform.ScaleX = newScale;
                CanvasScaleTransform.ScaleY = newScale;
                e.Handled = true;
            }
        }

        private void MainCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Point mousePos = e.GetPosition(MainCanvas);

            if (_activeTool == ActiveToolMode.Eraser)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    EraseAtPoint(mousePos);
                    MainCanvas.CaptureMouse();
                }
                return;
            }

            if (_activeTool == ActiveToolMode.Draw)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    _currentPolyline = new Polyline
                    {
                        Stroke = new SolidColorBrush(_isDarkMode ? Color.FromRgb(56, 189, 248) : Color.FromRgb(2, 132, 199)),
                        StrokeThickness = 2.4,
                        StrokeLineJoin = PenLineJoin.Round,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };
                    _currentPolyline.Points.Add(mousePos);
                    _drawnStrokes.Add(_currentPolyline);
                    MainCanvas.Children.Add(_currentPolyline);
                    MainCanvas.CaptureMouse();
                }
                return;
            }

            // Direct wiring from pin
            var hitPin = FindPinUnderPoint(mousePos);
            if (hitPin != null && e.LeftButton == MouseButtonState.Pressed)
            {
                _activeWiringPin = hitPin;
                _tempWireLine = new Line
                {
                    X1 = hitPin.AbsoluteX,
                    Y1 = hitPin.AbsoluteY,
                    X2 = mousePos.X,
                    Y2 = mousePos.Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(250, 204, 21)),
                    StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 4, 2 }
                };
                MainCanvas.Children.Add(_tempWireLine);
                MainCanvas.CaptureMouse();
                return;
            }

            // Drag component or Toggle Switch
            var hitComp = _components.FirstOrDefault(c =>
                mousePos.X >= c.X - c.Width / 2 && mousePos.X <= c.X + c.Width / 2 &&
                mousePos.Y >= c.Y - c.Height / 2 && mousePos.Y <= c.Y + c.Height / 2);

            _selectedComponent = hitComp;
            UpdateInspector();

            if (hitComp != null && e.LeftButton == MouseButtonState.Pressed)
            {
                // Если кликнули на ключ/выключатель — переключаем замкнут <-> разомкнут
                if (hitComp.Type == ComponentType.SwitchClosed || hitComp.Type == ComponentType.SwitchOpen)
                {
                    if (hitComp.Type == ComponentType.SwitchClosed)
                    {
                        hitComp.Type = ComponentType.SwitchOpen;
                        hitComp.Value = 0;
                        hitComp.Label = "Ключ (выкл)";
                    }
                    else
                    {
                        hitComp.Type = ComponentType.SwitchClosed;
                        hitComp.Value = 1;
                        hitComp.Label = "Ключ (вкл)";
                    }

                    if (_simulationRunning)
                    {
                        SolveCircuit_Click(sender, e);
                    }
                    else
                    {
                        RedrawAll();
                    }
                    UpdateInspector();
                }

                _draggedComponent = hitComp;
                _dragStartMousePos = mousePos;
                _compInitialPos = new Point(hitComp.X, hitComp.Y);
                MainCanvas.CaptureMouse();
                return;
            }

            RedrawAll();
        }

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(MainCanvas);

            if (_activeTool == ActiveToolMode.Eraser && e.LeftButton == MouseButtonState.Pressed)
            {
                EraseAtPoint(mousePos);
                return;
            }

            if (_activeTool == ActiveToolMode.Draw && _currentPolyline != null && e.LeftButton == MouseButtonState.Pressed)
            {
                _currentPolyline.Points.Add(mousePos);
                return;
            }

            if (_activeWiringPin != null && _tempWireLine != null)
            {
                _tempWireLine.X2 = mousePos.X;
                _tempWireLine.Y2 = mousePos.Y;
                return;
            }

            if (_draggedComponent != null && e.LeftButton == MouseButtonState.Pressed)
            {
                double dx = mousePos.X - _dragStartMousePos.X;
                double dy = mousePos.Y - _dragStartMousePos.Y;
                double rawX = _compInitialPos.X + dx;
                double rawY = _compInitialPos.Y + dy;
                _draggedComponent.X = Math.Round(rawX / 20.0) * 20.0;
                _draggedComponent.Y = Math.Round(rawY / 20.0) * 20.0;
                RedrawAll();
            }
        }

        private void MainCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Point mousePos = e.GetPosition(MainCanvas);

            if (_activeTool == ActiveToolMode.Eraser)
            {
                MainCanvas.ReleaseMouseCapture();
                return;
            }

            if (_activeTool == ActiveToolMode.Draw && _currentPolyline != null)
            {
                _currentPolyline = null;
                MainCanvas.ReleaseMouseCapture();
                return;
            }

            if (_activeWiringPin != null)
            {
                var targetPin = FindPinUnderPoint(mousePos);
                if (targetPin != null && targetPin != _activeWiringPin)
                {
                    bool exists = _wires.Any(w =>
                        (w.Pin1 == _activeWiringPin && w.Pin2 == targetPin) ||
                        (w.Pin1 == targetPin && w.Pin2 == _activeWiringPin));

                    if (!exists)
                    {
                        _wires.Add(new Wire(_activeWiringPin, targetPin));
                    }
                }

                if (_tempWireLine != null)
                {
                    MainCanvas.Children.Remove(_tempWireLine);
                    _tempWireLine = null;
                }
                _activeWiringPin = null;
                MainCanvas.ReleaseMouseCapture();
                RedrawAll();
                return;
            }

            if (_draggedComponent != null)
            {
                _draggedComponent = null;
                MainCanvas.ReleaseMouseCapture();
            }
        }

        private void EraseAtPoint(Point p)
        {
            double eraseRadius = 16;
            bool changed = false;

            for (int i = _drawnStrokes.Count - 1; i >= 0; i--)
            {
                var stroke = _drawnStrokes[i];
                if (stroke.Points.Any(pt => (pt.X - p.X) * (pt.X - p.X) + (pt.Y - p.Y) * (pt.Y - p.Y) <= eraseRadius * eraseRadius))
                {
                    _drawnStrokes.RemoveAt(i);
                    changed = true;
                }
            }

            for (int i = _wires.Count - 1; i >= 0; i--)
            {
                var wire = _wires[i];
                double dist = DistanceToSegment(p, new Point(wire.Pin1.AbsoluteX, wire.Pin1.AbsoluteY), new Point(wire.Pin2.AbsoluteX, wire.Pin2.AbsoluteY));
                if (dist <= 12)
                {
                    _wires.RemoveAt(i);
                    changed = true;
                }
            }

            var comp = _components.FirstOrDefault(c =>
                p.X >= c.X - c.Width / 2 && p.X <= c.X + c.Width / 2 &&
                p.Y >= c.Y - c.Height / 2 && p.Y <= c.Y + c.Height / 2);

            if (comp != null)
            {
                _wires.RemoveAll(w => comp.Pins.Contains(w.Pin1) || comp.Pins.Contains(w.Pin2));
                _components.Remove(comp);
                if (_selectedComponent == comp)
                {
                    _selectedComponent = null;
                    UpdateInspector();
                }
                changed = true;
            }

            if (changed)
            {
                RedrawAll();
            }
        }

        private static double DistanceToSegment(Point p, Point a, Point b)
        {
            double l2 = (b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y);
            if (l2 < 1e-6) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
            double t = Math.Max(0, Math.Min(1, ((p.X - a.X) * (b.X - a.X) + (p.Y - a.Y) * (b.Y - a.Y)) / l2));
            Point proj = new Point(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y));
            return Math.Sqrt((p.X - proj.X) * (p.X - proj.X) + (p.Y - proj.Y) * (p.Y - proj.Y));
        }

        private Pin FindPinUnderPoint(Point p)
        {
            foreach (var comp in _components)
            {
                foreach (var pin in comp.Pins)
                {
                    double distSq = (p.X - pin.AbsoluteX) * (p.X - pin.AbsoluteX) + (p.Y - pin.AbsoluteY) * (p.Y - pin.AbsoluteY);
                    if (distSq <= 16 * 16) return pin;
                }
            }
            return null;
        }

        // ================= INSPECTOR =================
        private bool _isUpdatingUi = false;
        private void UpdateInspector()
        {
            _isUpdatingUi = true;
            if (_selectedComponent != null)
            {
                SelectedElementNameText.Text = $"{_selectedComponent.Label} ({GetCategoryRussianName(_selectedComponent.Category)})";
                ElementNameBox.Text = _selectedComponent.Label;
                ElementValueBox.Text = _selectedComponent.Value.ToString(CultureInfo.InvariantCulture);
                ValueLabelText.Text = $"Номинал ({_selectedComponent.Unit}):";
                ElementDescriptionText.Text = GetComponentEncyclopediaText(_selectedComponent);
            }
            else
            {
                SelectedElementNameText.Text = "(ничего не выбрано)";
                ElementNameBox.Text = "";
                ElementValueBox.Text = "";
                ElementDescriptionText.Text = "Выберите любой компонент на схеме, чтобы прочитать, как он работает и для чего нужен в электронике.";
            }
            _isUpdatingUi = false;
        }

        private string GetCategoryRussianName(ComponentCategory cat)
        {
            return cat switch
            {
                ComponentCategory.Sources => "Источник питания",
                ComponentCategory.Resistors => "Резистор",
                ComponentCategory.Capacitors => "Конденсатор",
                ComponentCategory.Inductors => "Катушка индуктивности",
                ComponentCategory.Semiconductors => "Полупроводник",
                ComponentCategory.Lighting => "Освещение / Индикация",
                ComponentCategory.Meters => "Измерительный прибор",
                ComponentCategory.Switches => "Коммутационный ключ",
                ComponentCategory.LogicGates => "Логический элемент",
                ComponentCategory.Protection => "Защита / Датчик",
                _ => "Компонент"
            };
        }

        private string GetComponentEncyclopediaText(CircuitComponent comp)
        {
            switch (comp.Category)
            {
                case ComponentCategory.Resistors:
                    return $"⚡ РЕЗИСТОР (сопротивление {comp.Value} Ом):\n" +
                           "Пассивный элемент, ограничивающий силу электрического тока в цепи и распределяющий напряжение по закону Ома (U = I × R). Необходим для защиты светодиодов от сгорания, деления напряжения и задания рабочих режимов транзисторов.";

                case ComponentCategory.Sources:
                    if (comp.Type == ComponentType.Ground)
                        return "⏚ ЗАЗЕМЛЕНИЕ (GND, 0 Вольт):\n" +
                               "Опорная точка схемы с нулевым электрическим потенциалом. Служит общей шиной отсчета для измерения всех остальных напряжений и предотвращения накопления паразитных зарядов.";
                    return $"🔋 ИСТОЧНИК ПИТАНИЯ (ЭДС {comp.Value} В):\n" +
                           "Активный элемент, создающий разность потенциалов (электрическое поле), благодаря которому электроны приходят в упорядоченное движение. Преобразует химическую или световую энергию в электрический ток.";

                case ComponentCategory.Lighting:
                    return $"💡 ЛАМПА НАКАЛИВАНИЯ / LED (номинал {comp.Value} Ом):\n" +
                           "Преобразует электрическую энергию в видимый свет и тепло. Чем выше протекающий ток и выделяемая мощность (P = U × I), тем ярче светится нить накала или светодиодный кристалл.";

                case ComponentCategory.Capacitors:
                    return $"╢ КОНДЕНСАТОР ({comp.Value} мкФ):\n" +
                           "Накапливает электрический заряд на двух изолированных обкладках. В цепях постоянного тока (DC) после зарядки работает как разрыв цепи. Используется для фильтрации пульсаций питания и подавления помех.";

                case ComponentCategory.Inductors:
                    return $"🌀 КАТУШКА ИНДУКТИВНОСТИ ({comp.Value} мГн):\n" +
                           "Накапливает энергию в магнитном поле при протекании тока. Препятствует резким скачкам тока (явление самоиндукции). В цепях постоянного тока работает как обычный проводник.";

                case ComponentCategory.Meters:
                    if (comp.Type == ComponentType.Voltmeter)
                        return "📊 ВОЛЬТМЕТР:\n" +
                               "Прибор для измерения разности потенциалов (напряжения) между двумя точками. Подключается ПАРАЛЛЕЛЬНО исследуемому участку цепи. Обладает гигантским внутренним сопротивлением, чтобы не влиять на схему.";
                    if (comp.Type == ComponentType.Ammeter)
                        return "⏱ АМПЕРМЕТР:\n" +
                               "Прибор для измерения силы электрического тока. Подключается в РАЗРЫВ цепи (последовательно). Обладает околонулевым сопротивлением, чтобы не мешать течению электронов.";
                    return "📈 ИЗМЕРИТЕЛЬНЫЙ ПРИБОР:\n" +
                           "Контрольный датчик электрических параметров цепи.";

                case ComponentCategory.Semiconductors:
                    return "⯈ ДИОД:\n" +
                           "Электронный клапан, пропускающий электрический ток только в одном направлении (от анода '+' к катоду '-'). При обратном подключении ток блокируется.";

                case ComponentCategory.Switches:
                    return "🔘 КЛЮЧ / ВЫКЛЮЧАТЕЛЬ:\n" +
                           "Механическое коммутационное устройство для замыкания или размыкания участка электрической цепи.";

                default:
                    return $"Радиоэлемент схемы ({comp.Label}) с номиналом {comp.Value} {comp.Unit}.";
            }
        }

        private void ElementNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedComponent == null) return;
            _selectedComponent.Label = ElementNameBox.Text;
            RedrawAll();
        }

        private void ElementValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedComponent == null) return;
            if (double.TryParse(ElementValueBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
            {
                _selectedComponent.Value = val;
                RedrawAll();
            }
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedComponent == null) return;
            _wires.RemoveAll(w => _selectedComponent.Pins.Contains(w.Pin1) || _selectedComponent.Pins.Contains(w.Pin2));
            _components.Remove(_selectedComponent);
            _selectedComponent = null;
            UpdateInspector();
            RedrawAll();
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            _simulationRunning = false;
            _components.Clear();
            _wires.Clear();
            _drawnStrokes.Clear();
            _selectedComponent = null;
            UpdateInspector();
            SolutionStatusText.Text = "Холст очищен.";
            ResultsDataGrid.ItemsSource = null;
            RedrawAll();
        }

        private void LoadExample_Click(object sender, RoutedEventArgs e) => LoadExample();

        private void LoadExample()
        {
            _components.Clear();
            _wires.Clear();
            _drawnStrokes.Clear();

            var vs = new CircuitComponent(ComponentType.VoltageSource12V, ComponentCategory.Sources, 220, 240, "Батарея", 12, "В");
            var r1 = new CircuitComponent(ComponentType.Resistor100, ComponentCategory.Resistors, 420, 160, "Резистор", 100, "Ом");
            var bulb = new CircuitComponent(ComponentType.BulbStandard, ComponentCategory.Lighting, 420, 320, "Лампа", 50, "Ом");
            var gnd = new CircuitComponent(ComponentType.Ground, ComponentCategory.Sources, 220, 420, "Заземление", 0, "В");

            _components.Add(vs);
            _components.Add(r1);
            _components.Add(bulb);
            _components.Add(gnd);

            _wires.Add(new Wire(vs.Pins[0], r1.Pins[0]));
            _wires.Add(new Wire(r1.Pins[1], bulb.Pins[0]));
            _wires.Add(new Wire(bulb.Pins[1], vs.Pins[1]));
            _wires.Add(new Wire(gnd.Pins[0], vs.Pins[1]));

            _selectedComponent = vs;
            UpdateInspector();
            RedrawAll();

            SolutionStatusText.Text = "Схема готова: Батарея 12В, резистор 100 Ом и лампа 50 Ом. Нажмите 'РАССЧИТАТЬ И ЗАПУСТИТЬ ТОК'.";
        }

        private void SolveCircuit_Click(object sender, RoutedEventArgs e)
        {
            var result = MnaSolver.Solve(_components, _wires);

            if (result.Success)
            {
                _simulationRunning = true;
                SolutionStatusText.Text = $"✔ {result.Message}\nЭлектрический ток запущен! Лампы активны.";
                ResultsDataGrid.ItemsSource = result.Reports;
                RedrawAll();
            }
            else
            {
                _simulationRunning = false;
                SolutionStatusText.Text = $"❌ {result.Message}";
                ResultsDataGrid.ItemsSource = null;
                RedrawAll();
            }
        }

        private void StopCircuit_Click(object sender, RoutedEventArgs e)
        {
            _simulationRunning = false;
            SolutionStatusText.Text = "⏸ Цепь остановлена. Ток отключен.";
            RedrawAll();
        }

        private void RotateSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedComponent == null) return;
            _selectedComponent.Rotate();
            RedrawAll();
        }

        private void SolveHandwritten_Click(object sender, RoutedEventArgs e)
        {
            MnaSolver.Solve(_components, _wires);
            var vars = CollectCircuitVariables();

            Point loc;
            string solution = HandwrittenFormulaRecognizer.RecognizeAndSolve(_drawnStrokes, vars, out loc);

            FormulaResultText.Text = solution;

            // Отображаем текстовый результат прямо на холсте рядом с рукописной записью!
            var textBlock = new TextBlock
            {
                Text = $"✍ Решение: {solution}",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Gold,
                Background = new SolidColorBrush(Color.FromArgb(200, 20, 24, 30)),
                Padding = new Thickness(8, 4, 8, 4)
            };
            Canvas.SetLeft(textBlock, loc.X);
            Canvas.SetTop(textBlock, loc.Y);
            MainCanvas.Children.Add(textBlock);
        }

        private Dictionary<string, double> CollectCircuitVariables()
        {
            var vars = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            int compIndex = 1;
            foreach (var c in _components)
            {
                vars[c.Label] = c.Value;
                vars[$"I{compIndex}"] = Math.Abs(c.CalculatedCurrent);
                vars[$"I_{c.Label}"] = Math.Abs(c.CalculatedCurrent);
                vars[$"I({c.Label})"] = Math.Abs(c.CalculatedCurrent);

                vars[$"U{compIndex}"] = Math.Abs(c.CalculatedVoltage);
                vars[$"U_{c.Label}"] = Math.Abs(c.CalculatedVoltage);
                vars[$"U({c.Label})"] = Math.Abs(c.CalculatedVoltage);
                vars[$"V{compIndex}"] = Math.Abs(c.CalculatedVoltage);

                vars[$"P{compIndex}"] = c.CalculatedPower;
                vars[$"P({c.Label})"] = c.CalculatedPower;

                compIndex++;
            }
            return vars;
        }

        private void SolveFormula_Click(object sender, RoutedEventArgs e)
        {
            MnaSolver.Solve(_components, _wires);
            var vars = CollectCircuitVariables();

            string inputFormula = FormulaInputBox.Text;
            string solution = FormulaSolver.Evaluate(inputFormula, vars);

            FormulaResultText.Text = solution;
        }
    }
}
