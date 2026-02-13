using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Optix.Web.API.Authorization
{
    public sealed class AllowedClientHandler : AuthorizationHandler<AllowedClientRequirement>
    {
        private readonly AuthOptions _authOptions;

        public AllowedClientHandler(IOptions<AuthOptions> options)
        {
            _authOptions = options.Value;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            AllowedClientRequirement requirement)
        {
            var azp = context.User.FindFirst("azp")?.Value;

            if (azp is not null &&
                _authOptions.ClientIds.Contains(azp))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
