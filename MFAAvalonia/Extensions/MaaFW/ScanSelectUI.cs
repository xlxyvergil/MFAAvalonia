using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using FluentIcons.Avalonia.Fluent;
using FluentIcons.Common;
using Lang.Avalonia.MarkupExtensions;
using MFAAvalonia.Extensions;
using MFAAvalonia.Helper;
using MFAAvalonia.Helper.ValueType;
using MFAAvalonia.Views.UserControls;
using Newtonsoft.Json.Linq;
using SukiUI.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Action = System.Action;

namespace MFAAvalonia.Extensions.MaaFW;

/// <summary>
/// scan_select UI 控件生成器
/// 完全参照 CreateComboBoxControl 的实现，添加目录扫描和刷新功能
/// </summary>
public static class ScanSelectUI
{
    public static Control CreateScanSelectControl(
        DragItemViewModel source,
        MaaInterface.MaaInterfaceOption interfaceOption,
        MaaInterface.MaaInterfaceSelectOption option,
        Action saveConfigurationAction)
    {
        var wrapper = new StackPanel();
        var grid = CreateBaseGrid();

        // 初始化 Cases 显示名称和图标
        interfaceOption.InitializeIcon();

        // 执行初始目录扫描
        var scannedItems = ScanDirectory(interfaceOption);
        interfaceOption.Cases = scannedItems;  // 将扫描结果赋值给 interfaceOption.Cases
        
        // 初始化 Cases 显示名称
        interfaceOption.Cases?.ForEach(c => c.InitializeDisplayName());
        
        // 创建 ComboBox
        var comboBox = new ComboBox
        {
            MinWidth = 120,
            Classes = { "LimitWidth" },
            Margin = new Thickness(0, 2, 0, 2),
            ItemsSource = scannedItems,
            SelectedIndex = option.Index ?? 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        
        BindIdleEnabled(comboBox);
        SetupComboBoxTemplate(comboBox);

        // Sub-options container
        var subOptionsContainer = new StackPanel();

        void UpdateSubOptions(int index)
        {
            subOptionsContainer.Children.Clear();
            if (interfaceOption.Cases == null || index < 0 || index >= interfaceOption.Cases.Count) return;

            var selectedCase = interfaceOption.Cases[index];
            if (selectedCase.Option == null || selectedCase.Option.Count == 0) return;

            option.SubOptions ??= new List<MaaInterface.MaaInterfaceSelectOption>();

            foreach (var subOptionName in selectedCase.Option)
            {
                var existing = option.SubOptions.FirstOrDefault(o => o.Name == subOptionName);
                if (existing == null)
                {
                    existing = CreateDefaultSelectOption(subOptionName);
                    option.SubOptions.Add(existing);
                }
                AddSubOption(subOptionsContainer, existing, source);
            }
        }

        comboBox.SelectionChanged += (_, _) =>
        {
            var selectedCase = comboBox.SelectedItem as MaaInterface.MaaInterfaceOptionCase;
            var resolvedIndex = selectedCase != null && interfaceOption.Cases != null
                ? interfaceOption.Cases.FindIndex(c => c.Name == selectedCase.Name)
                : comboBox.SelectedIndex;

            option.Index = resolvedIndex;
            UpdateSubOptions(resolvedIndex);
            saveConfigurationAction();
        };
        
        UpdateSubOptions(option.Index ?? 0);
        ComboBoxExtensions.SetDisableNavigationOnLostFocus(comboBox, true);
        ComboBoxExtensions.SetCanSearch(comboBox, true);
        ComboBoxExtensions.SetSearchMemberPath(comboBox, "DisplayName");
        comboBox.Bind(ComboBoxExtensions.SearchWatermarkProperty, new I18nBinding(LangKeys.Search));

        // Header
        var labelPanel = CreateLabelPanel(option.DisplayName, option.Name, interfaceOption.Description, interfaceOption.Document);
        var icon = CreateIcon(interfaceOption);
        icon.Margin = new Thickness(10, 0, 6, 0);
        labelPanel.Children.Insert(0, icon);

        // 刷新按钮
        var refreshButton = new Button
        {
            Content = new SymbolIcon { Symbol = Symbol.ArrowSync },
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(4),
            Width = 32,
            Height = 32
        };
        ToolTip.SetTip(refreshButton, "刷新扫描结果");
        BindIdleEnabled(refreshButton);

        refreshButton.Click += (_, _) =>
        {
            var newItems = ScanDirectory(interfaceOption);
            interfaceOption.Cases = newItems;  // 更新 interfaceOption.Cases
            interfaceOption.Cases?.ForEach(c => c.InitializeDisplayName());  // 重新初始化显示名称
            comboBox.ItemsSource = newItems;
            
            // 尝试保持当前选中项
            if (option.Index.HasValue && option.Index.Value >= 0 && option.Index.Value < newItems.Count)
            {
                comboBox.SelectedIndex = option.Index.Value;
            }
            else if (newItems.Count > 0)
            {
                comboBox.SelectedIndex = 0;
                option.Index = 0;
            }
            
            saveConfigurationAction();
        };

        Grid.SetColumn(labelPanel, 0);
        Grid.SetColumn(comboBox, 1);
        Grid.SetColumn(refreshButton, 2);
        
        AddResponsiveBehavior(grid, labelPanel, comboBox, refreshButton);
        
        grid.Children.Add(labelPanel);
        grid.Children.Add(comboBox);
        grid.Children.Add(refreshButton);

        wrapper.Children.Add(grid);
        
        // Sub-options border
        var subOptionsBorder = new Border
        {
            BorderThickness = new Thickness(2, 0, 0, 0),
            Margin = new Thickness(24, 0, 0, 4),
            Padding = new Thickness(8, 0, 0, 0),
            Child = subOptionsContainer
        };
        subOptionsBorder.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("SukiPrimaryColor"));
        subOptionsBorder.Bind(Visual.IsVisibleProperty, new Binding("Children.Count")
        {
            Source = subOptionsContainer,
            Converter = new FuncValueConverter<int, bool>(count => count > 0)
        });

        wrapper.Children.Add(subOptionsBorder);

        return wrapper;
    }

