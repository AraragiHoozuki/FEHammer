using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace FEHagemu.Controls;

/// <summary>
/// A virtualizing panel that arranges items in a wrapping grid layout.
/// Only materializes controls for items visible in the current viewport.
/// Must be placed inside a ScrollViewer to function.
/// </summary>
public class VirtualizingWrapPanel : Panel, ILogicalScrollable
{
    #region Styled Properties

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<VirtualizingWrapPanel, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<VirtualizingWrapPanel, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<double> ItemWidthProperty =
        AvaloniaProperty.Register<VirtualizingWrapPanel, double>(nameof(ItemWidth), 100);

    public static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<VirtualizingWrapPanel, double>(nameof(ItemHeight), 100);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<VirtualizingWrapPanel, double>(nameof(Spacing), 4);

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public double ItemWidth
    {
        get => GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    #endregion

    #region Internal State

    private Size _extent;
    private Size _viewport;
    private Vector _offset;
    private int _itemsPerRow = 1;
    private double _rowHeight = 1;
    private EventHandler? _scrollInvalidated;

    // Maps item index -> materialized control
    private readonly Dictionary<int, Control> _realized = new();
    // Recycling pool of detached controls
    private readonly Queue<Control> _pool = new();

    #endregion

    static VirtualizingWrapPanel()
    {
        AffectsMeasure<VirtualizingWrapPanel>(
            ItemsSourceProperty, ItemTemplateProperty,
            ItemWidthProperty, ItemHeightProperty, SpacingProperty);
    }

    public VirtualizingWrapPanel()
    {
        // Nothing extra
    }

    #region Property Changed

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty)
        {
            var oldVal = change.GetOldValue<IEnumerable?>();
            var newVal = change.GetNewValue<IEnumerable?>();

            if (oldVal is INotifyCollectionChanged oldNcc)
                oldNcc.CollectionChanged -= OnCollectionChanged;

            RecycleAll();
            _offset = default;

            if (newVal is INotifyCollectionChanged newNcc)
                newNcc.CollectionChanged += OnCollectionChanged;

            InvalidateMeasure();
        }
        else if (change.Property == ItemTemplateProperty)
        {
            RecycleAll();
            _pool.Clear(); // template changed, old controls invalid
            InvalidateMeasure();
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RecycleAll();
        InvalidateMeasure();
    }

    #endregion

    #region Layout

