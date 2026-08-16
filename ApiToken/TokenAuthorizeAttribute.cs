using lotus_blue.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace lotus_blue.ApiToken
{
    public class TokenAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var httpContext = context.HttpContext;
            var configuration = httpContext.RequestServices.GetService<IConfiguration>();

            var acceptedTokens = configuration.GetSection("AcceptedTokens").Get<List<string>>();

            // Extract the token from the Authorization header
            var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
            var token = authHeader?.Split(" ").Last();
            // Add logging here

            if (string.IsNullOrEmpty(token) || !acceptedTokens.Contains(token))
            {
                context.Result = new UnauthorizedResult();
            }
        }
    }
}
