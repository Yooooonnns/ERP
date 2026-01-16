using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DigitalisationERP.Desktop.Models;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.Views;

public partial class InventoryPage : UserControl
{
    private readonly LoginResponse _loginResponse;
    private readonly ApiClient _apiClient;
    private readonly ProductionDataService _productionDataService;

    private List<MaterialDto> _rawMaterials = new();
    private List<MaterialDto> _finishedProducts = new();
    private List<ProductionLineDto> _lines = new();

    private double _currentZoom = 1.0;
    private bool _isPanning;
    private Point _panStart;

    private NodeTag? _selected;

    private readonly Dictionary<string, Rect> _rawNodeBounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Rect> _lineNodeBounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Rect> _finalNodeBounds = new(StringComparer.OrdinalIgnoreCase);

    public InventoryPage(LoginResponse loginResponse)
    {
        InitializeComponent();
        _loginResponse = loginResponse;
        _apiClient = new ApiClient { AuthToken = _loginResponse.AccessToken };
        _productionDataService = ProductionDataService.Instance;

        _productionDataService.EnsureInitialized();

        // Keep the diagram in sync with PF counters updated by Production.
        _productionDataService.LineUpdated += (_, __) => Dispatcher.Invoke(RenderDiagram);
        _productionDataService.LineAdded += (_, __) => Dispatcher.Invoke(RenderDiagram);
        _productionDataService.LineRemoved += (_, __) => Dispatcher.Invoke(RenderDiagram);

        Loaded += async (_, _) =>
        {
            DrawGridPattern();
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        try
        {
            _rawMaterials = await _apiClient.GetMaterialsAsync(materialType: 1); // RawMaterial
            _finishedProducts = await _apiClient.GetMaterialsAsync(materialType: 3); // FinishedProduct
            _lines = await _apiClient.GetProductionLinesAsync();

            RenderDiagram();
            UpdateStatistics();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load stock diagram data: {ex.Message}", "API Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DrawGridPattern()
    {
        GridCanvas.Children.Clear();
        const int gridSize = 50;
        var brush = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));

        for (int x = 0; x < 2200; x += gridSize)
        {
            GridCanvas.Children.Add(new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = 1500,
                Stroke = brush,
                StrokeThickness = 1
            });
        }

        for (int y = 0; y < 1500; y += gridSize)
        {
            GridCanvas.Children.Add(new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = 2200,
                Y2 = y,
                Stroke = brush,
                StrokeThickness = 1
            });
        }
    }

    private void RenderDiagram()
    {
        InventoryCanvas.Children.Clear();
        _rawNodeBounds.Clear();
        _lineNodeBounds.Clear();
        _finalNodeBounds.Clear();

        // Build a lookup: output PF materialNumber -> list of (lineId, finishedCount)
        var producedByFinishedMaterial = new Dictionary<string, List<(string lineId, int finishedCount)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in _lines)
        {
            var lineId = (line.lineId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(lineId)) continue;

            var outMn = (line.output?.materialNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outMn)) continue;

            var localLine = _productionDataService.GetProductionLine(lineId);
            var finishedCount = localLine?.FinishedProductCount ?? 0;

            if (!producedByFinishedMaterial.TryGetValue(outMn, out var list))
            {
                list = new List<(string lineId, int finishedCount)>();
                producedByFinishedMaterial[outMn] = list;
            }

            list.Add((lineId, finishedCount));
        }

        DrawColumnHeader("RAW MATERIALS", 120, Brushes.DodgerBlue);
        DrawColumnHeader("PRODUCTION LINES", 820, Brushes.MediumPurple);
        DrawColumnHeader("FINISHED PRODUCTS", 1520, Brushes.SeaGreen);

        const double rawX = 100.0;
        const double lineX = 780.0;
        const double finalX = 1480.0;

        var y = 120.0;
        foreach (var raw in _rawMaterials.OrderBy(m => m.materialNumber))
        {
            var mn = raw.materialNumber ?? string.Empty;
            var b = CreateNode(
                title: mn,
                subtitle: $"{raw.description}  •  Stock: {raw.stockQuantity:0.##} {raw.unitOfMeasure}",
                accent: Brushes.DodgerBlue,
                tag: new NodeTag(NodeKind.RawMaterial, mn));

            AddAt(b, rawX, y);
            _rawNodeBounds[mn] = new Rect(rawX, y, b.Width, b.Height);
            y += 140;
        }

        y = 120.0;
        foreach (var line in _lines.OrderBy(l => l.lineId))
        {
            var id = line.lineId ?? string.Empty;
            var output = line.output?.materialNumber ?? string.Empty;
            var b = CreateNode(
                title: $"{id}  •  {line.lineName}",
                subtitle: $"Outputs: {output}",
                accent: Brushes.MediumPurple,
                tag: new NodeTag(NodeKind.Line, id));

            AddAt(b, lineX, y);
            _lineNodeBounds[id] = new Rect(lineX, y, b.Width, b.Height);
            y += 160;
        }

        y = 120.0;
        foreach (var fp in _finishedProducts.OrderBy(m => m.materialNumber))
        {
            var mn = fp.materialNumber ?? string.Empty;

            var producedInfo = string.Empty;
            if (!string.IsNullOrWhiteSpace(mn) && producedByFinishedMaterial.TryGetValue(mn, out var perLine))
            {
                // Show totals per line that outputs this PF.
                var parts = perLine
                    .OrderBy(x => x.lineId, StringComparer.OrdinalIgnoreCase)
                    .Select(x => $"{x.lineId}={x.finishedCount}")
                    .ToList();

                if (parts.Count > 0)
                {
                    producedInfo = "\nPF fabriqués (par ligne): " + string.Join("  •  ", parts);
                }
            }

            var b = CreateNode(
                title: mn,
                subtitle: $"{fp.description}  •  Stock: {fp.stockQuantity:0.##} {fp.unitOfMeasure}{producedInfo}",
                accent: Brushes.SeaGreen,
                tag: new NodeTag(NodeKind.FinishedProduct, mn));

            AddAt(b, finalX, y);
            _finalNodeBounds[mn] = new Rect(finalX, y, b.Width, b.Height);
            y += 140;
        }

        DrawConnections();
    }

    private void DrawColumnHeader(string text, double x, Brush accent)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.Bold,
            Foreground = accent,
            FontSize = 14
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, 50);
        InventoryCanvas.Children.Add(tb);
    }

    private Border CreateNode(string title, string subtitle, Brush accent, NodeTag tag)
    {
        var border = new Border
        {
            Width = 520,
            Height = 90,
            Background = Brushes.White,
            BorderBrush = accent,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Tag = tag
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Bold });
        stack.Children.Add(new TextBlock { Text = subtitle, Opacity = 0.85, TextWrapping = TextWrapping.Wrap });
        border.Child = stack;

        border.MouseLeftButtonDown += Node_MouseLeftButtonDown;
        return border;
    }

    private void AddAt(FrameworkElement el, double x, double y)
    {
        Canvas.SetLeft(el, x);
        Canvas.SetTop(el, y);
        InventoryCanvas.Children.Add(el);
    }

    private void DrawConnections()
    {
        var existing = InventoryCanvas.Children.OfType<Shape>().Where(s => s is Path || s is Line).ToList();
        foreach (var shape in existing)
        {
            InventoryCanvas.Children.Remove(shape);
        }

        foreach (var line in _lines)
        {
            if (string.IsNullOrWhiteSpace(line.lineId)) continue;
            if (!_lineNodeBounds.TryGetValue(line.lineId, out var lineRect)) continue;

            foreach (var input in line.inputs ?? new List<ProductionLineInputDto>())
            {
                var mn = input.materialNumber;
                if (string.IsNullOrWhiteSpace(mn)) continue;
                if (!_rawNodeBounds.TryGetValue(mn, out var rawRect)) continue;
                DrawBezier(rawRect, lineRect, Brushes.DodgerBlue);
            }

            var outMn = line.output?.materialNumber;
            if (string.IsNullOrWhiteSpace(outMn)) continue;
            if (_finalNodeBounds.TryGetValue(outMn, out var outRect))
            {
                DrawBezier(lineRect, outRect, Brushes.SeaGreen);
            }
        }
    }

    private void DrawBezier(Rect fromRect, Rect toRect, Brush stroke)
    {
        var start = new Point(fromRect.Right, fromRect.Top + fromRect.Height / 2);
        var end = new Point(toRect.Left, toRect.Top + toRect.Height / 2);

        var dx = Math.Max(80, (end.X - start.X) / 2);
        var c1 = new Point(start.X + dx, start.Y);
        var c2 = new Point(end.X - dx, end.Y);

        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new BezierSegment(c1, c2, end, true));
        var geom = new PathGeometry();
        geom.Figures.Add(figure);

        var path = new Path
        {
            Data = geom,
            Stroke = stroke,
            StrokeThickness = 2,
            Opacity = 0.75
        };

        InventoryCanvas.Children.Insert(0, path);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private async void AddRawMaterial_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new CreateMaterialDialog(materialType: 1) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;

        try
        {
            await _apiClient.UpsertMaterialAsync(new MaterialUpsertRequest
            {
                materialNumber = dlg.MaterialNumber,
                description = dlg.MaterialDescription,
                materialType = 1,
                unitOfMeasure = dlg.UnitOfMeasure,
                initialStockQuantity = dlg.InitialStock,
                minimumStock = 0,
                maximumStock = 0
            });
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Create Raw Material", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void AddFinishedProduct_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new CreateMaterialDialog(materialType: 3) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;

        try
        {
            await _apiClient.UpsertMaterialAsync(new MaterialUpsertRequest
            {
                materialNumber = dlg.MaterialNumber,
                description = dlg.MaterialDescription,
                materialType = 3,
                unitOfMeasure = dlg.UnitOfMeasure,
                initialStockQuantity = dlg.InitialStock,
                minimumStock = 0,
                maximumStock = 0
            });
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Create Finished Product", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void AddLine_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddProductionLineDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;

        _productionDataService.EnsureInitialized();
        if (_productionDataService.ProductionLines.Any(l => l.LineId.Equals(dialog.LineId, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("A line with this ID already exists.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _productionDataService.AddProductionLine(new ProductionLineData
        {
            LineId = dialog.LineId,
            LineName = dialog.LineName,
            Description = dialog.Description,
            IsActive = dialog.IsActive
        });

        try
        {
            var req = new UpsertProductionLineRequest
            {
                lineId = dialog.LineId,
                lineName = dialog.LineName,
                description = dialog.Description,
                isActive = dialog.IsActive,
                outputMaterial = new MaterialRefRequest
                {
                    materialNumber = dialog.OutputMaterialNumber,
                    description = dialog.OutputMaterialDescription,
                    materialType = 3,
                    unitOfMeasure = dialog.OutputUnitOfMeasure,
                    initialStockQuantity = dialog.OutputInitialStock
                },
                inputs = dialog.Inputs
                    .Where(i => !string.IsNullOrWhiteSpace(i.MaterialNumber))
                    .Select(i => new LineInputRequest
                    {
                        material = new MaterialRefRequest
                        {
                            materialNumber = i.MaterialNumber,
                            description = i.Description,
                            materialType = 1,
                            unitOfMeasure = i.UnitOfMeasure,
                            initialStockQuantity = i.InitialStockQuantity
                        },
                        quantityPerUnit = i.QuantityPerUnit,
                        unitOfMeasure = i.UnitOfMeasure
                    })
                    .ToList()
            };

            await _apiClient.CreateProductionLineAsync(req);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Create Line", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DuplicateSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        if (_selected.Kind != NodeKind.Line)
        {
            MessageBox.Show("Duplicate is only supported for production lines right now.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var prompt = new PromptDialog("Duplicate Line", "New Line ID") { Owner = Window.GetWindow(this) };
        if (prompt.ShowDialog() != true) return;
        var newId = prompt.Value;
        if (string.IsNullOrWhiteSpace(newId)) return;

        try
        {
            await _apiClient.DuplicateProductionLineAsync(_selected.Key, newId);
            _productionDataService.AddProductionLine(new ProductionLineData { LineId = newId, LineName = newId, Description = "", IsActive = true });
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Duplicate Line", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        if (_selected.Kind != NodeKind.Line)
        {
            MessageBox.Show("Delete is only supported for production lines right now.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"Delete line '{_selected.Key}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _apiClient.DeleteProductionLineAsync(_selected.Key);
            _productionDataService.RemoveProductionLine(_selected.Key);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Delete Line", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border b) return;
        if (b.Tag is not NodeTag tag) return;

        _selected = tag;
        if (e.ClickCount >= 2)
        {
            _ = HandleNodeDoubleClickAsync(tag);
        }
    }

    private async Task HandleNodeDoubleClickAsync(NodeTag tag)
    {
        if (tag.Kind == NodeKind.RawMaterial)
        {
            var win = new MaterialDetailsWindow(_apiClient, tag.Key) { Owner = Window.GetWindow(this) };
            win.ShowDialog();
            await ReloadAsync();
            return;
        }

        if (tag.Kind == NodeKind.Line)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                main.NavigateToProductionLine(tag.Key);
            }
            return;
        }

        if (tag.Kind == NodeKind.FinishedProduct)
        {
            var win = new FinalProductDashboardWindow(_apiClient, tag.Key) { Owner = Window.GetWindow(this) };
            win.ShowDialog();
            await ReloadAsync();
        }
    }

    private void UpdateStatistics()
    {
        RawCountText.Text = _rawMaterials.Count.ToString();
        LineCountText.Text = _lines.Count.ToString();
        FinalCountText.Text = _finishedProducts.Count.ToString();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _currentZoom = Math.Min(3.0, _currentZoom + 0.1);
        ApplyZoom();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _currentZoom = Math.Max(0.3, _currentZoom - 0.1);
        ApplyZoom();
    }

    private void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        _currentZoom = 1.0;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        CanvasScaleTransform.ScaleX = _currentZoom;
        CanvasScaleTransform.ScaleY = _currentZoom;
    }

    private void CanvasScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

        if (e.Delta > 0) ZoomIn_Click(sender, e);
        else ZoomOut_Click(sender, e);
        e.Handled = true;
    }

    private void InventoryCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed || Keyboard.IsKeyDown(Key.Space))
        {
            _isPanning = true;
            _panStart = e.GetPosition(CanvasScrollViewer);
            InventoryCanvas.CaptureMouse();
        }
    }

    private void InventoryCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning) return;

        var pos = e.GetPosition(CanvasScrollViewer);
        var delta = pos - _panStart;
        _panStart = pos;

        CanvasScrollViewer.ScrollToHorizontalOffset(CanvasScrollViewer.HorizontalOffset - delta.X);
        CanvasScrollViewer.ScrollToVerticalOffset(CanvasScrollViewer.VerticalOffset - delta.Y);
    }

    private void InventoryCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning) return;

        _isPanning = false;
        InventoryCanvas.ReleaseMouseCapture();
    }

    private enum NodeKind
    {
        RawMaterial,
        Line,
        FinishedProduct
    }

    private sealed record NodeTag(NodeKind Kind, string Key);
}
