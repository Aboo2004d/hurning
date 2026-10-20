// نماذج قاعدة البيانات والحالات الأساسية للمصادر والإعلانات ونتائج المعالجة.
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace HiringPs.Core;

/// <summary>مكون DuplicateCandidateStatus ضمن مجال Opportunities؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public enum DuplicateCandidateStatus
{
    Open = 0,
    Merged = 1,
    Dismissed = 2,
}

// A near-match found during extraction; administrators merge or dismiss it.
