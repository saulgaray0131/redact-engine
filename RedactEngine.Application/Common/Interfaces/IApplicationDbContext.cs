using Microsoft.EntityFrameworkCore;
using RedactEngine.Application.Common;
using RedactEngine.Domain.Entities;

namespace RedactEngine.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
