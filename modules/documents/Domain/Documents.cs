namespace Smartboxx.Modules.Documents.Domain;

public enum DocumentLifecycle { Received, Processing, ReviewRequired, Completed, Failed, Archived }
public enum ProcessingStageKind { Ingestion, SecurityValidation, Normalisation, Preprocessing, Splitting, Classification, Extraction, Validation, Confidence, Review, Publish }
public enum StageState { Pending, Running, Succeeded, Failed, Cancelled }
public sealed record SourceLineage(string Source, string? ExternalId, string OriginalFileName, string ContentType, string Sha256);
public sealed record DocumentPage(Guid Id, int Number, string OriginalObjectName, string? NormalisedObjectName, string OriginalChecksum, string? NormalisedChecksum, IReadOnlyDictionary<string, string> Metadata);
public sealed record ProcessingStage(Guid Id, ProcessingStageKind Kind, StageState State, int Attempts, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? ErrorCode, IReadOnlyDictionary<string, string> ProviderMetadata);
public sealed record ProcessingRun(Guid Id, int Number, DateTimeOffset CreatedAt, IReadOnlyList<ProcessingStage> Stages);
public sealed record Document(Guid Id, string TenantId, SourceLineage Lineage, DocumentLifecycle Lifecycle, IReadOnlyList<DocumentPage> Pages, IReadOnlyList<ProcessingRun> ProcessingHistory, DateTimeOffset CreatedAt);
public sealed record DocumentPackage(Guid Id, string TenantId, IReadOnlyList<Guid> DocumentIds, IReadOnlyList<ProcessingRun> ProcessingHistory, DateTimeOffset CreatedAt);

public enum RegistryLifecycle { Draft, Published, Archived }
public sealed record ExtractionSchema(string Reference, int Version);
public sealed record ValidationRuleSet(string Reference, int Version);
public sealed record ReviewPolicy(decimal ClassificationThreshold, decimal DefaultFieldThreshold, decimal AutoApproveThreshold, IReadOnlyDictionary<string, decimal> FieldThresholds, IReadOnlySet<string> MandatoryReviewFields);
public sealed record ModelConfiguration(string PurposeAlias, string Strategy, IReadOnlyDictionary<string, string> Parameters);
public sealed record OutputMapping(IReadOnlyDictionary<string, string> Fields);

public sealed record DocumentTypeDefinition(
    Guid Id, string TenantId, string TechnicalName, int Version, RegistryLifecycle Lifecycle,
    string DisplayName, string? Description, string ClassificationInstructions,
    ExtractionSchema ExtractionSchema, string Prompt, ValidationRuleSet ValidationRules,
    ReviewPolicy ReviewPolicy, ModelConfiguration ModelConfiguration,
    string? EvaluationDatasetReference, OutputMapping OutputMapping,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string UpdatedBy)
{
    public bool IsMutable => Lifecycle == RegistryLifecycle.Draft;
}
