using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using PolyStock.Core.Models;

namespace PolyStock.WPF.Controls;

public class ItemCommittedEventArgs : RoutedEventArgs
{
    public Item? Item { get; }
    public bool IsEmpty { get; }

    public ItemCommittedEventArgs(RoutedEvent routedEvent, object source, Item? item, bool isEmpty)
        : base(routedEvent, source)
    {
        Item = item;
        IsEmpty = isEmpty;
    }
}

public partial class SearchableItemBox : UserControl
{
    private bool _isUpdatingText = false;
    private bool _isNavigatingPopup = false;

    public SearchableItemBox()
    {
        InitializeComponent();
        Loaded += (s, e) => UpdateVisualStates();
    }

    #region Dependency Properties

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(Item),
            typeof(SearchableItemBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public Item? SelectedItem
    {
        get => (Item?)GetValue(SelectedItemProperty);
        set
        {
            if (Equals(GetValue(SelectedItemProperty), value))
            {
                return;
            }

            try
            {
                var binding = BindingOperations.GetBindingExpression(this, SelectedItemProperty);
                if (binding != null && (DataContext == null || binding.DataItem == null || binding.Status != BindingStatus.Active || binding.DataItem.ToString() == "{DisconnectedItem}" || binding.DataItem.GetType().Name == "NamedObject"))
                {
                    // Target binding source is unattached, null or disconnected.
                    // Bypass pushing through two-way binding to prevent WPF PropertyPathWorker.DetermineWhetherDBNullIsValid NullReferenceException.
                    return;
                }

                SetValue(SelectedItemProperty, value);
            }
            catch (NullReferenceException)
            {
                // Suppress WPF internal PropertyPathWorker.DetermineWhetherDBNullIsValid crash on detached/null DataContext
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchableItemBox] SelectedItem set exception: {ex.Message}");
            }
        }
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SearchableItemBox control)
        {
            control.UpdateTextFromSelectedItem();
        }
    }

    public static readonly DependencyProperty AllItemsProperty =
        DependencyProperty.Register(
            nameof(AllItems),
            typeof(IEnumerable<Item>),
            typeof(SearchableItemBox),
            new PropertyMetadata(null, OnAllItemsChanged));

    public IEnumerable<Item>? AllItems
    {
        get => (IEnumerable<Item>?)GetValue(AllItemsProperty);
        set => SetValue(AllItemsProperty, value);
    }

    private static void OnAllItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SearchableItemBox control && control.PART_Popup.IsOpen)
        {
            control.RefreshFilter(control.PART_TextBox.Text);
        }
    }

    public static readonly DependencyProperty PriorityHeadGroupIdProperty =
        DependencyProperty.Register(
            nameof(PriorityHeadGroupId),
            typeof(int),
            typeof(SearchableItemBox),
            new PropertyMetadata(0, OnPriorityChanged));

    public int PriorityHeadGroupId
    {
        get => (int)GetValue(PriorityHeadGroupIdProperty);
        set => SetValue(PriorityHeadGroupIdProperty, value);
    }

    public static readonly DependencyProperty PriorityBrandIdProperty =
        DependencyProperty.Register(
            nameof(PriorityBrandId),
            typeof(int),
            typeof(SearchableItemBox),
            new PropertyMetadata(0, OnPriorityChanged));

    public int PriorityBrandId
    {
        get => (int)GetValue(PriorityBrandIdProperty);
        set => SetValue(PriorityBrandIdProperty, value);
    }

    private static void OnPriorityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SearchableItemBox control && control.PART_Popup.IsOpen)
        {
            control.RefreshFilter(control.PART_TextBox.Text);
        }
    }

    public static readonly DependencyProperty SearchPrefixProperty =
        DependencyProperty.Register(
            nameof(SearchPrefix),
            typeof(string),
            typeof(SearchableItemBox),
            new PropertyMetadata(null, OnSearchPrefixChanged));

    public string? SearchPrefix
    {
        get => (string?)GetValue(SearchPrefixProperty);
        set => SetValue(SearchPrefixProperty, value);
    }

    private static void OnSearchPrefixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SearchableItemBox control && control.SelectedItem == null && !string.IsNullOrEmpty(e.NewValue as string))
        {
            control.ApplySearchPrefix((string)e.NewValue);
        }
    }

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder),
            typeof(string),
            typeof(SearchableItemBox),
            new PropertyMetadata("Search or select item..."));

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    #endregion

    #region Routed Events

    public static readonly RoutedEvent ItemCommittedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ItemCommitted),
            RoutingStrategy.Bubble,
            typeof(EventHandler<ItemCommittedEventArgs>),
            typeof(SearchableItemBox));

    public event EventHandler<ItemCommittedEventArgs> ItemCommitted
    {
        add => AddHandler(ItemCommittedEvent, value);
        remove => RemoveHandler(ItemCommittedEvent, value);
    }

    private void RaiseItemCommitted(Item? item, bool isEmpty)
    {
        RaiseEvent(new ItemCommittedEventArgs(ItemCommittedEvent, this, item, isEmpty));
    }

    #endregion

    #region Public Methods

    public void ApplySearchPrefix(string prefix)
    {
        if (SelectedItem != null) return;

        _isUpdatingText = true;
        PART_TextBox.Text = prefix;
        _isUpdatingText = false;
        PART_TextBox.CaretIndex = prefix.Length;
        UpdateVisualStates();
    }

    public void FocusInput()
    {
        PART_TextBox.Focus();
        if (SelectedItem == null && !string.IsNullOrEmpty(SearchPrefix))
        {
            if (string.IsNullOrEmpty(PART_TextBox.Text))
            {
                ApplySearchPrefix(SearchPrefix);
            }
            PART_TextBox.CaretIndex = PART_TextBox.Text.Length;
            OpenDropDown();
        }
        else
        {
            PART_TextBox.SelectAll();
        }
    }

    #endregion

    #region Event Handlers & Filtering

    private void UpdateTextFromSelectedItem()
    {
        _isUpdatingText = true;
        PART_TextBox.Text = SelectedItem?.ItemName ?? string.Empty;
        _isUpdatingText = false;
        UpdateVisualStates();
    }

    private void UpdateVisualStates()
    {
        if (PART_Placeholder == null || ClearButton == null || PART_TextBox == null) return;
        bool hasText = !string.IsNullOrEmpty(PART_TextBox.Text);
        PART_Placeholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        ClearButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshFilter(string? searchText = null)
    {
        if (AllItems == null)
        {
            PART_ListBox.ItemsSource = null;
            NoMatchesText.Visibility = Visibility.Visible;
            PriorityHeaderBorder.Visibility = Visibility.Collapsed;
            return;
        }

        var filtered = SearchableItemWrapper.FilterAndSortItems(
            AllItems,
            searchText,
            PriorityHeadGroupId,
            PriorityBrandId);

        PART_ListBox.ItemsSource = filtered;
        NoMatchesText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (filtered.Count > 0)
        {
            PART_ListBox.SelectedIndex = 0;
            PART_ListBox.ScrollIntoView(filtered[0]);
        }
        else
        {
            PART_ListBox.SelectedIndex = -1;
        }

        bool hasPriority = (PriorityHeadGroupId > 0 || PriorityBrandId > 0) &&
                           filtered.Any(w => w.IsPriorityMatch);
        PriorityHeaderBorder.Visibility = hasPriority ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenDropDown()
    {
        RefreshFilter(PART_TextBox.Text);
        PART_Popup.IsOpen = true;
    }

    private void PART_TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateVisualStates();

        if (_isUpdatingText || _isNavigatingPopup) return;

        // When user types, automatically open dropdown and filter
        if (PART_TextBox.IsFocused)
        {
            OpenDropDown();
        }
    }

    private void PART_TextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null && !string.IsNullOrEmpty(SearchPrefix))
        {
            if (string.IsNullOrEmpty(PART_TextBox.Text))
            {
                ApplySearchPrefix(SearchPrefix);
            }
            PART_TextBox.CaretIndex = PART_TextBox.Text.Length;
            OpenDropDown();
        }
        else
        {
            PART_TextBox.SelectAll();
        }
    }

    private void PART_TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (PART_Popup.IsOpen && !IsKeyboardFocusWithin)
        {
            PART_Popup.IsOpen = false;
            // Restore valid text if no match committed
            _isUpdatingText = true;
            PART_TextBox.Text = SelectedItem?.ItemName ?? string.Empty;
            _isUpdatingText = false;
            UpdateVisualStates();
        }
    }

    private void PART_TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (!PART_Popup.IsOpen)
            {
                OpenDropDown();
                e.Handled = true;
                return;
            }

            if (PART_ListBox.Items.Count > 0)
            {
                _isNavigatingPopup = true;
                int next = PART_ListBox.SelectedIndex + 1;
                if (next < PART_ListBox.Items.Count)
                {
                    PART_ListBox.SelectedIndex = next;
                    PART_ListBox.ScrollIntoView(PART_ListBox.SelectedItem);
                }
                _isNavigatingPopup = false;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Up)
        {
            if (PART_Popup.IsOpen && PART_ListBox.Items.Count > 0)
            {
                _isNavigatingPopup = true;
                int prev = PART_ListBox.SelectedIndex - 1;
                if (prev >= 0)
                {
                    PART_ListBox.SelectedIndex = prev;
                    PART_ListBox.ScrollIntoView(PART_ListBox.SelectedItem);
                }
                _isNavigatingPopup = false;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CommitCurrentSelection();
        }
        else if (e.Key == Key.Escape)
        {
            if (PART_Popup.IsOpen)
            {
                PART_Popup.IsOpen = false;
                _isUpdatingText = true;
                PART_TextBox.Text = SelectedItem?.ItemName ?? string.Empty;
                _isUpdatingText = false;
                UpdateVisualStates();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Tab)
        {
            if (PART_Popup.IsOpen && PART_ListBox.SelectedItem != null)
            {
                CommitCurrentSelection();
            }
        }
    }

    private void CommitCurrentSelection()
    {
        try
        {
            if (PART_Popup.IsOpen && PART_ListBox.SelectedItem is SearchableItemWrapper wrapper)
            {
                PART_Popup.IsOpen = false;
                if (!Equals(SelectedItem, wrapper.Item))
                {
                    SelectedItem = wrapper.Item;
                }
                _isUpdatingText = true;
                PART_TextBox.Text = wrapper.Item.ItemName;
                _isUpdatingText = false;
                UpdateVisualStates();
                RaiseItemCommitted(wrapper.Item, false);
                return;
            }

            string text = PART_TextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                PART_Popup.IsOpen = false;
                if (SelectedItem != null)
                {
                    SelectedItem = null;
                }
                UpdateVisualStates();
                RaiseItemCommitted(null, true);
                return;
            }

            // Try resolving best match from AllItems
            if (AllItems != null)
            {
                var resolved = SearchableItemWrapper.FilterAndSortItems(
                    AllItems,
                    text,
                    PriorityHeadGroupId,
                    PriorityBrandId).FirstOrDefault();

                if (resolved != null)
                {
                    PART_Popup.IsOpen = false;
                    if (!Equals(SelectedItem, resolved.Item))
                    {
                        SelectedItem = resolved.Item;
                    }
                    _isUpdatingText = true;
                    PART_TextBox.Text = resolved.Item.ItemName;
                    _isUpdatingText = false;
                    UpdateVisualStates();
                    RaiseItemCommitted(resolved.Item, false);
                    return;
                }
            }

            if (SelectedItem != null)
            {
                PART_Popup.IsOpen = false;
                RaiseItemCommitted(SelectedItem, false);
            }
            else
            {
                PART_Popup.IsOpen = false;
                RaiseItemCommitted(null, string.IsNullOrWhiteSpace(text));
            }
        }
        catch (NullReferenceException)
        {
            PART_Popup.IsOpen = false;
            RaiseItemCommitted(SelectedItem, string.IsNullOrWhiteSpace(PART_TextBox.Text));
        }
    }

    private void DropDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (PART_Popup.IsOpen)
        {
            PART_Popup.IsOpen = false;
        }
        else
        {
            PART_TextBox.Focus();
            OpenDropDown();
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _isUpdatingText = true;
        PART_TextBox.Text = string.Empty;
        _isUpdatingText = false;
        SelectedItem = null;
        UpdateVisualStates();
        PART_TextBox.Focus();
        if (PART_Popup.IsOpen)
        {
            RefreshFilter(string.Empty);
        }
    }

    private void PART_ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item != null && item.DataContext is SearchableItemWrapper wrapper)
        {
            e.Handled = true;
            SelectedItem = wrapper.Item;
            _isUpdatingText = true;
            PART_TextBox.Text = wrapper.Item.ItemName;
            _isUpdatingText = false;
            PART_Popup.IsOpen = false;
            UpdateVisualStates();
            RaiseItemCommitted(wrapper.Item, false);
        }
    }

    private void PART_Popup_Opened(object sender, EventArgs e)
    {
        if (PART_ListBox.SelectedItem != null)
        {
            PART_ListBox.ScrollIntoView(PART_ListBox.SelectedItem);
        }
    }

    private void PART_Popup_Closed(object sender, EventArgs e)
    {
        // Clean up visual states if needed
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T target) return target;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    #endregion
}
