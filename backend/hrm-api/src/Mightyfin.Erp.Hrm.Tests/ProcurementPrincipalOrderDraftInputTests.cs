using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalOrderDraftInputTests
{
    private static ProcurementPrincipalOrderDraftRoutes.CreateInput Valid()
    {
        var line = new OrderDraftLineInput(null, "Part", "EA", 1m, 2m, 0m, null, null, null);
        var draft = new OrderDraftInput(Guid.NewGuid(), "USD", null, null, null, null, [line]);
        return new(draft, [new OrderSourceSelection(0, Guid.NewGuid(), Guid.NewGuid(), 1)]);
    }

    [Fact]
    public void Valid_selection_and_draft_line_admit_the_create_shape()
        => Assert.True(ProcurementPrincipalOrderDraftRoutes.HasValidCreateSelectionShape(Valid()));

    [Fact]
    public void Null_nested_elements_and_mismatched_or_duplicate_selection_are_rejected_before_dereference()
    {
        var valid = Valid();
        Assert.False(ProcurementPrincipalOrderDraftRoutes.HasValidCreateSelectionShape(valid with
            { Sources = [null!] }));
        Assert.False(ProcurementPrincipalOrderDraftRoutes.HasValidCreateSelectionShape(valid with
            { Draft = valid.Draft with { Lines = [null!] } }));
        Assert.False(ProcurementPrincipalOrderDraftRoutes.HasValidCreateSelectionShape(valid with
            { Sources = [] }));
        var second = new OrderDraftLineInput(null, "Part two", "EA", 1m, 2m, 0m, null, null, null);
        var two = valid.Draft with { Lines = [valid.Draft.Lines![0], second] };
        var selected = valid.Sources[0];
        Assert.False(ProcurementPrincipalOrderDraftRoutes.HasValidCreateSelectionShape(valid with
            { Draft = two, Sources = [selected, selected with { PurchaseRequestLineId = Guid.NewGuid() }] }));
        Assert.False(ProcurementPrincipalOrderDraftRoutes.HasValidCreateSelectionShape(valid with
            { Draft = two, Sources = [selected, selected with { DraftLineIndex = 1 }] }));
    }
}
