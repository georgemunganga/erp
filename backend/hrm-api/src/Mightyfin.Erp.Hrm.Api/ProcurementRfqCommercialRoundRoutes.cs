using Microsoft.AspNetCore.Authorization;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static void MapCommercialRoundRoutes(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}/commercial-draft", CommercialSave)
            .RequireAuthorization("procurement-sourcing-commercial-configure");
        group.MapPost("/{id:guid}/commercial-prepare", CommercialPrepare)
            .RequireAuthorization("procurement-sourcing-commercial-configure");
        group.MapPost("/{id:guid}/commercial-rounds", CommercialOpen)
            .RequireAuthorization("procurement-sourcing-round-open");
        group.MapPost("/commercial-rounds/{id:guid}/assignments/{assignmentId:guid}/decisions",
            CommercialDecide).RequireAuthorization(
                PrincipalRfqCommercialRoundPolicyRules.ApprovalAction);
        group.MapPost("/{eventId:guid}/commercial-rounds/{id:guid}/offers", CommercialSubmitOffer)
            .RequireAuthorization(PrincipalRfqSealedManualOffers.SubmitAction);
        group.MapPost("/commercial-rounds/{id:guid}/close", CommercialClose)
            .RequireAuthorization(PrincipalRfqSealedManualOffers.CloseAction);
        group.MapPost("/commercial-rounds/{id:guid}/open-offers", CommercialOpenOffers)
            .RequireAuthorization(PrincipalRfqOfferOpeningControls.OpenAction);
        group.MapGet("/commercial-opened-rounds", CommercialOpenedRoundWorklist)
            .RequireAuthorization(PrincipalRfqOfferOpeningControls.ReadAction);
        group.MapGet("/commercial-rounds/{id:guid}/opened-offers", CommercialOpenedOffers)
            .RequireAuthorization(PrincipalRfqOfferOpeningControls.ReadAction);
        group.MapGet("/commercial-rounds/{id:guid}/opening-commands/{key:guid}", CommercialOfferOpeningRecovery)
            .RequireAuthorization(PrincipalRfqOfferOpeningControls.OpenAction);
        group.MapPost("/commercial-rounds/{id:guid}/assessments", CommercialRecordCriterionAssessment)
            .RequireAuthorization(PrincipalRfqCriterionAssessmentStorage.Action);
        group.MapGet("/commercial-rounds/{id:guid}/assessments/own", CommercialOwnCriterionAssessments)
            .RequireAuthorization(PrincipalRfqCriterionAssessmentStorage.Action);
        group.MapGet("/commercial-rounds/{id:guid}/assessment-commands/{key:guid}", CommercialCriterionAssessmentRecovery)
            .RequireAuthorization(PrincipalRfqCriterionAssessmentStorage.Action);
        group.MapPost("/commercial-rounds/{id:guid}/recommendation-drafts", CommercialSaveRecommendation)
            .RequireAuthorization(PrincipalRfqRecommendationDraft.Action);
        group.MapGet("/commercial-rounds/{id:guid}/recommendation-source", CommercialRecommendationSource)
            .RequireAuthorization(PrincipalRfqRecommendationDraft.Action);
        group.MapGet("/commercial-rounds/{id:guid}/recommendation-drafts/own", CommercialOwnRecommendations)
            .RequireAuthorization(PrincipalRfqRecommendationDraft.Action);
        group.MapGet("/commercial-rounds/{id:guid}/recommendation-drafts/{draftId:guid}", CommercialRecommendationDetail)
            .RequireAuthorization(PrincipalRfqRecommendationDraft.Action);
        group.MapGet("/commercial-rounds/{id:guid}/recommendation-commands/{key:guid}", CommercialRecommendationRecovery)
            .RequireAuthorization(PrincipalRfqRecommendationDraft.Action);
        group.MapGet("/commercial-rounds/{id:guid}/offers/{offerId:guid}/criteria/{criterionCode}/assessment-coverage",
            CommercialCriterionAssessmentCoverage)
            .RequireAuthorization(PrincipalRfqOfferOpeningControls.ReadAction);
        group.MapGet("/commercial-rounds", CommercialRounds).RequireAuthorization();
        group.MapGet("/commercial-rounds/{id:guid}", CommercialRound).RequireAuthorization();
        group.MapGet("/commercial-round-reviews/assigned", CommercialRoundAssigned)
            .RequireAuthorization(PrincipalRfqCommercialRoundPolicyRules.ApprovalAction);
        group.MapGet("/commercial-round-reviews/{id:guid}/assignments/{assignmentId:guid}",
            CommercialRoundReview).RequireAuthorization(
                PrincipalRfqCommercialRoundPolicyRules.ApprovalAction);
        group.MapGet("/commercial-round-reviews/{id:guid}/assignments/{assignmentId:guid}/documents",
            CommercialRoundReviewDocuments).RequireAuthorization(
                PrincipalRfqCommercialRoundPolicyRules.ApprovalAction);
        group.MapGet("/commercial-round-reviews/{id:guid}/assignments/{assignmentId:guid}/documents/{versionId:guid}/download",
            CommercialRoundReviewDocumentDownload).RequireAuthorization(
                PrincipalRfqCommercialRoundPolicyRules.ApprovalAction);
        group.MapGet("/commercial-round-commands/{operation}/{key:guid}", CommercialRoundRecovery)
            .RequireAuthorization();
    }
}
