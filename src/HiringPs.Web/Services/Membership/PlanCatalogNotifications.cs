using System.Collections.Concurrent;
using System.Threading.Channels;

namespace HiringPs.Web.Services.Membership;

/// <summary>إشعارات إبطال كتالوج الأسعار داخل نسخة الويب؛ لا تحمل أسعارًا أو بيانات مستخدم.</summary>
public sealed class PlanCatalogNotifications
{
    private readonly ConcurrentDictionary<Guid, Channel<bool>> listeners = new();

    /// <summary>ينشئ قناة محدودة لكل صفحة؛ تدمج التعديلات المتتابعة بدل تراكمها في الذاكرة.</summary>
    public (Guid Id, ChannelReader<bool> Reader) Subscribe()
    {
        var channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true,
        });
        var id = Guid.NewGuid();
        listeners[id] = channel;
        return (id, channel.Reader);
    }

    /// <summary>يغلق اشتراك الصفحة عند انقطاعها.</summary>
    public void Unsubscribe(Guid id)
    {
        if (listeners.TryRemove(id, out var channel)) channel.Writer.TryComplete();
    }

    /// <summary>يخطر الصفحات بعد نجاح المعاملة فقط لإعادة جلب بياناتها من الخادم.</summary>
    public void Changed()
    {
        foreach (var channel in listeners.Values) channel.Writer.TryWrite(true);
    }
}
