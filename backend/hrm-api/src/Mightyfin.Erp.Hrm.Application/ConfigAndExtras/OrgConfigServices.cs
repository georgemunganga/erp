using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.ConfigAndExtras;

/// <summary>M1: full CRUD surface for organization configuration (legal entities,
/// locations, org units with effective-dated tree, work calendars, holidays,
/// leave types) plus capability (feature-flag) toggles.</summary>
public interface IConfigAdminService
{
    // Legal entities
    Task<Paged<LegalEntityDtoFull>> ListLegalEntitiesAsync(CancellationToken ct);
    Task<LegalEntityDtoFull> GetLegalEntityAsync(Guid id, CancellationToken ct);
    Task<LegalEntityDtoFull> CreateLegalEntityAsync(LegalEntityCreateRequest request, CancellationToken ct);
    Task<LegalEntityDtoFull> UpdateLegalEntityAsync(Guid id, LegalEntityUpdateRequest request, CancellationToken ct);

    // Work locations
    Task<Paged<WorkLocationDtoFull>> ListLocationsAsync(CancellationToken ct);
    Task<WorkLocationDtoFull> CreateLocationAsync(WorkLocationCreateRequest request, CancellationToken ct);
    Task<WorkLocationDtoFull> UpdateLocationAsync(Guid id, WorkLocationUpdateRequest request, CancellationToken ct);

    // Org units (effective-dated tree)
    Task<List<OrgUnitDtoFull>> ListOrgUnitsAsync(CancellationToken ct);
    Task<List<OrgUnitTreeDto>> GetOrgUnitTreeAsync(CancellationToken ct);
    Task<List<OrgUnitTreeDto>> GetEntityTreeAsync(CancellationToken ct);
    Task<OrgUnitDtoFull> CreateOrgUnitAsync(OrgUnitCreateRequest request, CancellationToken ct);
    Task<OrgUnitDtoFull> UpdateOrgUnitAsync(Guid id, OrgUnitUpdateRequest request, CancellationToken ct);
    Task CloseOrgUnitAsync(Guid id, OrgUnitCloseRequest request, CancellationToken ct);

    // Work calendars + public holidays
    Task<Paged<WorkCalendarDtoFull>> ListCalendarsAsync(CancellationToken ct);
    Task<WorkCalendarDtoFull> GetCalendarAsync(Guid id, CancellationToken ct);
    Task<WorkCalendarDtoFull> CreateCalendarAsync(WorkCalendarCreateRequest request, CancellationToken ct);
    Task<WorkCalendarDtoFull> UpdateCalendarAsync(Guid id, WorkCalendarUpdateRequest request, CancellationToken ct);
    Task<PublicHolidayDto> AddHolidayAsync(PublicHolidayCreateRequest request, CancellationToken ct);
    Task<PublicHolidayDto> UpdateHolidayAsync(Guid id, PublicHolidayUpdateRequest request, CancellationToken ct);
    Task DeleteHolidayAsync(Guid id, CancellationToken ct);

    // Leave types
    Task<Paged<LeaveTypeDtoFull>> ListLeaveTypesAsync(bool includeInactive, CancellationToken ct);
    Task<LeaveTypeDtoFull> CreateLeaveTypeAsync(LeaveTypeCreateRequest request, CancellationToken ct);
    Task<LeaveTypeDtoFull> UpdateLeaveTypeAsync(Guid id, LeaveTypeUpdateRequest request, CancellationToken ct);

    // Contract types
    Task<Paged<ContractTypeDto>> ListContractTypesAsync(bool includeInactive, CancellationToken ct);
    Task<ContractTypeDto> CreateContractTypeAsync(ContractTypeCreateRequest request, CancellationToken ct);
    Task<ContractTypeDto> UpdateContractTypeAsync(Guid id, ContractTypeUpdateRequest request, CancellationToken ct);

    // Capabilities (feature flags)
    Task<List<CapabilityConfig>> ListCapabilitiesAsync(CancellationToken ct);
    Task<CapabilityConfig> UpdateCapabilityAsync(string featureKey, CapabilityUpdateRequest request, CancellationToken ct);
}
