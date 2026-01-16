using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DigitalisationERP.Desktop.Models.InternalMessaging;

namespace DigitalisationERP.Desktop.Views;

public sealed class RecipientPickerDialog : Window
{
    private readonly ListBox _list;

    public List<int> SelectedRecipientIds
        => _list.SelectedItems.OfType<WorkerDto>().Select(w => w.Id).ToList();

    public RecipientPickerDialog(List<WorkerDto> workers)
    {
        Title = "Select recipients";
        Width = 520;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new TextBlock
        {
            Text = "Recipients (Ctrl/Shift multi-select)",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _list = new ListBox
        {
            SelectionMode = SelectionMode.Extended,
            ItemsSource = workers,
            DisplayMemberPath = nameof(WorkerDto.Name)
        };

        _list.ItemTemplate = BuildItemTemplate();

        var view = (CollectionView)CollectionViewSource.GetDefaultView(_list.ItemsSource);
        view.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(WorkerDto.Department), System.ComponentModel.ListSortDirection.Ascending));
        view.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(WorkerDto.Role), System.ComponentModel.ListSortDirection.Ascending));
        view.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(WorkerDto.Name), System.ComponentModel.ListSortDirection.Ascending));

        Grid.SetRow(_list, 1);
        root.Children.Add(_list);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "Cancel", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        var ok = new Button { Content = "Send", Width = 90 };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);

        Content = root;
    }

    private static DataTemplate BuildItemTemplate()
    {
        var template = new DataTemplate(typeof(WorkerDto));

        var sp = new FrameworkElementFactory(typeof(StackPanel));
        sp.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
        sp.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 6, 0, 6));

        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, new Binding(nameof(WorkerDto.Name)));
        name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        sp.AppendChild(name);

        var meta = new FrameworkElementFactory(typeof(TextBlock));
        meta.SetBinding(TextBlock.TextProperty, new Binding(nameof(WorkerDto.Role)));
        meta.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Gray);
        meta.SetValue(TextBlock.FontSizeProperty, 12.0);
        sp.AppendChild(meta);

        var dept = new FrameworkElementFactory(typeof(TextBlock));
        dept.SetBinding(TextBlock.TextProperty, new Binding(nameof(WorkerDto.Department)));
        dept.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Gray);
        dept.SetValue(TextBlock.FontSizeProperty, 11.0);
        sp.AppendChild(dept);

        template.VisualTree = sp;
        return template;
    }
}
