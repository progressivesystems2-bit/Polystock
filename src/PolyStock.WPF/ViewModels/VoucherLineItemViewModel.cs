using System;
using System.Windows.Input;
using PolyStock.Core.Common;
using PolyStock.Core.Models;

namespace PolyStock.WPF.ViewModels;

public class VoucherLineItemViewModel : ObservableObject
{
    private readonly Action _onChanged;
    private readonly Action<VoucherLineItemViewModel> _onDelete;

    private int _lineNumber;
    private Account? _selectedAccount;
    private double _amount;
    private string _amountText = string.Empty;
    private string? _referenceNo;
    private string? _remarks;

    public VoucherLineItemViewModel(Action onChanged, Action<VoucherLineItemViewModel> onDelete)
    {
        _onChanged = onChanged;
        _onDelete = onDelete;

        DeleteCommand = new RelayCommand(() => _onDelete(this));
    }

    public int LineNumber
    {
        get => _lineNumber;
        set => SetProperty(ref _lineNumber, value);
    }

    public Account? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetProperty(ref _selectedAccount, value))
            {
                OnPropertyChanged(nameof(CurrentBalanceText));
                _onChanged?.Invoke();
            }
        }
    }

    public string CurrentBalanceText
    {
        get
        {
            if (SelectedAccount == null) return "₹ 0.00";
            if (Math.Abs(SelectedAccount.CurrentBalance) < 0.0001) return "₹ 0.00 (Nil)";
            string sign = SelectedAccount.CurrentBalance >= 0 ? "Dr" : "Cr";
            return $"₹ {Math.Abs(SelectedAccount.CurrentBalance):N2} {sign}";
        }
    }

    public double Amount
    {
        get => _amount;
        set
        {
            if (SetProperty(ref _amount, value))
            {
                string currentCleaned = (_amountText ?? string.Empty).Replace(",", "").Trim();
                if (!double.TryParse(currentCleaned, out var currVal) || Math.Abs(currVal - value) > 0.001)
                {
                    _amountText = _amount > 0 ? _amount.ToString("N2") : string.Empty;
                    OnPropertyChanged(nameof(AmountText));
                }
                _onChanged?.Invoke();
            }
        }
    }

    public string AmountText
    {
        get => _amountText;
        set
        {
            if (SetProperty(ref _amountText, value))
            {
                string cleaned = (value ?? string.Empty).Replace(",", "").Trim();
                if (double.TryParse(cleaned, out var d) && d >= 0)
                {
                    _amount = d;
                }
                else if (string.IsNullOrWhiteSpace(cleaned))
                {
                    _amount = 0.0;
                }
                OnPropertyChanged(nameof(Amount));
                _onChanged?.Invoke();
            }
        }
    }

    public void FormatAmount()
    {
        if (_amount > 0)
        {
            _amountText = _amount.ToString("N2");
        }
        else
        {
            _amountText = string.Empty;
        }
        OnPropertyChanged(nameof(AmountText));
    }

    public bool HasData => SelectedAccount != null || Amount > 0.0001 || !string.IsNullOrWhiteSpace(ReferenceNo) || !string.IsNullOrWhiteSpace(Remarks);

    public string? ReferenceNo
    {
        get => _referenceNo;
        set
        {
            if (SetProperty(ref _referenceNo, value))
            {
                _onChanged?.Invoke();
            }
        }
    }

    public string? Remarks
    {
        get => _remarks;
        set
        {
            if (SetProperty(ref _remarks, value))
            {
                _onChanged?.Invoke();
            }
        }
    }

    public ICommand DeleteCommand { get; }
}
