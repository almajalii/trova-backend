using TrovaBackend.DTOs.Bids;
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

    // GET /api/projects/{projectId}/owner-profile. Project-scoped, not
    // bid-scoped — for the browse/Submit Bid screen, before a bid exists.
    // Visibility matches BrowseProjectsAsync/GetBrowseProjectDetailAsync:
    // any authenticated contractor can view it as long as the project is
    // still OpenForBids. Null (-> 404) once the project closes or if it
    // never existed — same "closed/nonexistent looks the same" pattern
    // used for browse detail.
    Task<OwnerProfileDto?> GetOwnerProfileByProjectAsync(string projectId);
}