using TrovaBackend.DTOs.Projects;

namespace TrovaBackend.Services.Projects;

public interface IProjectService
{
    Task<PostProjectResponse> PostProjectAsync(Guid ownerId, PostProjectRequest request);
}