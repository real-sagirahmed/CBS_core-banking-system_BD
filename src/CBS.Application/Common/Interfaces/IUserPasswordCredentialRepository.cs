using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Security;

namespace CBS.Application.Common.Interfaces;

public interface IUserPasswordCredentialRepository
{
    Task<UserPasswordCredential?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task AddAsync(UserPasswordCredential credential, CancellationToken ct = default);
    Task UpdateAsync(UserPasswordCredential credential, CancellationToken ct = default);
}
