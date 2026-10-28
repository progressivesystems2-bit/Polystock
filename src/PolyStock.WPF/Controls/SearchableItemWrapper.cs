using System;
using System.Collections.Generic;
using System.Linq;
using PolyStock.Core.Models;

namespace PolyStock.WPF.Controls;

public class SearchableItemWrapper
{
    public Item Item { get; set; } = null!;
    public bool IsPriorityMatch { get; set; }
    public bool HasBrand => !string.IsNullOrWhiteSpace(Item.BrandName);
    public bool HasHeadGroup => !string.IsNullOrWhiteSpace(Item.HeadGroupName);
    public string RateText => Item.DefaultRatePerKg > 0 ? $"₹ {Item.DefaultRatePerKg:F2}/kg" : string.Empty;

    public static string GetPrefixUpToHeadGroupAndBrand(Item? item)
    {
        if (item == null) return string.Empty;

        string name = (item.ItemName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name)) return string.Empty;

        string hg = (item.HeadGroupName ?? string.Empty).Trim();
        string brand = (item.BrandName ?? string.Empty).Trim();
        string code = (item.ItemCode ?? string.Empty).Trim();

        // 1. Direct match: HeadGroup + Brand prefix (e.g. "CC Balaji 7" -> "CC Balaji ")
        if (!string.IsNullOrEmpty(hg) && !string.IsNullOrEmpty(brand))
        {
            string p1 = $"{hg} {brand}";
            if (name.StartsWith(p1, StringComparison.OrdinalIgnoreCase))
            {
                return p1 + " ";
            }

            string p2 = $"{brand} {hg}";
            if (name.StartsWith(p2, StringComparison.OrdinalIgnoreCase))
            {
                return p2 + " ";
            }
        }

        // 2. If name ends with ItemCode (e.g. "CC Balaji 7" with code "7", or "Reliance Repol H030SG")
        if (!string.IsNullOrEmpty(code) && name.EndsWith(code, StringComparison.OrdinalIgnoreCase) && name.Length > code.Length)
        {
            string withoutCode = name.Substring(0, name.Length - code.Length).TrimEnd();
            if (!string.IsNullOrWhiteSpace(withoutCode))
            {
                return withoutCode + " ";
            }
        }

        // 3. If name starts with Brand
        if (!string.IsNullOrEmpty(brand) && name.StartsWith(brand, StringComparison.OrdinalIgnoreCase))
        {
            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 1)
            {
                return string.Join(" ", words.Take(words.Length - 1)) + " ";
            }
            return brand + " ";
        }

        // 4. If name starts with HeadGroup
        if (!string.IsNullOrEmpty(hg) && name.StartsWith(hg, StringComparison.OrdinalIgnoreCase))
        {
            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 1)
            {
                return string.Join(" ", words.Take(words.Length - 1)) + " ";
            }
            return hg + " ";
        }

        // 5. Fallback to Brand or HeadGroup if known
        if (!string.IsNullOrEmpty(brand)) return brand + " ";
        if (!string.IsNullOrEmpty(hg)) return hg + " ";

        // 6. Generic multi-word strip last word
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
        {
            return string.Join(" ", parts.Take(parts.Length - 1)) + " ";
        }

        return string.Empty;
    }

    public static List<SearchableItemWrapper> FilterAndSortItems(
        IEnumerable<Item>? allItems,
        string? searchText,
        int priorityHeadGroupId,
        int priorityBrandId)
    {
        if (allItems == null) return new List<SearchableItemWrapper>();

        bool hasPriority = priorityHeadGroupId > 0 || priorityBrandId > 0;
        string query = (searchText ?? string.Empty).Trim();

        IEnumerable<Item> matches = allItems;
        if (!string.IsNullOrEmpty(query))
        {
            matches = allItems.Where(i =>
                (i.ItemName != null && i.ItemName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(i.ItemCode) && i.ItemCode.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(i.BrandName) && i.BrandName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(i.HeadGroupName) && i.HeadGroupName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(i.Description) && i.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        // Priority Scoring:
        // 0: Both same HeadGroup AND same Brand (Exact sister grade match from Row 1)
        // 1: Same Brand
        // 2: Same HeadGroup
        // 3: Other items
        var ordered = matches.OrderBy(i =>
        {
            if (hasPriority)
            {
                bool matchesHg = priorityHeadGroupId > 0 && i.HeadGroupId == priorityHeadGroupId;
                bool matchesBrand = priorityBrandId > 0 && i.BrandId == priorityBrandId;

                if (matchesHg && matchesBrand) return 0;
                if (matchesBrand) return 1;
                if (matchesHg) return 2;
            }
            return 3;
        })
        .ThenBy(i =>
        {
            if (!string.IsNullOrEmpty(query))
            {
                if (i.ItemName != null && i.ItemName.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 0;
                if (!string.IsNullOrEmpty(i.ItemCode) && i.ItemCode.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 1;
            }
            return 2;
        })
        .ThenBy(i => i.ItemName)
        .Select(i => new SearchableItemWrapper
        {
            Item = i,
            IsPriorityMatch = hasPriority &&
                ((priorityHeadGroupId > 0 && priorityBrandId > 0 && i.HeadGroupId == priorityHeadGroupId && i.BrandId == priorityBrandId) ||
                 (priorityHeadGroupId > 0 && priorityBrandId == 0 && i.HeadGroupId == priorityHeadGroupId) ||
                 (priorityBrandId > 0 && priorityHeadGroupId == 0 && i.BrandId == priorityBrandId))
        })
        .ToList();

        return ordered;
    }
}