    /// <summary>
    /// 扫描目录并生成 MaaInterfaceOptionCase 列表
    /// </summary>
    private static List<MaaInterface.MaaInterfaceOptionCase> ScanDirectory(MaaInterface.MaaInterfaceOption interfaceOption)
    {
        var cases = new List<MaaInterface.MaaInterfaceOptionCase>();

        if (string.IsNullOrEmpty(interfaceOption.ScanDir))
            return cases;

        try
        {
            // 解析扫描目录（支持相对路径，相对于 DataRoot）
            var baseDir = AppPaths.DataRoot;
            var scanDir = Path.IsPathRooted(interfaceOption.ScanDir) 
                ? interfaceOption.ScanDir 
                : Path.Combine(baseDir, interfaceOption.ScanDir);

            if (!Directory.Exists(scanDir))
                return cases;

            // 获取过滤模式
            var filter = string.IsNullOrEmpty(interfaceOption.ScanFilter) ? "*" : interfaceOption.ScanFilter;
            
            // 扫描文件
            var files = Directory.GetFiles(scanDir, filter, SearchOption.TopDirectoryOnly);
            
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var relativePath = Path.GetRelativePath(scanDir, file);
                
                var caseOption = new MaaInterface.MaaInterfaceOptionCase
                {
                    Name = relativePath,  // 使用相对路径作为 Name（用于 pipeline_override 替换）
                    Label = fileName      // 使用文件名作为显示标签
                };
                caseOption.InitializeDisplayName();
                
                cases.Add(caseOption);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ScanSelect] 扫描目录失败: {ex.Message}");
        }

