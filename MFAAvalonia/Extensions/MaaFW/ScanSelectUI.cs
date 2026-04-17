using Avalonia.Controls;
using Avalonia.Layout;
using FluentIcons.Avalonia.Fluent;
using MFAAvalonia.ViewModels.UsersControls;
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
        MaaInterface.MaaInterfaceOption interfaceOption)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        // ComboBox
        var comboBox = new ComboBox
        {
            MinWidth = 200,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // 绑定数据
        UpdateComboBoxItems(comboBox, interfaceOption);

        // 绑定选中值
        if (source.Option?.Data != null && source.Option.Data.ContainsKey(interfaceOption.Name ?? ""))
        {
            var currentValue = source.Option.Data[interfaceOption.Name ?? ""];
            var selectedIndex = interfaceOption.Cases?.FindIndex(c => c.Name == currentValue) ?? -1;
            if (selectedIndex >= 0)
                comboBox.SelectedIndex = selectedIndex;
        }

        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is MaaInterface.MaaInterfaceOptionCase selectedCase)
            {
                source.SetOption(interfaceOption.Name ?? "", selectedCase.Name ?? "");
            }
        };

        panel.Children.Add(comboBox);

        // 刷新按钮
        var refreshButton = new Button
        {
            Content = new SymbolIcon { Symbol = Symbol.Regular.ArrowSync24 },
            ToolTip.Tip = "刷新扫描结果"
        };

        refreshButton.Click += (_, _) =>
        {
            UpdateComboBoxItems(comboBox, interfaceOption);
        };

        panel.Children.Add(refreshButton);

        return panel;
    }

    private static void UpdateComboBoxItems(ComboBox comboBox, MaaInterface.MaaInterfaceOption option)
    {
        var scanDir = option.ScanDir;
        if (string.IsNullOrEmpty(scanDir) || !Directory.Exists(scanDir))
        {
            comboBox.ItemsSource = option.Cases;
            return;
        }

        var filter = string.IsNullOrEmpty(option.ScanFilter) ? "*.*" : option.ScanFilter;
        
        try
        {
            var files = Directory.GetFiles(scanDir, filter, SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name)
                .Select(name => new MaaInterface.MaaInterfaceOptionCase
                {
                    Name = name,
                    DisplayName = name
                })
                .ToList();

            // 保留原有的默认选中状态
            var currentSelected = comboBox.SelectedItem as MaaInterface.MaaInterfaceOptionCase;
            comboBox.ItemsSource = files;

            if (currentSelected != null && files.Any(f => f.Name == currentSelected.Name))
            {
                comboBox.SelectedItem = files.First(f => f.Name == currentSelected.Name);
            }
            else if (files.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }
        catch
        {
            comboBox.ItemsSource = option.Cases;
        }
    }
}
