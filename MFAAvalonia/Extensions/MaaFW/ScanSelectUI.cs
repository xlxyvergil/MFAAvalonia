using Avalonia.Controls;
using Avalonia.Layout;
using FluentIcons.Avalonia.Fluent;
using MFAAvalonia.Helper.ValueType;
using SukiUI.Extensions;
using System;
using System.IO;
using System.Linq;

namespace MFAAvalonia.Extensions.MaaFW;

/// <summary>
/// scan_select UI 控件生成器
/// </summary>
public static class ScanSelectUI
{
    /// <summary>
    /// 创建 scan_select 控件（带刷新按钮的 ComboBox）
    /// </summary>
    public static Control CreateScanSelectControl(
        DragItemViewModel source,
        MaaInterfaceOption interfaceOption,
        MaaInterfaceSelectOption option,
        Action saveConfigurationAction)
    {
        var wrapper = new StackPanel();
        var grid = TaskOptionGenerator.CreateBaseGrid();

        interfaceOption.Cases?.ForEach(c => c.InitializeDisplayName());
        interfaceOption.InitializeIcon();

        var comboBox = new ComboBox
        {
            MinWidth = 120,
            Classes = { "LimitWidth" },
            Margin = new Thickness(0, 2, 0, 2),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // 初始扫描并设置 ItemsSource
        UpdateComboBoxItems(comboBox, interfaceOption, option);

        TaskOptionGenerator.BindIdleEnabled(comboBox);
        TaskOptionGenerator.SetupComboBoxTemplate(comboBox);

        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is MaaInterfaceOptionCase selectedCase)
            {
                var selectedIndex = interfaceOption.Cases?.FindIndex(c => c.Name == selectedCase.Name) ?? -1;
                if (selectedIndex >= 0)
                {
                    option.Index = selectedIndex;
                    // 存储选中的值到 Data
                    option.Data ??= new System.Collections.Generic.Dictionary<string, string?>();
                    option.Data[interfaceOption.Name ?? ""] = selectedCase.Name;
                    saveConfigurationAction();
                }
            }
        };

        ComboBoxExtensions.SetDisableNavigationOnLostFocus(comboBox, true);
        ComboBoxExtensions.SetCanSearch(comboBox, true);
        ComboBoxExtensions.SetSearchMemberPath(comboBox, "DisplayName");
        comboBox.Bind(ComboBoxExtensions.SearchWatermarkProperty, new Lang.Avalonia.MarkupExtensions.I18nBinding(Lang.LangKeys.Search));

        // Header
        var labelPanel = TaskOptionGenerator.CreateLabelPanel(option.DisplayName, option.Name, interfaceOption.Description, interfaceOption.Document);
        var icon = TaskOptionGenerator.CreateIcon(interfaceOption);
        icon.Margin = new Thickness(10, 0, 6, 0);
        labelPanel.Children.Insert(0, icon);

        // 刷新按钮
        var refreshButton = new Button
        {
            Content = new SymbolIcon { Symbol = FluentIcons.Common.Symbol.Regular.ArrowSync24 },
            ToolTip.Tip = "刷新扫描结果",
            Margin = new Thickness(8, 0, 0, 0)
        };

        refreshButton.Click += (_, _) =>
        {
            UpdateComboBoxItems(comboBox, interfaceOption, option);
            saveConfigurationAction();
        };

        Grid.SetColumn(labelPanel, 0);
        Grid.SetColumn(comboBox, 1);
        Grid.SetColumn(refreshButton, 2);

        TaskOptionGenerator.AddResponsiveBehavior(grid, labelPanel, comboBox);

        grid.Children.Add(labelPanel);
        grid.Children.Add(comboBox);
        grid.Children.Add(refreshButton);

        wrapper.Children.Add(grid);

        return wrapper;
    }

    private static void UpdateComboBoxItems(
        ComboBox comboBox, 
        MaaInterfaceOption option,
        MaaInterfaceSelectOption selectOption)
    {
        var scanDir = option.ScanDir;
        if (string.IsNullOrEmpty(scanDir))
        {
            comboBox.ItemsSource = option.Cases;
            return;
        }

        // 解析相对路径（相对于程序根目录）
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var fullPath = Path.IsPathRooted(scanDir) ? scanDir : Path.Combine(baseDir, scanDir);
        
        if (!Directory.Exists(fullPath))
        {
            LoggerHelper.Error($"scan_select: 目录不存在: {fullPath}");
            comboBox.ItemsSource = option.Cases;
            return;
        }

        var filter = string.IsNullOrEmpty(option.ScanFilter) ? "*.*" : option.ScanFilter;
        
        try
        {
            var files = Directory.GetFiles(fullPath, filter, SearchOption.TopDirectoryOnly)
                .Select(p => Path.GetFileNameWithoutExtension(p))
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name)
                .Select(name => new MaaInterfaceOptionCase
                {
                    Name = name,
                    DisplayName = name
                })
                .ToList();

            if (files.Count == 0)
            {
                LoggerHelper.Warning($"scan_select: 未找到匹配文件: {filter}");
            }

            // 更新 interface option 的 cases
            option.Cases = files;
            comboBox.ItemsSource = files;

            // 恢复选中状态
            if (selectOption.Index.HasValue && selectOption.Index.Value >= 0 && selectOption.Index.Value < files.Count)
            {
                comboBox.SelectedIndex = selectOption.Index.Value;
            }
            else if (files.Count > 0)
            {
                comboBox.SelectedIndex = 0;
                selectOption.Index = 0;
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"scan_select: 扫描失败: {ex.Message}");
            comboBox.ItemsSource = option.Cases;
        }
    }
}
