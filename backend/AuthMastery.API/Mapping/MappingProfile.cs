using AuthMastery.API.DTO.Project;
using AuthMastery.API.Models;
using AutoMapper;

namespace AuthMastery.API.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {   
        CreateMap<ApplicationUser, UserDto>()
        .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.UserName))
        .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email));

        CreateMap<Project, ProjectListDto>();

        CreateMap<Project, ProjectDetailDto>()
        .ForMember(dest => dest.Tags,
            opt => opt.MapFrom(src => src.ProjectTags.Select(pt => pt.Tag)))
        .ForMember(dest => dest.Watchers,
            opt => opt.MapFrom(src => src.ProjectWatchers.Select(w => w.User)));      
        
        CreateMap<Tag, TagDto>();
    }
}