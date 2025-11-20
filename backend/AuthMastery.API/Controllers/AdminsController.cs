using AuthMastery.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthMastery.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminsController : Controller
    {
        private readonly UserService _userService;
        private readonly ILogger<AdminsController> _logger;
        public AdminsController(UserService userService, ILogger<AdminsController> logger)
        {
            _userService = userService;
            _logger = logger;
        }
        [Authorize(Roles = "Admin")]
        [HttpGet("users")]
        public async Task<IActionResult> AdminGetAllUsers()
        {
            _logger.LogInformation("GetAllUsers called by user: {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            var res = await _userService.AdminGetAllUsersAsync();

            return Ok(res);
        }
        [Authorize(Roles = "Admin")]
        [HttpGet("users/{userEmail}")]
        public async Task<IActionResult> AdminGetUserByEmail(string userEmail)
        {
            _logger.LogInformation("AdminGetUserByEmail called by user: {UserId}, for email {UserEmail}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value, userEmail);

            var res = await _userService.AdminGetUserByEmailAsync(userEmail);

            return Ok(res);
        }
    }
}
