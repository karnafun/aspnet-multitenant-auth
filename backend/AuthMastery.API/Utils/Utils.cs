using AuthMastery.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

public static class Utils
{

    public static string GetDisplayName(ProjectStatus status)
    {
        var fieldInfo = status.GetType().GetField(status.ToString());
        var displayAttribute = fieldInfo?.GetCustomAttribute<DisplayAttribute>();
        return displayAttribute?.Name ?? status.ToString();
    }

}
