using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Services;

namespace PolyStock.WPF.ViewModels;

public class InvoiceHistoryViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private bool _isSalesSelected = true;
    private Sale? _selectedSale;
    private Purchase? _selectedPurchase;
    private string _searchText = string.Empty;

    public InvoiceHistoryViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        Sales = new ObservableCollection<Sale>();
        FilteredSales = new ObservableCollection<Sale>();
        Purchases = new ObservableCollection<Purchase>();
        FilteredPurchases = new ObservableCollection<Purchase>();

        RefreshCommand = new RelayCommand(LoadData);
        LoadData();
    }

    public ObservableCollection<Sale> Sales { get; }
    public ObservableCollection<Sale> FilteredSales { get; }
    public ObservableCollection<Purchase> Purchases { get; }
    public ObservableCollection<Purchase> FilteredPurchases { get; }

    public bool IsSalesSelected
    {
        get => _isSalesSelected;
        set
        {
            if (SetProperty(ref _isSalesSelected, value))
            {
                OnPropertyChanged(nameof(IsPurchasesSelected));
            }
        }
    }

    public bool IsPurchasesSelected
    {
        get => !_isSalesSelected;
        set => IsSalesSelected = !value;
    }

    public Sale? SelectedSale
    {
        get => _selectedSale;
        set => SetProperty(ref _selectedSale, value);
    }

    public Purchase? SelectedPurchase
    {
        get => _selectedPurchase;
        set => SetProperty(ref _selectedPurchase, value);
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

    public ICommand RefreshCommand { get; }

    public void LoadData()
    {
        Sales.Clear();
        foreach (var s in _inventoryService.GetAllSales()) Sales.Add(s);

        Purchases.Clear();
        foreach (var p in _inventoryService.GetAllPurchases()) Purchases.Add(p);

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredSales.Clear();
        var salesQuery = Sales.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            salesQuery = salesQuery.Where(s => s.InvoiceNo.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                               s.CustomerName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }
        foreach (var s in salesQuery) FilteredSales.Add(s);

        FilteredPurchases.Clear();
        var purQuery = Purchases.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            purQuery = purQuery.Where(p => p.InvoiceNo.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                           p.SupplierName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }
        foreach (var p in purQuery) FilteredPurchases.Add(p);
    }
}