    protected override Size MeasureOverride(Size availableSize)
    {
        var items = ItemsSource as IList;
        int count = items?.Count ?? 0;

        if (count == 0 || ItemTemplate is null)
        {
            _extent = default;
            _viewport = availableSize;
            RaiseScrollInvalidated();
            return default;
        }

        double itemW = ItemWidth;
        double itemH = ItemHeight;
        double sp = Spacing;
        double usableWidth = availableSize.Width;

        // Calculate grid dimensions
        _itemsPerRow = Math.Max(1, (int)((usableWidth + sp) / (itemW + sp)));
        _rowHeight = itemH + sp;
        int totalRows = (int)Math.Ceiling((double)count / _itemsPerRow);
        double totalHeight = totalRows * _rowHeight - sp;

        _extent = new Size(usableWidth, Math.Max(0, totalHeight));
        _viewport = availableSize;

        // Clamp offset
        double maxOffsetY = Math.Max(0, totalHeight - availableSize.Height);
        if (_offset.Y > maxOffsetY)
            _offset = new Vector(_offset.X, maxOffsetY);

        // Determine visible row range (with 1-row buffer for smooth scrolling)
        int firstRow = Math.Max(0, (int)(_offset.Y / _rowHeight) - 1);
        int lastRow = Math.Min(totalRows - 1, (int)((_offset.Y + availableSize.Height) / _rowHeight) + 1);

        int firstIdx = firstRow * _itemsPerRow;
        int lastIdx = Math.Min(count - 1, (lastRow + 1) * _itemsPerRow - 1);

        // 1. Recycle controls outside visible range
        var toRecycle = new List<int>();
        foreach (var kv in _realized)
        {
            if (kv.Key < firstIdx || kv.Key > lastIdx)
                toRecycle.Add(kv.Key);
        }
        foreach (int idx in toRecycle)
        {
            var ctrl = _realized[idx];
            _realized.Remove(idx);
            Children.Remove(ctrl);
            _pool.Enqueue(ctrl);
        }

        // 2. Realize controls in visible range
        Size childAvailable = new(itemW, itemH);
        for (int i = firstIdx; i <= lastIdx; i++)
        {
            if (!_realized.ContainsKey(i))
            {
                Control ctrl;
                if (_pool.Count > 0)
                {
                    ctrl = _pool.Dequeue();
                    ctrl.DataContext = items![i];
                }
                else
                {
                    ctrl = ItemTemplate.Build(items![i])!;
                    ctrl.DataContext = items[i];
                }
                _realized[i] = ctrl;
                Children.Add(ctrl);
            }
            else
            {
                // Ensure DataContext is up-to-date (items may have been recreated)
                var existing = _realized[i];
                if (existing.DataContext != items![i])
                    existing.DataContext = items[i];
            }

            _realized[i].Measure(childAvailable);
        }

        RaiseScrollInvalidated();

        // Return the viewport size so ScrollViewer knows our desired size
        return new Size(usableWidth, double.IsInfinity(availableSize.Height)
            ? totalHeight
            : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double itemW = ItemWidth;
        double itemH = ItemHeight;
        double sp = Spacing;

        // Recalculate effective item width to stretch-fill the row
        double totalGapWidth = (_itemsPerRow - 1) * sp;
        double effectiveW = (_itemsPerRow > 0)
            ? (finalSize.Width - totalGapWidth) / _itemsPerRow
            : itemW;

        foreach (var kv in _realized)
        {
            int row = kv.Key / _itemsPerRow;
            int col = kv.Key % _itemsPerRow;

            double x = col * (effectiveW + sp);
            double y = row * _rowHeight - _offset.Y;

            kv.Value.Arrange(new Rect(x, y, effectiveW, itemH));
        }

        return finalSize;
    }

    #endregion

    #region Helpers

    private void RecycleAll()
    {
        foreach (var ctrl in _realized.Values)
        {
            Children.Remove(ctrl);
            _pool.Enqueue(ctrl);
        }
        _realized.Clear();
    }

    private void RaiseScrollInvalidated()
    {
        _scrollInvalidated?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Unsubscribe from collection changes
        if (ItemsSource is INotifyCollectionChanged ncc)
            ncc.CollectionChanged -= OnCollectionChanged;

        RecycleAll();
        _pool.Clear();
        base.OnDetachedFromVisualTree(e);
    }

    #endregion

    #region ILogicalScrollable

    bool ILogicalScrollable.CanHorizontallyScroll { get => false; set { } }
    bool ILogicalScrollable.CanVerticallyScroll { get => true; set { } }
    bool ILogicalScrollable.IsLogicalScrollEnabled => true;

    Size ILogicalScrollable.ScrollSize => new Size(16, _rowHeight > 0 ? _rowHeight : 16);
    Size ILogicalScrollable.PageScrollSize => new Size(_viewport.Width, Math.Max(1, _viewport.Height - _rowHeight));

    Size IScrollable.Extent => _extent;
    Size IScrollable.Viewport => _viewport;

    Vector IScrollable.Offset
    {
        get => _offset;
        set
        {
            if (_offset != value)
            {
                _offset = value;
                InvalidateMeasure();
            }
        }
    }

    event EventHandler? ILogicalScrollable.ScrollInvalidated
    {
        add => _scrollInvalidated += value;
        remove => _scrollInvalidated -= value;
    }

    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect) => false;

    Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from) => null;

    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e)
    {
        _scrollInvalidated?.Invoke(this, e);
    }

    #endregion
}
