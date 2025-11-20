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

        CreateMap<ApplicationUser, UserAdminDto>();
        CreateMap<ApplicationUser, UserAdminDetailsDto>();
       

        CreateMap<Project, ProjectListDto>();
        CreateMap<Project, ProjectDetailDto>()
        .ForMember(dest => dest.Tags,
            opt => opt.MapFrom(src => src.ProjectTags.Select(pt => pt.Tag)))
        .ForMember(dest => dest.Watchers,
            opt => opt.MapFrom(src => src.ProjectWatchers.Select(w => w.User)));      
        
        CreateMap<Tag, TagDto>();

        // ProjectWatcher → ProjectListDto
        CreateMap<ProjectWatcher, ProjectListDto>()
            .ForMember(dest => dest.Id,
                opt => opt.MapFrom(src => src.Project.Id))
            .ForMember(dest => dest.Title,
                opt => opt.MapFrom(src => src.Project.Title))
            .ForMember(dest => dest.Status,
                opt => opt.MapFrom(src => src.Project.Status))
            .ForMember(dest => dest.CreatedByName,
                opt => opt.MapFrom(src => src.Project.CreatedBy.Email))
            .ForMember(dest => dest.WatcherCount,
                opt => opt.MapFrom(src => src.Project.ProjectWatchers.Count))
            .ForMember(dest => dest.AssignedTo,
                opt => opt.MapFrom(src => src.Project.AssignedTo.Email));

    }
}