using AuthMastery.API.Data;
using AuthMastery.API.DTO;
using AuthMastery.API.DTO.Project;
using AuthMastery.API.Models;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AuthMastery.API.Services
{
    public class TagService
    {
        private readonly ITenantProvider _tenantProvider;
        private readonly ILogger<TagService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        public TagService(ITenantProvider tenantProvider, ILogger<TagService> logger, ApplicationDbContext context, IMapper mapper)
        {
            _tenantProvider = tenantProvider;
            _logger = logger;
            _context = context;
            _mapper = mapper;
        }

        public async Task<List<TagDto>> GetAllTags()
        {
            var tenantId = _tenantProvider.GetTenantId();
            try
            {
                _logger.LogInformation("Fetching all tags for TenantId: {TenantId}", tenantId);

                var tags = await _context.Tags
                    .AsNoTracking() 
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} tags ", tags.Count);

                return _mapper.Map<List<TagDto>>(tags);
            }
            catch (Exception ex) when (ex is not (NotFoundException or BadRequestException))
            {
                throw new TagOperationException(tenantId:tenantId, inner: ex);
            }
        }

        public async Task<TagDto> GetTagBySlugAsync(string tagSlug)
        {
            var tenantId = _tenantProvider.GetTenantId();
            try
            {
                _logger.LogInformation("GetTagBySlugAsync for tag {TagSlug}, Tenant {TenantId}", tagSlug,tenantId);

                var tag = await _context.Tags
                    .AsNoTracking() 
                    .FirstOrDefaultAsync(t=>t.Slug==tagSlug);

                if (tag == null)
                {
                    _logger.LogWarning("Tag {TagSlug} not found", tagSlug);
                    throw new NotFoundException($"Tag {tagSlug} not found");
                }
                
                return _mapper.Map<TagDto>(tag);
            }
            catch (Exception ex) when (ex is not (NotFoundException or BadRequestException))
            {
                var context = new
                {
                    tagSlug
                };
                throw new TagOperationException(tenantId: tenantId, context: context, inner: ex);
            }
        }

        public async Task AddTag(string tagName) {
            var tenantId = _tenantProvider.GetTenantId();

            try
            {
                _logger.LogInformation("AddTag {TagName}, Tenant {TenantId}", tagName, tenantId);

                var tagSlug = tagName.ToLower().Replace(' ', '-');
                var tag = await _context.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == tagSlug);
                if (tag != null)
                {
                    _logger.LogWarning("Tag already exists {tag}", JsonSerializer.Serialize(tag));
                    var _dto = _mapper.Map<TagDto>(tag);
                    throw new ConflictException($"Tag already exists {JsonSerializer.Serialize(_dto)}");
                }

                var newTag = new Tag()
                {
                    Slug = tagSlug,
                    Name = tagName,
                    TenantId = tenantId
                };
                _context.Tags.Add(newTag);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) when (ex is not (NotFoundException or BadRequestException or ConflictException))
            {
                var context = new
                {
                    tagName
                };
                throw new TagOperationException(tenantId: tenantId, context: context, inner: ex);
            }

        }
        public async Task DeleteTagBySlugAsync(string tagSlug)
        {
            var tenantId = _tenantProvider.GetTenantId();
            try
            {
                _logger.LogInformation("DeleteTagBySlugAsync for tag {TagSlug}, Tenant {TenantId}", tagSlug, tenantId);

                var tag = await _context.Tags                    
                    .FirstOrDefaultAsync(t => t.Slug == tagSlug);

                if (tag == null)
                {
                    _logger.LogWarning("Tag {TagSlug} not found", tagSlug);
                    return;
                }
                
                var projectTags = await _context.ProjectTags
                    .Where(pt => pt.TagId == tag.Id)
                    .ToListAsync();

                if (projectTags.Any())
                {
                    _context.ProjectTags.RemoveRange(projectTags);
                    _logger.LogInformation("Removing tag {TagSlug} from {Count} projects",
                        tagSlug, projectTags.Count);
                }

                _context.Tags.Remove(tag);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted tag {TagSlug}", tagSlug);

            }
            catch (Exception ex) when (ex is not (NotFoundException or BadRequestException))
            {
                var context = new{
                    tagSlug
                };
                throw new TagOperationException(tenantId: tenantId, context: context, inner: ex);
            }
        }
    }
}
