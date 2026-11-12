using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Security;

namespace CBS.Application.Common.Interfaces;

public interface IUserDoaProfileRepository
{
    Task<UserDoaProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task AddAsync(UserDoaProfile profile, CancellationToken ct = default);
    Task UpdateAsync(UserDoaProfile profile, CancellationToken ct = default);
}
