namespace AuthMastery.API.DTO
{
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message)
        {
        }
    }

    public class UnauthorizedException : Exception
    {
        public UnauthorizedException(string message) : base(message)
        {
        }
    }
    public class BadRequestException : Exception {

        public BadRequestException(string message) : base(message)
        {
        }
    }
    public class ConflictException : Exception
    {

        public ConflictException(string message) : base(message)
        {
        }
    }

    public abstract class DomainOperationException : Exception
    {
        public int TenantId { get; }
        public object? EntityId { get; }
        public object? Context { get; }

        protected DomainOperationException(int tenantId, object? entityId = null, object? context = null, Exception? inner = null)
            : base(BuildMessage(tenantId, entityId, context), inner)
        {
            TenantId = tenantId;
            EntityId = entityId;
            Context = context;
        }

        private static string BuildMessage(int tenantId, object? entityId, object? context)
        {
            var ctxJson = context != null ? System.Text.Json.JsonSerializer.Serialize(context) : "";
            return $"Tenant={tenantId} EntityId={entityId?.ToString() ?? "unknown"} Context={ctxJson}";
        }
    }
    public class ProjectOperationException : DomainOperationException
    {
        public ProjectOperationException(int tenantId, Guid? projectId = null, object? context = null, Exception? inner = null)
            : base(tenantId, projectId, context, inner) { }
    }
    public class TagOperationException : DomainOperationException
    {
        public TagOperationException(int tenantId, int? tagId = null, object? context = null, Exception? inner = null)
            : base(tenantId, tagId, context, inner) { }
    }

    public class UserOperationException : DomainOperationException
    {
        public UserOperationException(int tenantId, string? userId = null, object? context = null, Exception? inner = null)
            : base(tenantId, userId, context, inner) { }
    }


}
