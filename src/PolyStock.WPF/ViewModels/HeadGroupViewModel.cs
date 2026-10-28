using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Services;

namespace PolyStock.WPF.ViewModels;

public class HeadGroupViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private HeadGroup? _selectedHeadGroup;
    private string _name = string.Empty;
    private string? _description;
    private string _searchText = string.Empty;

    public HeadGroupViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        HeadGroups = new ObservableCollection<HeadGroup>();
        FilteredHeadGroups = new ObservableCollection<HeadGroup>();

        SaveCommand = new RelayCommand(Save);
        NewCommand = new RelayCommand(ClearForm);
        DeleteCommand = new RelayCommand(Delete, () => SelectedHeadGroup != null);

        LoadData();
    }

    public ObservableCollection<HeadGroup> HeadGroups { get; }
    public ObservableCollection<HeadGroup> FilteredHeadGroups { get; }

    public HeadGroup? SelectedHeadGroup
    {
        get => _selectedHeadGroup;
        set
        {
            if (SetProperty(ref _selectedHeadGroup, value))
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
        HeadGroups.Clear();
        var list = _inventoryService.GetAllHeadGroups();
        foreach (var item in list) HeadGroups.Add(item);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredHeadGroups.Clear();
        var query = HeadGroups.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(h => h.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     (h.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        foreach (var item in query) FilteredHeadGroups.Add(item);
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            MessageBox.Show("Please enter a Head Group Name.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var hg = SelectedHeadGroup ?? new HeadGroup();
            hg.Name = Name.Trim();
            hg.Description = Description?.Trim();
            hg.CreatedAt = DateTime.Now;

            _inventoryService.SaveHeadGroup(hg);
            LoadData();
            ClearForm();
            MessageBox.Show("Head Group saved successfully!", "PolyStock", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving Head Group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearForm()
    {
        _selectedHeadGroup = null;
        OnPropertyChanged(nameof(SelectedHeadGroup));
        Name = string.Empty;
        Description = string.Empty;
    }

    private void Delete()
    {
        if (SelectedHeadGroup == null) return;
        var result = MessageBox.Show($"Are you sure you want to delete '{SelectedHeadGroup.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _inventoryService.DeleteHeadGroup(SelectedHeadGroup.Id);
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
