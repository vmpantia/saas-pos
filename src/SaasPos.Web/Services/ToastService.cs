namespace SaasPos.Web.Services;

public enum ToastKind { Success, Error }

public record ToastMessage(Guid Id, string Text, ToastKind Kind);

public class ToastService
{
    public event Action? Changed;
    private readonly List<ToastMessage> _items = [];
    public IReadOnlyList<ToastMessage> Items => _items;

    public void Show(string text, ToastKind kind = ToastKind.Success)
    {
        var t = new ToastMessage(Guid.NewGuid(), text, kind);
        _items.Add(t);
        Changed?.Invoke();
        _ = Task.Delay(4000).ContinueWith(_ => Dismiss(t.Id));
    }

    public void Dismiss(Guid id)
    {
        if (_items.RemoveAll(i => i.Id == id) > 0) Changed?.Invoke();
    }
}
