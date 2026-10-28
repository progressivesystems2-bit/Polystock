using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Services;

namespace PolyStock.WPF.ViewModels;

public class GodownViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private Godown? _selectedGodown;
    private string _code = string.Empty;
    private string _name = string.Empty;
    private string? _location;
    private string? _contactPerson;
    private string? _phone;
    private string? _description;
    private bool _isActive = true;
    private string _searchText = string.Empty;

    public GodownViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        Godowns = new ObservableCollection<Godown>();
        FilteredGodowns = new ObservableCollection<Godown>();

        SaveCommand = new RelayCommand(Save);
        NewCommand = new RelayCommand(ClearForm);
        DeleteCommand = new RelayCommand(Delete, CanDelete);

        LoadData();
    }

    public ObservableCollection<Godown> Godowns { get; }
    public ObservableCollection<Godown> FilteredGodowns { get; }

    public Godown? SelectedGodown
    {
        get => _selectedGodown;
        set
        {
            if (SetProperty(ref _selectedGodown, value))
            {
                if (value != null)
                {
                    Code = value.Code;
                    Name = value.Name;
                    Location = value.Location;
                    ContactPerson = value.ContactPerson;
                    Phone = value.Phone;
                    Description = value.Description;
                    IsActive = value.IsActive;
                }
            }
        }
    }

    public string Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? Location
    {
        get => _location;
        set => SetProperty(ref _location, value);
    }

    public string? ContactPerson
    {
        get => _contactPerson;
        set => SetProperty(ref _contactPerson, value);
    }

    public string? Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
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
        Godowns.Clear();
        foreach (var g in _inventoryService.GetAllGodowns())
        {
            Godowns.Add(g);
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredGodowns.Clear();
        var q = SearchText.Trim().ToLowerInvariant();
        var result = string.IsNullOrEmpty(q)
            ? Godowns
            : Godowns.Where(g => g.Name.ToLowerInvariant().Contains(q) ||
                                 g.Code.ToLowerInvariant().Contains(q) ||
                                 (g.Location?.ToLowerInvariant().Contains(q) ?? false) ||
                                 (g.ContactPerson?.ToLowerInvariant().Contains(q) ?? false));

        foreach (var g in result)
        {
            FilteredGodowns.Add(g);
        }
    }

    private bool CanDelete() => SelectedGodown != null;

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            MessageBox.Show("Please enter Godown Name.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var godown = SelectedGodown ?? new Godown();
            godown.Code = Code?.Trim() ?? string.Empty;
            godown.Name = Name.Trim();
            godown.Location = Location?.Trim();
            godown.ContactPerson = ContactPerson?.Trim();
            godown.Phone = Phone?.Trim();
            godown.Description = Description?.Trim();
            godown.IsActive = IsActive;
            godown.CreatedAt = DateTime.Now;

            _inventoryService.SaveGodown(godown);
            LoadData();
            ClearForm();
            MessageBox.Show($"Godown '{godown.Name}' saved successfully!", "PolyStock", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving Godown: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearForm()
    {
        _selectedGodown = null;
        OnPropertyChanged(nameof(SelectedGodown));
        Code = string.Empty;
        Name = string.Empty;
        Location = string.Empty;
        ContactPerson = string.Empty;
        Phone = string.Empty;
        Description = string.Empty;
        IsActive = true;
    }

    private void Delete()
    {
        if (SelectedGodown == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete Godown '{SelectedGodown.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _inventoryService.DeleteGodown(SelectedGodown.Id);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot delete Godown: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
