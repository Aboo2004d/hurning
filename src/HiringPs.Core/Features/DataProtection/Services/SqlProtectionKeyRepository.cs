using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>مستودع مفاتيح SQL مستقل عن معاملات العمل، دون أي مخزن ملفات أو مفاتيح مؤقتة.</summary>
public sealed class SqlProtectionKeyRepository(IConfiguration configuration) : IXmlRepository
{
    /// <summary>يقرأ جميع المفاتيح وسجلات الإلغاء؛ فشل القراءة لا ينشئ مخزنًا بديلًا.</summary>
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandTimeout = 10;
        command.CommandText = "SELECT [Xml] FROM dbo.DataProtectionKeys ORDER BY Id";
        using var reader = command.ExecuteReader();
        var elements = new List<XElement>();
        while (reader.Read()) elements.Add(Parse(reader.GetString(0)));
        return elements;
    }

    /// <summary>يحفظ تعريف المفتاح ذريًا دون حذف السابق؛ تكرار استيراد نفس XML لا يكرره.</summary>
    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandTimeout = 10;
        command.CommandText = """
            INSERT INTO dbo.DataProtectionKeys (FriendlyName, [Xml], CreatedAt)
            SELECT @name, @xml, @at
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.DataProtectionKeys WITH (UPDLOCK, HOLDLOCK) WHERE [Xml] = @xml
            )
            """;
        command.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 256).Value = (friendlyName ?? "").Truncate(256);
        command.Parameters.Add("@xml", System.Data.SqlDbType.NVarChar, -1).Value = element.ToString(SaveOptions.DisableFormatting);
        command.Parameters.AddWithValue("@at", DateTimeOffset.UtcNow);
        command.ExecuteNonQuery();
    }

    /// <summary>يفسر التعريف دون السماح بمستندات DTD أو موارد خارجية.</summary>
    public static XElement Parse(string xml)
    {
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_000_000,
        });
        return XElement.Load(reader);
    }

    private SqlConnection Open()
    {
        var value = configuration.GetConnectionString("Hiring")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Hiring for database protection keys.");
        var options = new SqlConnectionStringBuilder(value) { ConnectTimeout = 10 };
        var connection = new SqlConnection(options.ConnectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }
}
