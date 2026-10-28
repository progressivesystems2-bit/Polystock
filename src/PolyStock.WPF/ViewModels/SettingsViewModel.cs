using System;
using System.Globalization;
using System.Windows.Input;
using PolyStock.Core.Common;
using PolyStock.Services;

namespace PolyStock.WPF.ViewModels;

public class SettingsViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private string _thresholdKgText = "0";
    private string? _statusMessage;
    private bool _isErrorMessage;

    public SettingsViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;

        SaveCommand = new RelayCommand(SaveSettings);
        ResetCommand = new RelayCommand(LoadSettings);

        LoadSettings();
    }

    public ICommand SaveCommand { get; }
    public ICommand ResetCommand { get; }
    public Action? SettingsSavedAction { get; set; }

    public string ThresholdKgText
    {
        get => _thresholdKgText;
        set
        {
            if (SetProperty(ref _thresholdKgText, value))
            {
                OnPropertyChanged(nameof(ThresholdKgValue));
                OnPropertyChanged(nameof(ThresholdStatusSummary));
            }
        }
    }

    public double ThresholdKgValue
    {
        get
        {
            if (double.TryParse(_thresholdKgText, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) && val >= 0)
            {
                return val;
            }
            if (double.TryParse(_thresholdKgText, out var valLocal) && valLocal >= 0)
            {
                return valLocal;
            }
            return 0.0;
        }
    }

    public string ThresholdStatusSummary
    {
        get
        {
            double val = ThresholdKgValue;
            if (val <= 0.0001)
            {
                return "● Standard Mode: Inventory deduction threshold is DISABLED. All sales line items will be deducted from warehouse stock regardless of weight.";
            }

            return $"● Active Threshold: Any sales line item with total weight BELOW {val:0.##} KG will NOT be subtracted from inventory. Sales items ≥ {val:0.##} KG will be deducted normally.";
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsErrorMessage
    {
        get => _isErrorMessage;
        set => SetProperty(ref _isErrorMessage, value);
    }

    public void LoadSettings()
    {
        try
        {
            double current = _inventoryService.GetSalesDeductionThresholdKg();
            _thresholdKgText = current.ToString("0.###", CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(ThresholdKgText));
            OnPropertyChanged(nameof(ThresholdKgValue));
            OnPropertyChanged(nameof(ThresholdStatusSummary));

            StatusMessage = null;
            IsErrorMessage = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load settings: {ex.Message}";
            IsErrorMessage = true;
        }
    }

    public void SaveSettings()
    {
        if (!double.TryParse(_thresholdKgText, NumberStyles.Any, CultureInfo.InvariantCulture, out var threshold) &&
            !double.TryParse(_thresholdKgText, out threshold))
        {
            StatusMessage = "Please enter a valid numeric threshold weight in KG (e.g. 5.0, 10, or 0 to disable).";
            IsErrorMessage = true;
            return;
        }

        if (threshold < 0)
        {
            StatusMessage = "Threshold cannot be negative. Enter 0 to disable threshold.";
            IsErrorMessage = true;
            return;
        }

        try
        {
            _inventoryService.SetSalesDeductionThresholdKg(threshold);
            SettingsSavedAction?.Invoke();

            if (threshold > 0.0001)
            {
                StatusMessage = $"✓ Settings saved successfully! Sales line items below {threshold:0.##} KG will not be subtracted from inventory.";
            }
            else
            {
                StatusMessage = "✓ Settings saved successfully! Threshold disabled (all sales deduct inventory).";
            }
            IsErrorMessage = false;

            OnPropertyChanged(nameof(ThresholdStatusSummary));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
            IsErrorMessage = true;
        }
    }
}
