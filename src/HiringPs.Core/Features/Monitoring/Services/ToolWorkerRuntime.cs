using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

// A separate heartbeat stays fresh even when discovery or OCR occupies the collection loop.
/// <summary>حالة العامل داخل الذاكرة؛ تستخدم نبضات الحياة لنقل ملخصها إلى القاعدة.</summary>
public sealed class ToolWorkerRuntime
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    private CollectionRuntimeSnapshot snapshot = new(false);
    public CollectionRuntimeSnapshot Snapshot => Volatile.Read(ref snapshot);

    /// <summary>يسجل بدء جولة الجمع في حالة العامل داخل الذاكرة.</summary>
    public void Begin() =>
        Volatile.Write(
            ref snapshot,
            Snapshot with
            {
                Collecting = true,
                LastLoopAt = DateTimeOffset.UtcNow,
            }
        );

    /// <summary>ينهي علامة الجمع عند الخروج أو الإلغاء دون ادعاء نجاح الجولة.</summary>
    public void Stop() => Volatile.Write(ref snapshot, Snapshot with { Collecting = false });

    /// <summary>يسجل نجاح جولة الجمع وتوقيتها في حالة العامل.</summary>
    public void Succeed() =>
        Volatile.Write(
            ref snapshot,
            Snapshot with
            {
                Collecting = false,
                LastSuccessAt = DateTimeOffset.UtcNow,
                ErrorType = "",
            }
        );

    /// <summary>يسجل فشل الجولة بنوع الخطأ دون تفاصيل الاستثناء الحساسة.</summary>
    public void Fail(Exception error) =>
        Volatile.Write(
            ref snapshot,
            Snapshot with
            {
                Collecting = false,
                LastFailureAt = DateTimeOffset.UtcNow,
                ErrorType = error.GetType().Name.Truncate(100),
            }
        );
}
