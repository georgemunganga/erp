using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.ConfigAndExtras;

/// <summary>M1 CRUD implementation for organization configuration.</summary>
public sealed class ConfigAdminServiceImpl(IConfigRepository repo, IAuthzService authz) : IConfigAdminService
{
    // ================= Legal entities =================

    public async Task<Paged<LegalEntityDtoFull>> ListLegalEntitiesAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var items = await repo.ListLegalEntitiesAsync(ct);
        return new Paged<LegalEntityDtoFull>(items.Select(ToLegalEntityDto).ToList(), items.Count, 1, 100);
    }

    public async Task<LegalEntityDtoFull> GetLegalEntityAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var e = await repo.GetLegalEntityAsync(id, ct)
            ?? throw new DomainException("legal-entity-not-found", $"Legal entity {id} does not exist.");
        return ToLegalEntityDto(e);
    }

    public async Task<LegalEntityDtoFull> CreateLegalEntityAsync(LegalEntityCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        RequireNonEmpty(request.Code, "code");
        RequireNonEmpty(request.RegisteredName, "registeredName");
        var existing = (await repo.ListLegalEntitiesAsync(ct)).FirstOrDefault(e => e.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            throw new DomainException("legal-entity-code-taken", $"Code '{request.Code}' is already in use.");
        var entity = new LegalEntity
        {
            Code = request.Code.Trim(), RegisteredName = request.RegisteredName.Trim(), TradingName = request.TradingName?.Trim(),
            PacraNumber = request.PacraNumber?.Trim(), Tpin = request.Tpin?.Trim(),
            NapsaEmployerRef = request.NapsaEmployerRef?.Trim(), NhimaEmployerRef = request.NhimaEmployerRef?.Trim(),
            WcfcbEmployerRef = request.WcfcbEmployerRef?.Trim(), Currency = request.Currency.ToUpperInvariant(),
            CountryCode = request.CountryCode.ToUpperInvariant(), IsDefault = request.IsDefault,
        };
        return ToLegalEntityDto(await repo.CreateLegalEntityAsync(entity, ct));
    }

    public async Task<LegalEntityDtoFull> UpdateLegalEntityAsync(Guid id, LegalEntityUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var entity = await repo.GetLegalEntityAsync(id, ct)
            ?? throw new DomainException("legal-entity-not-found", $"Legal entity {id} does not exist.");
        if (request.RegisteredName is not null) entity.RegisteredName = request.RegisteredName.Trim();
        if (request.TradingName is not null) entity.TradingName = request.TradingName.Trim();
        if (request.PacraNumber is not null) entity.PacraNumber = request.PacraNumber.Trim();
        if (request.Tpin is not null) entity.Tpin = request.Tpin.Trim();
        if (request.NapsaEmployerRef is not null) entity.NapsaEmployerRef = request.NapsaEmployerRef.Trim();
        if (request.NhimaEmployerRef is not null) entity.NhimaEmployerRef = request.NhimaEmployerRef.Trim();
        if (request.WcfcbEmployerRef is not null) entity.WcfcbEmployerRef = request.WcfcbEmployerRef.Trim();
        if (request.Currency is not null) entity.Currency = request.Currency.ToUpperInvariant();
        if (request.IsDefault.HasValue) entity.IsDefault = request.IsDefault.Value;
        return ToLegalEntityDto(await repo.UpdateLegalEntityAsync(entity, ct));
    }

    // ================= Work locations =================

    public async Task<Paged<WorkLocationDtoFull>> ListLocationsAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee", "manager", "payroll");
        var items = await repo.ListLocationsAsync(ct);
        var entities = await repo.ListLegalEntitiesAsync(ct);
        var calendars = await repo.ListCalendarsAsync(ct);
        return new Paged<WorkLocationDtoFull>(items.Select(l => ToLocationDto(l, entities, calendars)).ToList(), items.Count, 1, 100);
    }

    public async Task<WorkLocationDtoFull> CreateLocationAsync(WorkLocationCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        RequireNonEmpty(request.Code, "code");
        RequireNonEmpty(request.Name, "name");
        var existing = (await repo.ListLocationsAsync(ct)).FirstOrDefault(l => l.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            throw new DomainException("location-code-taken", $"Location code '{request.Code}' is already in use.");
        var entities = await repo.ListLegalEntitiesAsync(ct);
        if (entities.All(e => e.Id != request.LegalEntityId))
            throw new DomainException("legal-entity-not-found", $"Legal entity {request.LegalEntityId} does not exist.");
        if (request.DefaultCalendarId.HasValue)
        {
            var calendars = await repo.ListCalendarsAsync(ct);
            if (calendars.All(c => c.Id != request.DefaultCalendarId))
                throw new DomainException("calendar-not-found", $"Calendar {request.DefaultCalendarId} does not exist.");
        }
        var location = new WorkLocation
        {
            Code = request.Code.Trim(), Name = request.Name.Trim(), LegalEntityId = request.LegalEntityId,
            AddressLine = request.AddressLine?.Trim(), Province = request.Province?.Trim(),
            District = request.District?.Trim(), City = request.City?.Trim(),
            Type = request.Type, DefaultCalendarId = request.DefaultCalendarId,
        };
        return ToLocationDto(await repo.CreateLocationAsync(location, ct), entities, await repo.ListCalendarsAsync(ct));
    }

    public async Task<WorkLocationDtoFull> UpdateLocationAsync(Guid id, WorkLocationUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var location = await repo.GetLocationAsync(id, ct)
            ?? throw new DomainException("location-not-found", $"Work location {id} does not exist.");
        if (request.Name is not null) location.Name = request.Name.Trim();
        if (request.AddressLine is not null) location.AddressLine = request.AddressLine.Trim();
        if (request.Province is not null) location.Province = request.Province.Trim();
        if (request.District is not null) location.District = request.District.Trim();
        if (request.City is not null) location.City = request.City.Trim();
        if (request.Type is not null) location.Type = request.Type;
        if (request.DefaultCalendarId.HasValue)
        {
            var calendars = await repo.ListCalendarsAsync(ct);
            if (calendars.All(c => c.Id != request.DefaultCalendarId))
                throw new DomainException("calendar-not-found", $"Calendar {request.DefaultCalendarId} does not exist.");
            location.DefaultCalendarId = request.DefaultCalendarId;
        }
        return ToLocationDto(await repo.UpdateLocationAsync(location, ct), await repo.ListLegalEntitiesAsync(ct), await repo.ListCalendarsAsync(ct));
    }

    // ================= Org units =================

    public async Task<List<OrgUnitDtoFull>> ListOrgUnitsAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee", "manager", "payroll");
        var units = await repo.ListOrgUnitsAsync(ct);
        var entities = await repo.ListLegalEntitiesAsync(ct);
        var workers = await repo.ListAllWorkersAsync(null, ct);
        return units.Select(u => ToOrgUnitDto(u, entities, workers)).ToList();
    }

    public async Task<List<OrgUnitTreeDto>> GetOrgUnitTreeAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee", "manager", "payroll");
        var units = await repo.ListOrgUnitsAsync(ct);
        var workers = await repo.ListAllWorkersAsync(null, ct);
        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var effective = units.Where(u =>
            u.EffectiveFrom <= now && (u.EffectiveTo is null || u.EffectiveTo > now) && u.Status != "closed").ToList();

        OrgUnitTreeDto ToTree(OrgUnit u) => new(
            u.Id, u.Code, u.Name, u.UnitType, u.Status, u.ManagerId,
            u.ManagerId.HasValue ? workers.FirstOrDefault(w => w.Id == u.ManagerId)?.FullName : null,
            u.EffectiveFrom.ToString("yyyy-MM-dd"), u.EffectiveTo?.ToString("yyyy-MM-dd"), []);

        var nodes = effective.Select(ToTree).ToDictionary(n => n.Id);
        var roots = new List<OrgUnitTreeDto>();
        foreach (var u in effective)
        {
            var node = nodes[u.Id];
            if (u.ParentId is null || !nodes.ContainsKey(u.ParentId.Value))
                roots.Add(node);
            else
                nodes[u.ParentId.Value].Children.Add(node);
        }
        return roots;
    }

    /// <summary>Legal entities with their org units nested beneath (entity → branch → department → team).
    /// Units whose legal entity is missing or unknown fall under an "Unassigned" root so nothing disappears.</summary>
    public async Task<List<OrgUnitTreeDto>> GetEntityTreeAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee", "manager", "payroll");
        var units = await repo.ListOrgUnitsAsync(ct);
        var workers = await repo.ListAllWorkersAsync(null, ct);
        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var effective = units.Where(u =>
            u.EffectiveFrom <= now && (u.EffectiveTo is null || u.EffectiveTo > now) && u.Status != "closed").ToList();

        OrgUnitTreeDto ToTree(OrgUnit u) => new(
            u.Id, u.Code, u.Name, u.UnitType, u.Status, u.ManagerId,
            u.ManagerId.HasValue ? workers.FirstOrDefault(w => w.Id == u.ManagerId)?.FullName : null,
            u.EffectiveFrom.ToString("yyyy-MM-dd"), u.EffectiveTo?.ToString("yyyy-MM-dd"), []);

        var nodes = effective.Select(ToTree).ToDictionary(n => n.Id);
        var entitiesByName = (await repo.ListLegalEntitiesAsync(ct))
            .ToDictionary(e => e.Id, e => e.RegisteredName);
        var entityRoots = new Dictionary<Guid, OrgUnitTreeDto>();
        OrgUnitTreeDto unassignedRoot = new(Guid.Empty, "UNASSIGNED", "Unassigned", "entity", "active", null, null,
            now.ToString("yyyy-MM-dd"), null, []);
        foreach (var u in effective)
        {
            var node = nodes[u.Id];
            OrgUnitTreeDto parent;
            if (u.LegalEntityId is Guid le && entityRoots.TryGetValue(le, out var existing))
                parent = existing;
            else if (u.LegalEntityId is Guid le2)
                parent = entityRoots[le2] = new OrgUnitTreeDto(le2, "", entitiesByName.GetValueOrDefault(le2) ?? "Unknown entity", "entity", "active", null, null,
                    now.ToString("yyyy-MM-dd"), null, []);
            else
                parent = unassignedRoot;
            if (u.ParentId is null || !nodes.ContainsKey(u.ParentId.Value))
                parent.Children.Add(node);
            else
                nodes[u.ParentId.Value].Children.Add(node);
        }
        var roots = new List<OrgUnitTreeDto>();
        foreach (var kvp in entityRoots.OrderBy(k => k.Value.Name == "" ? 1 : 0).ThenBy(k => k.Value.Name))
            roots.Add(kvp.Value);
        if (unassignedRoot.Children.Count > 0) roots.Add(unassignedRoot);
        return roots;
    }

    public async Task<OrgUnitDtoFull> CreateOrgUnitAsync(OrgUnitCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        RequireNonEmpty(request.Code, "code");
        RequireNonEmpty(request.Name, "name");
        var effectiveFrom = string.IsNullOrWhiteSpace(request.EffectiveFrom) ? DateOnly.FromDateTime(DateTime.UtcNow) : DateOnly.Parse(request.EffectiveFrom);
        var existing = (await repo.ListOrgUnitsAsync(ct)).FirstOrDefault(u => u.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            throw new DomainException("org-unit-code-taken", $"Unit code '{request.Code}' is already in use.");
        var entities = await repo.ListLegalEntitiesAsync(ct);
        if (entities.All(e => e.Id != request.LegalEntityId))
            throw new DomainException("legal-entity-not-found", $"Legal entity {request.LegalEntityId} does not exist.");
        if (request.ParentId.HasValue && (await repo.ListOrgUnitsAsync(ct)).All(u => u.Id != request.ParentId))
            throw new DomainException("parent-unit-not-found", $"Parent unit {request.ParentId} does not exist.");
        if (request.ManagerId.HasValue && (await repo.ListAllWorkersAsync(null, ct)).All(w => w.Id != request.ManagerId))
            throw new DomainException("manager-not-found", $"Worker {request.ManagerId} does not exist.");
        var unit = new OrgUnit
        {
            Code = request.Code.Trim(), Name = request.Name.Trim(), LegalEntityId = request.LegalEntityId,
            ParentId = request.ParentId, UnitType = request.UnitType, CostCentreRef = request.CostCentreRef?.Trim(),
            ManagerId = request.ManagerId, EffectiveFrom = effectiveFrom,
            EffectiveTo = string.IsNullOrWhiteSpace(request.EffectiveTo) ? null : DateOnly.Parse(request.EffectiveTo),
        };
        return ToOrgUnitDto(await repo.CreateOrgUnitAsync(unit, ct), entities, await repo.ListAllWorkersAsync(null, ct));
    }

    public async Task<OrgUnitDtoFull> UpdateOrgUnitAsync(Guid id, OrgUnitUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var unit = await repo.GetOrgUnitAsync(id, ct)
            ?? throw new DomainException("org-unit-not-found", $"Org unit {id} does not exist.");
        if (request.Name is not null) unit.Name = request.Name.Trim();
        if (request.ParentId.HasValue)
        {
            if (await repo.ListOrgUnitsAsync(ct) is { } all && all.All(u => u.Id != request.ParentId))
                throw new DomainException("parent-unit-not-found", $"Parent unit {request.ParentId} does not exist.");
            unit.ParentId = request.ParentId;
        }
        if (request.UnitType is not null) unit.UnitType = request.UnitType;
        if (request.CostCentreRef is not null) unit.CostCentreRef = request.CostCentreRef.Trim();
        if (request.ManagerId.HasValue && (await repo.ListAllWorkersAsync(null, ct)).All(w => w.Id != request.ManagerId))
            throw new DomainException("manager-not-found", $"Worker {request.ManagerId} does not exist.");
        if (request.ManagerId.HasValue) unit.ManagerId = request.ManagerId;
        if (request.EffectiveTo is not null) unit.EffectiveTo = string.IsNullOrWhiteSpace(request.EffectiveTo) ? null : DateOnly.Parse(request.EffectiveTo);
        if (request.Status is not null) unit.Status = request.Status;
        return ToOrgUnitDto(await repo.UpdateOrgUnitAsync(unit, ct), await repo.ListLegalEntitiesAsync(ct), await repo.ListAllWorkersAsync(null, ct));
    }

    /// <summary>Structural changes are future-effective: units are closed with an
    /// effective-to date rather than hard-deleted, preserving history.</summary>
    public async Task CloseOrgUnitAsync(Guid id, OrgUnitCloseRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var unit = await repo.GetOrgUnitAsync(id, ct)
            ?? throw new DomainException("org-unit-not-found", $"Org unit {id} does not exist.");
        var effectiveDate = DateOnly.Parse(request.EffectiveDate);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (effectiveDate < today)
            throw new DomainException("unit-close-backdated", "Org unit changes cannot be backdated; pick a date today or later.");
        unit.EffectiveTo = effectiveDate;
        unit.Status = "closed";
        await repo.UpdateOrgUnitAsync(unit, ct);
    }

    // ================= Work calendars & holidays =================

    public async Task<Paged<WorkCalendarDtoFull>> ListCalendarsAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee", "manager", "payroll");
        var items = await repo.ListCalendarsAsync(ct);
        var entities = await repo.ListLegalEntitiesAsync(ct);
        return new Paged<WorkCalendarDtoFull>(items.Select(c => ToCalendarDto(c, entities)).ToList(), items.Count, 1, 100);
    }

    public async Task<WorkCalendarDtoFull> GetCalendarAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee", "manager", "payroll");
        var items = await repo.ListCalendarsAsync(ct);
        var calendar = items.FirstOrDefault(c => c.Id == id)
            ?? throw new DomainException("calendar-not-found", $"Work calendar {id} does not exist.");
        return ToCalendarDto(calendar, await repo.ListLegalEntitiesAsync(ct));
    }

    public async Task<WorkCalendarDtoFull> CreateCalendarAsync(WorkCalendarCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        RequireNonEmpty(request.Name, "name");
        var entities = await repo.ListLegalEntitiesAsync(ct);
        if (entities.All(e => e.Id != request.LegalEntityId))
            throw new DomainException("legal-entity-not-found", $"Legal entity {request.LegalEntityId} does not exist.");
        var calendar = new WorkCalendar
        {
            Name = request.Name.Trim(), LegalEntityId = request.LegalEntityId, CountryCode = request.CountryCode.ToUpperInvariant(),
            StandardWeeklyHours = Math.Clamp(request.StandardWeeklyHours, 1, 168),
            WeekendDays = NormalizeWeekendDays(request.WeekendDays), IsDefault = request.IsDefault,
        };
        return ToCalendarDto(await repo.CreateCalendarAsync(calendar, ct), entities);
    }

    public async Task<WorkCalendarDtoFull> UpdateCalendarAsync(Guid id, WorkCalendarUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var items = await repo.ListCalendarsAsync(ct);
        var calendar = items.FirstOrDefault(c => c.Id == id)
            ?? throw new DomainException("calendar-not-found", $"Work calendar {id} does not exist.");
        if (request.Name is not null) calendar.Name = request.Name.Trim();
        if (request.StandardWeeklyHours.HasValue)
            calendar.StandardWeeklyHours = Math.Clamp(request.StandardWeeklyHours.Value, 1, 168);
        if (request.WeekendDays is not null) calendar.WeekendDays = NormalizeWeekendDays(request.WeekendDays);
        if (request.IsDefault.HasValue) calendar.IsDefault = request.IsDefault.Value;
        await repo.UpdateCalendarAsync(calendar, ct);
        return ToCalendarDto(calendar, await repo.ListLegalEntitiesAsync(ct));
    }

    public async Task<PublicHolidayDto> AddHolidayAsync(PublicHolidayCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        RequireNonEmpty(request.Name, "name");
        var calendars = await repo.ListCalendarsAsync(ct);
        var calendar = calendars.FirstOrDefault(c => c.Id == request.CalendarId)
            ?? throw new DomainException("calendar-not-found", $"Work calendar {request.CalendarId} does not exist.");
        var holiday = new PublicHoliday
        {
            CalendarId = request.CalendarId, Name = request.Name.Trim(), HolidayDate = DateOnly.Parse(request.HolidayDate),
            ObservedOn = request.ObservedOn?.Trim(), IsRecurring = request.IsRecurring, Description = request.Description?.Trim(),
        };
        await repo.CreateHolidayAsync(holiday, ct);
        return ToHolidayDto(holiday);
    }

    public async Task<PublicHolidayDto> UpdateHolidayAsync(Guid id, PublicHolidayUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var holiday = await repo.GetHolidayAsync(id, ct)
            ?? throw new DomainException("holiday-not-found", $"Holiday {id} does not exist.");
        if (request.Name is not null) holiday.Name = request.Name.Trim();
        if (request.HolidayDate is not null) holiday.HolidayDate = DateOnly.Parse(request.HolidayDate);
        if (request.ObservedOn is not null) holiday.ObservedOn = request.ObservedOn.Trim();
        if (request.IsRecurring.HasValue) holiday.IsRecurring = request.IsRecurring.Value;
        if (request.Description is not null) holiday.Description = request.Description.Trim();
        await repo.UpdateHolidayAsync(holiday, ct);
        return ToHolidayDto(holiday);
    }

    public async Task DeleteHolidayAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        await repo.DeleteHolidayAsync(id, ct);
    }

    // ================= Leave types =================

    public async Task<Paged<LeaveTypeDtoFull>> ListLeaveTypesAsync(bool includeInactive, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee", "manager", "payroll");
        var items = await repo.ListLeaveTypesAsync(includeInactive, ct);
        return new Paged<LeaveTypeDtoFull>(items.Select(ToLeaveTypeDto).ToList(), items.Count, 1, 100);
    }

    public async Task<LeaveTypeDtoFull> CreateLeaveTypeAsync(LeaveTypeCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        RequireNonEmpty(request.Code, "code");
        RequireNonEmpty(request.Name, "name");
        var existing = (await repo.ListLeaveTypesAsync(true, ct)).FirstOrDefault(t => t.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            throw new DomainException("leave-type-code-taken", $"Leave type code '{request.Code}' is already in use.");
        var effectiveFrom = string.IsNullOrWhiteSpace(request.EffectiveFrom) ? DateOnly.FromDateTime(DateTime.UtcNow) : DateOnly.Parse(request.EffectiveFrom);
        var leaveType = new LeaveType
        {
            Code = request.Code.Trim().ToLowerInvariant(), Name = request.Name.Trim(), Category = request.Category,
            DefaultDaysPerYear = Math.Max(0, request.DefaultDaysPerYear), MaxConsecutiveDays = request.MaxConsecutiveDays,
            RequiresEvidence = request.RequiresEvidence, MinNoticeDays = Math.Max(0, request.MinNoticeDays),
            AllowsPartialDays = request.AllowsPartialDays, CarryForwardDays = Math.Max(0, request.CarryForwardDays),
            CarryForwardExpiryMonths = Math.Max(0, request.CarryForwardExpiryMonths), AllowNegative = request.AllowNegative,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = string.IsNullOrWhiteSpace(request.EffectiveTo) ? null : DateOnly.Parse(request.EffectiveTo),
        };
        return ToLeaveTypeDto(await repo.CreateLeaveTypeAsync(leaveType, ct));
    }

    public async Task<LeaveTypeDtoFull> UpdateLeaveTypeAsync(Guid id, LeaveTypeUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var leaveType = await repo.GetLeaveTypeAsync(id, ct)
            ?? throw new DomainException("leave-type-not-found", $"Leave type {id} does not exist.");
        if (request.Name is not null) leaveType.Name = request.Name.Trim();
        if (request.Category is not null) leaveType.Category = request.Category;
        if (request.DefaultDaysPerYear.HasValue) leaveType.DefaultDaysPerYear = Math.Max(0, request.DefaultDaysPerYear.Value);
        if (request.MaxConsecutiveDays.HasValue) leaveType.MaxConsecutiveDays = request.MaxConsecutiveDays.Value;
        if (request.RequiresEvidence.HasValue) leaveType.RequiresEvidence = request.RequiresEvidence.Value;
        if (request.MinNoticeDays.HasValue) leaveType.MinNoticeDays = Math.Max(0, request.MinNoticeDays.Value);
        if (request.AllowsPartialDays.HasValue) leaveType.AllowsPartialDays = request.AllowsPartialDays.Value;
        if (request.CarryForwardDays.HasValue) leaveType.CarryForwardDays = Math.Max(0, request.CarryForwardDays.Value);
        if (request.CarryForwardExpiryMonths.HasValue) leaveType.CarryForwardExpiryMonths = Math.Max(0, request.CarryForwardExpiryMonths.Value);
        if (request.AllowNegative.HasValue) leaveType.AllowNegative = request.AllowNegative.Value;
        if (request.EffectiveTo is not null) leaveType.EffectiveTo = string.IsNullOrWhiteSpace(request.EffectiveTo) ? null : DateOnly.Parse(request.EffectiveTo);
        if (request.IsActive.HasValue) leaveType.IsActive = request.IsActive.Value;
        return ToLeaveTypeDto(await repo.UpdateLeaveTypeAsync(leaveType, ct));
    }

    // ================= Contract types =================

    public async Task<Paged<ContractTypeDto>> ListContractTypesAsync(bool includeInactive, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var items = await repo.ListContractTypesAsync(includeInactive, ct);
        if (items.Count == 0)
        {
            var defaults = new[]
            {
                ("permanent", "Permanent", 90, 30),
                ("fixed-term", "Fixed term", 90, 30),
                ("part-time", "Part time", 90, 30),
                ("casual", "Casual", 0, 1),
                ("internship", "Internship", 0, 7),
                ("apprenticeship", "Apprenticeship", 90, 30),
            };
            foreach (var item in defaults)
                await repo.CreateContractTypeAsync(new ContractType { Code = item.Item1, Name = item.Item2, ProbationDays = item.Item3, NoticeDays = item.Item4 }, ct);
            items = await repo.ListContractTypesAsync(includeInactive, ct);
        }
        return new Paged<ContractTypeDto>(items.Select(ToContractTypeDto).ToList(), items.Count, 1, 100);
    }

    public async Task<ContractTypeDto> CreateContractTypeAsync(ContractTypeCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        RequireNonEmpty(request.Code, "code");
        RequireNonEmpty(request.Name, "name");
        var code = request.Code.Trim().ToLowerInvariant();
        if ((await repo.ListContractTypesAsync(true, ct)).Any(t => t.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException("contract-type-code-taken", $"Contract type code '{code}' is already in use.");
        var type = new ContractType
        {
            Code = code,
            Name = request.Name.Trim(),
            ProbationDays = Math.Max(0, request.ProbationDays),
            NoticeDays = Math.Max(0, request.NoticeDays),
        };
        return ToContractTypeDto(await repo.CreateContractTypeAsync(type, ct));
    }

    public async Task<ContractTypeDto> UpdateContractTypeAsync(Guid id, ContractTypeUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var type = await repo.GetContractTypeAsync(id, ct)
            ?? throw new DomainException("contract-type-not-found", $"Contract type {id} does not exist.");
        if (request.Name is not null)
        {
            RequireNonEmpty(request.Name, "name");
            type.Name = request.Name.Trim();
        }
        if (request.ProbationDays.HasValue) type.ProbationDays = Math.Max(0, request.ProbationDays.Value);
        if (request.NoticeDays.HasValue) type.NoticeDays = Math.Max(0, request.NoticeDays.Value);
        if (request.IsActive.HasValue) type.IsActive = request.IsActive.Value;
        return ToContractTypeDto(await repo.UpdateContractTypeAsync(type, ct));
    }

    // ================= Capabilities =================

    public async Task<List<CapabilityConfig>> ListCapabilitiesAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        return await repo.ListCapabilitiesAsync(ct);
    }

    public async Task<CapabilityConfig> UpdateCapabilityAsync(string featureKey, CapabilityUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var capabilities = await repo.ListCapabilitiesAsync(ct);
        var capability = capabilities.FirstOrDefault(c => c.FeatureKey.Equals(featureKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainException("capability-not-found", $"Capability '{featureKey}' does not exist.");
        if (request.Tier is not null) capability.Tier = request.Tier;
        if (request.IsEnabled.HasValue) capability.IsEnabled = request.IsEnabled.Value;
        if (request.Description is not null) capability.Description = request.Description.Trim();
        return await repo.UpdateCapabilityAsync(capability, ct);
    }

    // ================= Mapping helpers =================

    private static LegalEntityDtoFull ToLegalEntityDto(LegalEntity e) => new(
        e.Id, e.Code, e.RegisteredName, e.TradingName, e.PacraNumber, e.Tpin, e.NapsaEmployerRef,
        e.NhimaEmployerRef, e.WcfcbEmployerRef, e.Currency, e.CountryCode, e.IsDefault, e.CreatedAt);

    private static WorkLocationDtoFull ToLocationDto(WorkLocation l, List<LegalEntity> entities, List<WorkCalendar> calendars) => new(
        l.Id, l.Code, l.Name, l.LegalEntityId, entities.FirstOrDefault(e => e.Id == l.LegalEntityId)?.RegisteredName,
        l.AddressLine, l.Province, l.District, l.City, l.Type ?? "branch", l.DefaultCalendarId,
        l.DefaultCalendarId.HasValue ? calendars.FirstOrDefault(c => c.Id == l.DefaultCalendarId)?.Name : null, l.CreatedAt);

    private static OrgUnitDtoFull ToOrgUnitDto(OrgUnit u, List<LegalEntity> entities, List<Worker> workers) => new(
        u.Id, u.Code, u.Name, u.LegalEntityId, entities.FirstOrDefault(e => e.Id == u.LegalEntityId)?.RegisteredName,
        u.ParentId, u.UnitType ?? "department", u.CostCentreRef, u.ManagerId,
        u.ManagerId.HasValue ? workers.FirstOrDefault(w => w.Id == u.ManagerId)?.FullName : null,
        u.EffectiveFrom.ToString("yyyy-MM-dd"), u.EffectiveTo?.ToString("yyyy-MM-dd"), u.Status ?? "active", u.CreatedAt);

    private static WorkCalendarDtoFull ToCalendarDto(WorkCalendar c, List<LegalEntity> entities) => new(
        c.Id, c.Name, c.LegalEntityId, entities.FirstOrDefault(e => e.Id == c.LegalEntityId)?.RegisteredName,
        c.CountryCode, c.StandardWeeklyHours, c.WeekendDays, c.IsDefault, c.Holidays.Count,
        c.Holidays.Select(ToHolidayDto).ToList(), c.CreatedAt);

    private static PublicHolidayDto ToHolidayDto(PublicHoliday h) => new(
        h.Id, h.CalendarId, h.Name, h.HolidayDate.ToString("yyyy-MM-dd"), h.ObservedOn, h.IsRecurring, h.Description);

    private static LeaveTypeDtoFull ToLeaveTypeDto(LeaveType t) => new(
        t.Id, t.Code, t.Name, t.Category, t.DefaultDaysPerYear, t.MaxConsecutiveDays, t.RequiresEvidence,
        t.MinNoticeDays, t.AllowsPartialDays, t.CarryForwardDays, t.CarryForwardExpiryMonths,
        t.AllowNegative, t.EffectiveFrom.ToString("yyyy-MM-dd"), t.EffectiveTo?.ToString("yyyy-MM-dd"),
        t.IsActive, t.CreatedAt);

    private static ContractTypeDto ToContractTypeDto(ContractType t) => new(
        t.Id, t.Code, t.Name, t.ProbationDays, t.NoticeDays, t.IsActive, t.CreatedAt);

    private static void RequireNonEmpty(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("validation-failed", $"{field} is required.");
    }

    private static readonly string[] ValidWeekendDays = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];
    private static string NormalizeWeekendDays(string weekendDays)
    {
        var days = weekendDays.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => d.Trim().ToLowerInvariant())
            .Where(d => ValidWeekendDays.Contains(d));
        return string.Join(",", days.Any() ? days : ["sat", "sun"]);
    }
}
