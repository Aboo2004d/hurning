using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Models;

public sealed class AdminPage<T>
{
    public List<T> Items { get; init; } = [];
    public AdminPager Pager { get; init; } = new();
}
