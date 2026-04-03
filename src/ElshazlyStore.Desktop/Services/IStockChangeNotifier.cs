namespace ElshazlyStore.Desktop.Services;

/// <summary>
/// Lightweight in-app signal that fires after any inventory-impacting operation.
/// Consumers (e.g., VariantsViewModel) subscribe to <see cref="StockChanged"/>
/// to invalidate caches and refresh displayed quantities.
/// </summary>
public interface IStockChangeNotifier
{
    event EventHandler? StockChanged;
    void NotifyStockChanged();
}

public sealed class StockChangeNotifier : IStockChangeNotifier
{
    public event EventHandler? StockChanged;

    public void NotifyStockChanged()
        => StockChanged?.Invoke(this, EventArgs.Empty);
}
