using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ConversionApp.Models;

namespace ConversionApp.Controls;

public sealed partial class HistoryControl : UserControl
{
    public static readonly DependencyProperty HistoryItemsProperty =
        DependencyProperty.Register("HistoryItems", typeof(ObservableCollection<ConversionHistoryItem>), typeof(HistoryControl), new PropertyMetadata(null));

    public ObservableCollection<ConversionHistoryItem> HistoryItems
    {
        get => (ObservableCollection<ConversionHistoryItem>)GetValue(HistoryItemsProperty);
        set => SetValue(HistoryItemsProperty, value);
    }

    public event EventHandler<ConversionHistoryItem>? ItemClick;

    public HistoryControl()
    {
        this.InitializeComponent();
    }

    private void ListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ConversionHistoryItem item)
        {
            ItemClick?.Invoke(this, item);
        }
    }
}
