using System;
using PolyStock.Core.Common;

namespace PolyStock.Core.Models;

public class RolePermission : ObservableObject
{
    private int _id;
    private int _roleId;
    private string _moduleKey = string.Empty;
    private bool _canView;
    private bool _canCreate;
    private bool _canEdit;
    private bool _canDelete;
    private bool _canPrint;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public int RoleId
    {
        get => _roleId;
        set => SetProperty(ref _roleId, value);
    }

    public string ModuleKey
    {
        get => _moduleKey;
        set => SetProperty(ref _moduleKey, value);
    }

    public bool CanView
    {
        get => _canView;
        set => SetProperty(ref _canView, value);
    }

    public bool CanCreate
    {
        get => _canCreate;
        set => SetProperty(ref _canCreate, value);
    }

    public bool CanEdit
    {
        get => _canEdit;
        set => SetProperty(ref _canEdit, value);
    }

    public bool CanDelete
    {
        get => _canDelete;
        set => SetProperty(ref _canDelete, value);
    }

    public bool CanPrint
    {
        get => _canPrint;
        set => SetProperty(ref _canPrint, value);
    }

    // Informational metadata
    public string ModuleName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "📄";

    public RolePermission Clone()
    {
        return new RolePermission
        {
            Id = this.Id,
            RoleId = this.RoleId,
            ModuleKey = this.ModuleKey,
            CanView = this.CanView,
            CanCreate = this.CanCreate,
            CanEdit = this.CanEdit,
            CanDelete = this.CanDelete,
            CanPrint = this.CanPrint,
            ModuleName = this.ModuleName,
            Category = this.Category,
            Description = this.Description,
            Icon = this.Icon
        };
    }
}
