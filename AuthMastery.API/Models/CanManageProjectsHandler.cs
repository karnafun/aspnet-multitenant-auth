using AuthMastery.API.Data;
using AuthMastery.API.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthMastery.API.Models
{
    public class CanManageProjectsHandler : AuthorizationHandler<CanManageProjectsRequirement>
    {
        private readonly ApplicationDbContext _context;

        public CanManageProjectsHandler(ApplicationDbContext context)
        {
            _context = context;
        }
        protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,        
        CanManageProjectsRequirement requirement)
        {
            var httpContext = context.Resource as HttpContext;
            if (httpContext == null)
            {
                context.Fail();
                return;
            }

            var projectId = httpContext.GetRouteValue("projectId")?.ToString();
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id.ToString() == projectId);
            if (project == null)
            {
                context.Fail(); 
                return;
            }
            if (context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
                return;
            }

            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == project.CreatedById)
                context.Succeed(requirement);
            else
                context.Fail();
  
        }
    }
}
