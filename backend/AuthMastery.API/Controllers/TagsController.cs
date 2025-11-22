using AuthMastery.API.Extensions;
using AuthMastery.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;


namespace AuthMastery.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TagsController : ControllerBase
    {
        private readonly TagService _tagService;
        private readonly ILogger<TagsController> _logger;

        public TagsController(TagService tagService, ILogger<TagsController> logger)
        {
            _tagService = tagService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAllTags() {
            _logger.LogInformation("GetAllTags called by user: {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            var tags = await _tagService.GetAllTags();

            return Ok(tags);
        }

        [Authorize]
        [HttpGet("{tagSlug}")]
        public async Task<IActionResult> GetTagBySlug(string tagSlug)
        {
            tagSlug.ValidateTagSlug(nameof(tagSlug));
            
            _logger.LogInformation("GetTagBySlug for tag {TagSlug} called by user: {UserId}",tagSlug, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            var tag = await _tagService.GetTagBySlugAsync(tagSlug);

            return Ok(tag);
        }

        [Authorize]
        [HttpDelete("{tagSlug}")]
        public async Task<IActionResult> DeleteTagBySlug(string tagSlug)
        {
            tagSlug.ValidateTagSlug(nameof(tagSlug));
            
            _logger.LogInformation("DeleteTagBySlug for tag {TagSlug} called by user: {UserId}", tagSlug, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            await _tagService.DeleteTagBySlugAsync(tagSlug);

            return NoContent();

        }
        [Authorize]
        [HttpPost("{tagName}")]
        public async Task<IActionResult> AddTag(string tagName)
        {
            tagName.ValidateTagName(nameof(tagName));
            
            _logger.LogInformation("AddTag  {TagName} called by user: {UserId}", tagName, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            await _tagService.AddTag(tagName);

            return NoContent();
        }
    }
}