        return cases;
    }

    private static Grid CreateBaseGrid()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(6, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Margin = new Thickness(10, 3, 10, 3)
        };
        return grid;
    }

    private static StackPanel CreateLabelPanel(string displayName, string? name, string? description, List<string>? document = null)
    {
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            MinWidth = 180,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        
        var textBlock = new TextBlock
        {
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        
        textBlock.Bind(TextBlock.TextProperty, new ResourceBindingWithFallback(displayName, name));
        textBlock.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("SukiLowText"));
        stackPanel.Children.Add(textBlock);
        
        var tooltipText = GetTooltipText(description, document);
        if (!string.IsNullOrWhiteSpace(tooltipText))
        {
            var docBlock = new TooltipBlock();
            docBlock.Bind(TooltipBlock.TooltipTextProperty, new ResourceBinding(tooltipText));
            docBlock.Margin = new Thickness(4, 0, 0, 0);
            stackPanel.Children.Add(docBlock);
        }
        
        return stackPanel;
    }

    private static DisplayIcon CreateIcon(MaaInterface.MaaInterfaceOption interfaceOption)
    {
        var iconDisplay = new DisplayIcon
        {
            IconSize = 20,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        iconDisplay.Bind(DisplayIcon.IconSourceProperty, new Binding(nameof(MaaInterface.MaaInterfaceOption.ResolvedIcon)) { Source = interfaceOption });
        iconDisplay.Bind(Visual.IsVisibleProperty, new Binding(nameof(MaaInterface.MaaInterfaceOption.HasIcon)) { Source = interfaceOption });
        return iconDisplay;
    }

    private static void BindIdleEnabled(Control control)
    {
        control.Bind(Control.IsEnabledProperty, new Binding("Idle") { Source = Instances.RootViewModel });
    }

    private static void AddResponsiveBehavior(Grid grid, Control label, Control input, Control button)
    {
        grid.SizeChanged += (sender, e) =>
        {
            if (sender is not Grid currentGrid) return;
            double totalMinWidth = currentGrid.Children.Sum(c => c is Control ctrl ? ctrl.MinWidth : 0);
            double availableWidth = currentGrid.Bounds.Width - currentGrid.Margin.Left - currentGrid.Margin.Right;

            // Responsive Switch
            if (availableWidth < totalMinWidth * 0.8)
            {
                currentGrid.ColumnDefinitions.Clear();
                currentGrid.RowDefinitions.Clear();
                currentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                currentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                currentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Grid.SetRow(label, 0);
                Grid.SetRow(input, 1);
                Grid.SetRow(button, 2);
                Grid.SetColumn(label, 0);
                Grid.SetColumn(input, 0);
                Grid.SetColumn(button, 0);
            }
            else
            {
                currentGrid.RowDefinitions.Clear();
                currentGrid.ColumnDefinitions.Clear();
                currentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) });
                currentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Star) });
                currentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                Grid.SetRow(label, 0);
                Grid.SetRow(input, 0);
                Grid.SetRow(button, 0);
                Grid.SetColumn(label, 0);
                Grid.SetColumn(input, 1);
                Grid.SetColumn(button, 2);
            }
        };
    }

    private static void SetupComboBoxTemplate(ComboBox comboBox)
    {
        comboBox.ItemTemplate = new FuncDataTemplate<MaaInterface.MaaInterfaceOptionCase>((caseOption, _) => CreateComboBoxItemContent(caseOption, true));
        comboBox.SelectionBoxItemTemplate = new FuncDataTemplate<MaaInterface.MaaInterfaceOptionCase>((caseOption, _) => CreateComboBoxItemContent(caseOption, false));
    }
    
    private static Control CreateComboBoxItemContent(MaaInterface.MaaInterfaceOptionCase? caseOption, bool isMarquee)
    {
         var grid = new Grid
         {
             ColumnDefinitions =
             {
                 new ColumnDefinition { Width = GridLength.Auto },
                 new ColumnDefinition { Width = GridLength.Star },
                 new ColumnDefinition { Width = GridLength.Auto }
             },
             HorizontalAlignment = HorizontalAlignment.Stretch
         };
         
         var iconDisplay = new DisplayIcon { IconSize = 20, Margin = new Thickness(0,0,6,0), VerticalAlignment = VerticalAlignment.Center };
         iconDisplay.Bind(DisplayIcon.IconSourceProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.ResolvedIcon)) { Source = caseOption });
         iconDisplay.Bind(Visual.IsVisibleProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.HasIcon)) { Source = caseOption });
         Grid.SetColumn(iconDisplay, 0);

         Control textControl;
         if (isMarquee)
         {
             var marquee = new MarqueeTextBlock { VerticalContentAlignment = VerticalAlignment.Center };
             marquee.Bind(MarqueeTextBlock.ForegroundProperty, new DynamicResourceExtension("SukiText"));
             marquee.Bind(MarqueeTextBlock.TextProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.DisplayName)) { Source = caseOption });
             marquee.Bind(ToolTip.TipProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.DisplayName)) { Source = caseOption });
             ToolTip.SetShowDelay(marquee, 100);
             textControl = marquee;
         }
         else
         {
             var tb = new TextBlock { TextTrimming = TextTrimming.WordEllipsis, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center };
             tb.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("SukiText"));
             tb.Bind(TextBlock.TextProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.DisplayName)) { Source = caseOption });
             tb.Bind(ToolTip.TipProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.DisplayName)) { Source = caseOption });
             textControl = tb;
         }
         Grid.SetColumn(textControl, 1);
         
         var tooltipBlock = new TooltipBlock { Margin = new Thickness(4, 0, 0, 0) };
         tooltipBlock.Bind(TooltipBlock.TooltipTextProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.DisplayDescription)) { Source = caseOption });
         tooltipBlock.Bind(Visual.IsVisibleProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.HasDescription)) { Source = caseOption });
         Grid.SetColumn(tooltipBlock, 2);
         grid.Children.Add(tooltipBlock);
         
         grid.Children.Add(iconDisplay);
         grid.Children.Add(textControl);
         
         return grid;
    }

    private static void AddSubOption(StackPanel container, MaaInterface.MaaInterfaceSelectOption subOption, DragItemViewModel source)
    {
         if (MaaProcessor.Interface?.Option?.TryGetValue(subOption.Name ?? string.Empty, out var subInterfaceOption) != true) return;
         
         Control control;
         if (subInterfaceOption.IsInput)
            control = CreateInputControl(subOption, subInterfaceOption);
         else if (subInterfaceOption.IsCheckbox)
            control = CreateCheckboxControl(subOption, subInterfaceOption, source);
         else if ((subInterfaceOption.IsSwitch && subInterfaceOption.Cases.ShouldSwitchButton(out var yes, out var no)) ||
                  subInterfaceOption.Cases.ShouldSwitchButton(out yes, out no))
            control = CreateToggleControl(subOption, yes, no, subInterfaceOption, source);
         else
            control = CreateComboBoxControl(subOption, subInterfaceOption, source);

         container.Children.Add(control);
    }

    // 简化的子选项创建方法（实际应该调用 TaskOptionGenerator 的方法，但为了避免循环依赖，这里简化处理）
    private static MaaInterface.MaaInterfaceSelectOption CreateDefaultSelectOption(string name)
    {
        return new MaaInterface.MaaInterfaceSelectOption
        {
            Name = name,
            Index = 0
        };
    }

    private static Control CreateInputControl(MaaInterface.MaaInterfaceSelectOption option, MaaInterface.MaaInterfaceOption interfaceOption)
    {
        var textBox = new TextBox
        {
            Text = option.Data?.Values.FirstOrDefault() ?? "",
            MinWidth = 120,
            Classes = { "LimitWidth" },
            Margin = new Thickness(0, 2, 0, 2)
        };
        BindIdleEnabled(textBox);
        return textBox;
    }

    private static Control CreateCheckboxControl(MaaInterface.MaaInterfaceSelectOption option, MaaInterface.MaaInterfaceOption interfaceOption, DragItemViewModel source)
    {
        var checkBox = new CheckBox
        {
            IsChecked = option.Index == 1,
            Content = option.DisplayName,
            Margin = new Thickness(0, 2, 0, 2)
        };
        BindIdleEnabled(checkBox);
        return checkBox;
    }

    private static Control CreateToggleControl(MaaInterface.MaaInterfaceSelectOption option, int yesValue, int noValue, MaaInterface.MaaInterfaceOption interfaceOption, DragItemViewModel source)
    {
        var toggleSwitch = new ToggleSwitch
        {
            IsChecked = option.Index == yesValue,
            Classes = { "Switch" },
            MaxHeight = 60,
            MaxWidth = 100,
            Margin = new Thickness(0, 4, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        BindIdleEnabled(toggleSwitch);
        return toggleSwitch;
    }

    private static Control CreateComboBoxControl(MaaInterface.MaaInterfaceSelectOption option, MaaInterface.MaaInterfaceOption interfaceOption, DragItemViewModel source)
    {
        var comboBox = new ComboBox
        {
            MinWidth = 120,
            Classes = { "LimitWidth" },
            Margin = new Thickness(0, 2, 0, 2),
            ItemsSource = interfaceOption.Cases,
            SelectedIndex = option.Index ?? 0
        };
        BindIdleEnabled(comboBox);
        SetupComboBoxTemplate(comboBox);
        return comboBox;
    }

    private static string? GetTooltipText(string? description, List<string>? document)
    {
         if (!string.IsNullOrWhiteSpace(description))
         {
             var result = description.ResolveContentAsync(transform: false).GetAwaiter().GetResult();
             if (result == description)
             {
                 var localized = description.ToLocalization();
                 if (localized != description)
                     return localized;
             }
             return result;
         }
             
         if (document is { Count: > 0 })
         {
             try { return LanguageHelper.GetLocalizedString(System.Text.RegularExpressions.Regex.Unescape(string.Join("\\n", document))); }
             catch { return string.Join("\n", document); }
         }
         return null;
    }
}
