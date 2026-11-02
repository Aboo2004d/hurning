namespace HiringPs.Core;

/// <summary>Plain-text help with isolated command lines for predictable Arabic/Latin layout.</summary>
public static class TelegramHelp
{
    private static string Command(string value) => "\u2066" + value + "\u2069";

    public static string Text(bool english) => english
        ? $"""
            📋 Bot help

            🔎 Search for jobs
            {Command("/search")}
            Choose a search criterion, then select from the buttons.

            👤 Start and account status
            {Command("/start")}

            📦 Subscription and alerts
            {Command("/status")}

            🔕 Stop opportunity alerts
            {Command("/stop")}
            To resume alerts, open account settings. Payment and account messages remain enabled.

            💳 Check a payment
            {Command("/payment_status ORDER_ID")}
            Replace ORDER_ID with the order ID from your account.

            ❓ Show this guide
            {Command("/help")}

            Use the buttons below to search or open your account.
            """
        : $"""
            📋 دليل استخدام البوت

            🔎 البحث عن وظائف
            {Command("/search")}
            اختر طريقة البحث، ثم اختر من القوائم بالأزرار.
            يمكنك أيضًا إرسال كلمة «بحث».

            👤 بدء الاستخدام وحالة الحساب
            {Command("/start")}

            📦 الاشتراك وحالة التنبيهات
            {Command("/status")}

            🔕 إيقاف تنبيهات الفرص
            {Command("/stop")}
            لإعادة التفعيل افتح إعدادات الحساب. تستمر رسائل الدفع والحساب.

            💳 التحقق من الدفع
            {Command("/payment_status ORDER_ID")}
            استبدل ORDER_ID بمعرّف الطلب من حسابك.

            ❓ عرض هذه المساعدة
            {Command("/help")}

            استخدم الأزرار أدناه للبحث أو فتح حسابك.
            """;
}
