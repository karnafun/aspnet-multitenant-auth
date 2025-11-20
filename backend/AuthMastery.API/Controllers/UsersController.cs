using AuthMastery.API.Extensions;
using AuthMastery.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthMastery.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : Controller
    {
        private readonly UserService _userService;
        private readonly ILogger<UsersController> _logger;
        public UsersController(UserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            _logger.LogInformation("GetAllUsers called by user: {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            var res = await _userService.GetAllUsersAsync();

            return Ok(res);
        }

        [Authorize]
        [HttpGet("{userEmail}")]
        public async Task<IActionResult> GetUserById(string userEmail)
        {
            _logger.LogInformation("GetAllUsers called by user: {UserId}, for email {UserEmail}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value,userEmail);

            var res = await _userService.GetUserByIdAsync(userEmail);

            return Ok(res);
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            var userId = User.GetUserId();
            var email = User.GetEmail();
            var tenantId = User.GetTenantId();
            var roles = User.Claims
              .Where(c => c.Type == ClaimTypes.Role)
              .Select(c => c.Value)
              .ToList();
            return Ok(new
            {
                userId,
                email,
                tenantId,
                roles
            });
        }
    }
}
