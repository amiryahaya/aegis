using Aegis.Domain.Entities;

namespace Aegis.Domain.Services;

public interface IJwtTokenGenerator
{
    string Generate(User user);
}
