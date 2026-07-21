using TrovaBackend.DTOs.Projects;

namespace TrovaBackend.Services.Projects;

public interface IProjectService
{
    Task<PostProjectResponse> PostProjectAsync(Guid ownerId, PostProjectRequest request);

    Task<List<ProjectListItemDto>> GetMyProjectsAsync(Guid ownerId);
    Task<List<ProjectHistoryItemDto>> GetMyProjectHistoryAsync(Guid ownerId);
    Task<ProjectDetailDto?> GetProjectDetailAsync(Guid ownerId, string projectId);

    Task<List<BidderDto>?> GetProjectBiddersAsync(Guid ownerId, string projectId);
    Task<AwardProjectResponse?> AwardProjectAsync(Guid ownerId, string projectId, AwardProjectRequest request);

    Task<List<BrowseProjectListItemDto>> BrowseProjectsAsync(List<string>? sectors, decimal? minValue, decimal? maxValue);
    Task<BrowseProjectDetailDto?> GetBrowseProjectDetailAsync(Guid contractorId, string projectId);
    Task<SubmitBidResponse> SubmitBidAsync(Guid contractorId, string projectId, SubmitBidRequest request);
}