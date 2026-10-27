namespace HiringPs.Core;

/// <summary>يحلل الأوامر قبل إنشاء المضيف أو الاتصال بالقاعدة، ويرفض المصدر غير الصالح والخيارات المتعارضة.</summary>
public static class CollectorArguments
{
    /// <summary>يعيد خيارات جولة واحدة أو الفحص؛ غياب الأمر يعيد المساعدة دون أي اتصال خارجي.</summary>
    public static CollectorCommandOptions Parse(string[] args)
    {
        var collect = false; var checks = false; var probe = false; var install = false; var pdf = false; var help = args.Length == 0;
        int? source = null; string? settings = null, environment = null, file = null;
        for (var i = 0; i < args.Length; i++)
        {
            string Value() => ++i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal)
                ? args[i] : throw new ArgumentException("A required command value is missing. Use --help.");
            switch (args[i])
            {
                case "--collect": case "--once": collect = true; break;
                case "--checks": checks = true; break;
                case "--source":
                    if (!int.TryParse(Value(), out var id) || id <= 0) throw new ArgumentException("--source requires a positive source ID.");
                    source = id; break;
                case "--settings": settings = Path.GetFullPath(Value()); break;
                case "--environment": environment = Value(); break;
                case "--probe": probe = true; break;
                case "--install-browser": install = true; break;
                case "--pdf-check": pdf = true; break;
                case "--pdf-read": file = Value(); break;
                case "--help": case "-h": help = true; break;
                default: throw new ArgumentException("Unknown worker option. Use --help.");
            }
        }
        if (source is not null && !collect) throw new ArgumentException("--source requires --collect.");
        var modes = (collect || checks ? 1 : 0) + (probe ? 1 : 0) + (install ? 1 : 0) + (pdf || file is not null ? 1 : 0);
        if (!help && modes != 1) throw new ArgumentException("Choose one worker operation. Use --help.");
        return new(collect, checks, source, settings, environment, probe, install, pdf, file, help);
    }
}
