using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Services;

namespace PolyStock.WPF.ViewModels;

public class ItemCreationViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private Item? _selectedItem;
    private HeadGroup? _selectedHeadGroup;
    private Brand? _selectedBrand;
    private string _itemCode = string.Empty;
    private string _itemName = string.Empty;
    private double _defaultRatePerKg = 40.0;
    private string? _description;
    private string _searchText = string.Empty;

    public ItemCreationViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        HeadGroups = new ObservableCollection<HeadGroup>();
        Brands = new ObservableCollection<Brand>();
        Items = new ObservableCollection<Item>();
        FilteredItems = new ObservableCollection<Item>();

        SaveCommand = new RelayCommand(Save);
        NewCommand = new RelayCommand(ClearForm);
        DeleteCommand = new RelayCommand(Delete, CanDelete);

        LoadData();
    }

    public ObservableCollection<HeadGroup> HeadGroups { get; }
    public ObservableCollection<Brand> Brands { get; }
    public ObservableCollection<Item> Items { get; }
    public ObservableCollection<Item> FilteredItems { get; }

    public Item? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                if (value != null)
                {
                    SelectedHeadGroup = HeadGroups.FirstOrDefault(hg => hg.Id == value.HeadGroupId);
                    SelectedBrand = Brands.FirstOrDefault(b => b.Id == value.BrandId);
                    ItemCode = value.ItemCode;
                    ItemName = value.ItemName;
                    DefaultRatePerKg = value.DefaultRatePerKg;
                    Description = value.Description;
                }
            }
        }
    }

    public HeadGroup? SelectedHeadGroup
    {
        get => _selectedHeadGroup;
        set
        {
            if (SetProperty(ref _selectedHeadGroup, value))
            {
                AutoGenerateItemName();
            }
        }
    }

    public Brand? SelectedBrand
    {
        get => _selectedBrand;
        set
        {
            if (SetProperty(ref _selectedBrand, value))
            {
                AutoGenerateItemName();
            }
        }
    }

    public string ItemCode
    {
        get => _itemCode;
        set
        {
            if (SetProperty(ref _itemCode, value))
            {
                AutoGenerateItemName();
            }
        }
    }

    public string ItemName
    {
        get => _itemName;
        set => SetProperty(ref _itemName, value);
    }

    public double DefaultRatePerKg
    {
        get => _defaultRatePerKg;
        set => SetProperty(ref _defaultRatePerKg, value);
    }

    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand DeleteCommand { get; }

    public void LoadData()
    {
        HeadGroups.Clear();
        foreach (var hg in _inventoryService.GetAllHeadGroups()) HeadGroups.Add(hg);

        Brands.Clear();
        foreach (var b in _inventoryService.GetAllBrands()) Brands.Add(b);

        Items.Clear();
        foreach (var item in _inventoryService.GetAllItems()) Items.Add(item);

        ApplyFilter();
    }

    private void AutoGenerateItemName()
    {
        var hgName = SelectedHeadGroup?.Name?.Trim() ?? string.Empty;
        var brandName = SelectedBrand?.Name?.Trim() ?? string.Empty;
        var code = ItemCode?.Trim() ?? string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(hgName)) parts.Add(hgName);
        if (!string.IsNullOrEmpty(brandName)) parts.Add(brandName);
        if (!string.IsNullOrEmpty(code)) parts.Add(code);

        ItemName = string.Join(" ", parts);
    }

    private void ApplyFilter()
    {
        FilteredItems.Clear();
        var q = SearchText.Trim().ToLowerInvariant();
        var result = string.IsNullOrEmpty(q)
            ? Items
            : Items.Where(i => i.ItemName.ToLowerInvariant().Contains(q) ||
                               i.HeadGroupName.ToLowerInvariant().Contains(q) ||
                               i.BrandName.ToLowerInvariant().Contains(q) ||
                               i.ItemCode.ToLowerInvariant().Contains(q));

        foreach (var item in result) FilteredItems.Add(item);
    }

    private bool CanDelete() => SelectedItem != null;

    private void Save()
    {
        if (SelectedHeadGroup == null)
        {
            MessageBox.Show("Please select a Head Group from the dropdown.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedBrand == null)
        {
            MessageBox.Show("Please select a Brand from the dropdown.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ItemName))
        {
            MessageBox.Show("Please enter an Item Code / Name.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var item = SelectedItem ?? new Item();
            item.HeadGroupId = SelectedHeadGroup.Id;
            item.HeadGroupName = SelectedHeadGroup.Name;
            item.BrandId = SelectedBrand.Id;
            item.BrandName = SelectedBrand.Name;
            item.ItemCode = ItemCode.Trim();
            item.ItemName = ItemName.Trim();
            item.DefaultRatePerKg = DefaultRatePerKg;
            item.Description = Description?.Trim();
            item.CreatedAt = DateTime.Now;

            _inventoryService.SaveItem(item);
            LoadData();
            ClearForm();
            MessageBox.Show($"Item '{item.ItemName}' saved successfully!", "PolyStock", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving Item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearForm()
    {
        _selectedItem = null;
        OnPropertyChanged(nameof(SelectedItem));
        SelectedHeadGroup = null;
        SelectedBrand = null;
        ItemCode = string.Empty;
        ItemName = string.Empty;
        DefaultRatePerKg = 40.0;
        Description = string.Empty;
    }

    private void Delete()
    {
        if (SelectedItem == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete item '{SelectedItem.ItemName}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _inventoryService.DeleteItem(SelectedItem.Id);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot delete item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public void SelectItemByName(string itemName)
    {
        var match = Items.FirstOrDefault(i => i.ItemName.Equals(itemName, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            SelectedItem = match;
        }
    }

    public void SelectItemById(int id)
    {
        var match = Items.FirstOrDefault(i => i.Id == id);
        if (match != null)
        {
            SelectedItem = match;
        }
    }
}
