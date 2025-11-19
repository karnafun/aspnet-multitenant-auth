using AuthMastery.API.Data;
using AuthMastery.API.DTO;
using AuthMastery.API.DTO.Project;
using AuthMastery.API.Models;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthMastery.API.Services
{
    public class UserService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UserService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ITenantProvider _tenantProvider;
        private readonly IMapper _mapper;
        public UserService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<UserService> logger,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ITenantProvider tenantProvider, IMapper mapper)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _tenantProvider = tenantProvider;
            _mapper = mapper;
        }

        public async Task<List<UserDto>> GetAllUsersAsync()
        {

            var tenantId = _tenantProvider.GetTenantId();
            _logger.LogInformation("Fetching all users for TenantId: {TenantId}", tenantId);

            try
            {
                var users = await _context.Users
                    .AsNoTracking()
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} users", users.Count);

                return _mapper.Map<List<UserDto>>(users);
            }
            catch (Exception ex) when (ex is not (NotFoundException or BadRequestException))
            {
                throw new UserOperationException(tenantId: tenantId, inner: ex);
            }
        }

        public async Task<UserDetailsDto> GetUserByIdAsync(string userEmail)
        {
            var tenantId = _tenantProvider.GetTenantId();
            _logger.LogInformation("GetUserById Email:{UserEmail}, TenantId: {TenantId}", userEmail, tenantId);

            try
            {

                var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == userEmail);
                if (user == null)
                {
                    _logger.LogWarning($"User not found: {userEmail}");
                    throw new NotFoundException($"User {userEmail} not found"); 
                }

                var userDto = _mapper.Map<UserDetailsDto>(user);
                return userDto;
            }
            catch (Exception ex) when (ex is not (NotFoundException or BadRequestException))
            {
                var context = new { userEmail };
                throw new UserOperationException(tenantId: tenantId, context: context, inner: ex);
            }
        }
    }
}
