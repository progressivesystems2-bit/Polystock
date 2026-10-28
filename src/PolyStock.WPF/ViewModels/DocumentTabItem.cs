using System.Windows.Input;
using PolyStock.Core.Common;

namespace PolyStock.WPF.ViewModels;

public class DocumentTabItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public object ContentViewModel { get; set; } = null!;
    public bool CanClose { get; set; } = true;
    public ICommand? CloseCommand { get; set; }
}
