using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Services;

namespace PolyStock.WPF.ViewModels;

public class BrandViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private Brand? _selectedBrand;
    private string _name = string.Empty;
    private string? _description;
    private string _searchText = string.Empty;

    public BrandViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        Brands = new ObservableCollection<Brand>();
        FilteredBrands = new ObservableCollection<Brand>();

        SaveCommand = new RelayCommand(Save);
        NewCommand = new RelayCommand(ClearForm);
        DeleteCommand = new RelayCommand(Delete, () => SelectedBrand != null);

        LoadData();
    }

    public ObservableCollection<Brand> Brands { get; }
    public ObservableCollection<Brand> FilteredBrands { get; }

    public Brand? SelectedBrand
    {
        get => _selectedBrand;
        set
        {
            if (SetProperty(ref _selectedBrand, value))
            {
                if (value != null)
                {
                    Name = value.Name;
                    Description = value.Description;
                }
            }
        }
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
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
        Brands.Clear();
        var list = _inventoryService.GetAllBrands();
        foreach (var item in list) Brands.Add(item);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredBrands.Clear();
        var query = Brands.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(b => b.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     (b.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        foreach (var item in query) FilteredBrands.Add(item);
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            MessageBox.Show("Please enter a Brand Name.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var brand = SelectedBrand ?? new Brand();
            brand.Name = Name.Trim();
            brand.Description = Description?.Trim();
            brand.CreatedAt = DateTime.Now;

            _inventoryService.SaveBrand(brand);
            LoadData();
            ClearForm();
            MessageBox.Show("Brand saved successfully!", "PolyStock", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving Brand: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearForm()
    {
        _selectedBrand = null;
        OnPropertyChanged(nameof(SelectedBrand));
        Name = string.Empty;
        Description = string.Empty;
    }

    private void Delete()
    {
        if (SelectedBrand == null) return;
        var result = MessageBox.Show($"Are you sure you want to delete '{SelectedBrand.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _inventoryService.DeleteBrand(SelectedBrand.Id);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
