export type WorkflowStep = 'URL' | 'ANALYSIS_REVIEW' | 'CATEGORY_EDIT' | 'MEDIA' | 'DETAIL' | 'BLOG_DRAFT' | 'COMPLETED'
export type ContentStatus = 'INBOX' | 'REVIEW_REQUIRED' | 'READY' | 'ARCHIVED'
export type WorkflowStepValue = WorkflowStep | number
export type ContentStatusValue = ContentStatus | number
export type SearchScope = 'ALL' | 'TAG'
export type MediaFilter = 'ALL' | 'SELECTED' | 'DUPLICATE' | 'DELETED'
export type MediaSort = 'TIME_ASC' | 'TIME_DESC'
export type ContentSourceKind = 'GENERIC' | 'INSTAGRAM'
export type InstagramContentType = 'POST' | 'REEL'
export type PinnedAuthorCommentState = 'PRESENT' | 'NONE'
export type SourceAcquisitionMode = 'MANUAL' | 'HTTP_METADATA'
export type ExternalFetchStatus = 'STARTED' | 'SUCCEEDED' | 'FAILED' | 'CANCELLED' | 'APPLIED'
export type IntakeStatus = 'URL_ACCEPTED' | 'MANUAL_INPUT_REQUIRED' | 'CONTENT_READY'
export type AnalysisRecommendationRunStatus = 'REQUESTED' | 'SUCCEEDED' | 'PARTIALLY_SUCCEEDED' | 'FAILED' | 'CANCELLED'
export type AnalysisRecommendationKind = 'TITLE' | 'SUMMARY' | 'CATEGORY' | 'TAG'
export type AnalysisRecommendationConfidence = 'LOW' | 'MEDIUM' | 'HIGH'
export type AnalysisRecommendationDecision = 'PENDING' | 'APPLIED' | 'MODIFIED' | 'REJECTED'
export type AnalysisRecommendationEvidenceType = 'DETAIL_CONTENT' | 'MANUAL_CAPTION' | 'PINNED_AUTHOR_COMMENT' | 'SOURCE_EVIDENCE'
export type ManualPromptEvidenceKind = 'CURRENT_TITLE' | 'CURRENT_SUMMARY' | 'DETAIL_CONTENT' | 'MANUAL_CAPTION' | 'PINNED_AUTHOR_COMMENT' | 'SOURCE_EVIDENCE' | 'CURRENT_CATEGORY' | 'CURRENT_TAGS'

export interface ManualRecommendationPromptEvidence {
  evidenceId: string
  kind: ManualPromptEvidenceKind
  sourceEvidenceId: string | null
  label: string
  text: string
}

export interface ManualRecommendationPrompt {
  schemaVersion: string
  sourceFingerprint: string
  prompt: string
  evidence: ManualRecommendationPromptEvidence[]
}

export interface AnalysisRecommendationEvidence {
  evidenceType: AnalysisRecommendationEvidenceType
  sourceEvidenceId: string | null
  excerpt: string
}

export interface AnalysisRecommendationItem {
  id: string
  kind: AnalysisRecommendationKind
  recommendedValue: string
  reason: string
  confidence: AnalysisRecommendationConfidence
  decision: AnalysisRecommendationDecision
  modifiedValue: string | null
  decidedAtUtc: string | null
  evidence: AnalysisRecommendationEvidence[]
}

export interface AnalysisRecommendationRun {
  id: string
  contentId: string
  requestedAtUtc: string
  completedAtUtc: string | null
  status: AnalysisRecommendationRunStatus
  providerIdentifier: string
  modelVersion: string | null
  errorCode: string | null
  rowVersion: string
  items: AnalysisRecommendationItem[]
}

export interface AnalysisRecommendationDecisionInput {
  itemId: string
  decision: Exclude<AnalysisRecommendationDecision, 'PENDING'>
  modifiedValue: string | null
}

export interface AnalysisRecommendationDecisionResult {
  content: ContentItem
  run: AnalysisRecommendationRun
}

export interface SearchAttribute {
  id: string
  categoryCode: string
  attributeKey: string
  displayName: string
  sortOrder: number
  isActive: boolean
  isSearchable: boolean
  rowVersion: string
}

export interface Category {
  id: string
  sortOrder: number
  code: string
  displayName: string
  isActive: boolean
  rowVersion: string
  attributes?: SearchAttribute[]
}

export interface ContentItem {
  id: string
  categoryId: string
  categoryCode: string
  categoryDisplayName: string
  title: string
  shortSummary: string | null
  detailContent: string | null
  status: ContentStatusValue
  visibility: 'PRIVATE' | 'PUBLIC_ALLOWED'
  isFavorite: boolean
  experienceStatus: 'NONE' | 'WANT_TO_TRY' | 'TRIED'
  currentWorkflowStep: WorkflowStepValue
  blogDraftStatus: string
  updatedAtUtc: string
  createdAtUtc: string
  rowVersion: string
  tags: string[]
}

export interface ContentPage {
  items: ContentItem[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface SearchQuery {
  majorCategory: string
  attributeKey: string
  attributeValue: string
  status: string
  workflowStep: '' | number
  searchScope: SearchScope
  keyword: string
  page: number
  pageSize: 24 | 48 | 96
}

export interface CookingIngredient {
  id: string
  sortOrder: number
  name: string
  quantity: string
  ingredientType: '주재료' | '부재료' | '확인 필요'
  isPrimary: boolean
  note: string
  rowVersion: string
}

export interface CategoryEdit {
  contentId: string
  title: string
  shortSummary: string | null
  categoryId: string
  categoryCode: string
  currentWorkflowStep: WorkflowStepValue
  rowVersion: string
  valuesByCategory: Record<string, Record<string, string | null>>
}

export interface ImageStageMediaItem {
  id: string
  ownerContentId: string
  originalFileName: string
  thumbnailUrl: string
  mimeType: string
  sizeBytes: number
  width: number
  height: number
}

export interface ImageStage {
  contentId: string
  currentWorkflowStep: WorkflowStepValue
  rowVersion: string
  linkedMediaIds: string[]
  linkedMedia: ImageStageMediaItem[]
}

export interface DetailStageIngredientItem {
  id: string
  sortOrder: number
  name: string
  quantity: string | null
  ingredientType: string
  isPrimary: boolean
  note: string | null
}

export interface DetailStageMediaItem {
  id: string
  ownerContentId: string
  originalFileName: string
  thumbnailUrl: string
  mimeType: string
  sizeBytes: number
  width: number
  height: number
}

export interface DetailStage {
  contentId: string
  currentWorkflowStep: WorkflowStepValue
  rowVersion: string
  title: string
  shortSummary: string | null
  originalUrl: string | null
  normalizedUrl: string | null
  sourceKind: ContentSourceKind | null
  instagramContentType: InstagramContentType | null
  manualCaption: string | null
  pinnedAuthorCommentState: PinnedAuthorCommentState | null
  pinnedAuthorCommentText: string | null
  manualBody: string | null
  categoryId: string
  categoryCode: string
  categoryDisplayName: string
  categoryValues: Record<string, string | null>
  ingredients: DetailStageIngredientItem[]
  linkedMedia: DetailStageMediaItem[]
  editableFields: string[]
}

export interface BlogDraftMediaItem {
  id: string
  ownerContentId: string
  originalFileName: string
  thumbnailUrl: string
  mimeType: string
  width: number
  height: number
}

export interface BlogDraft {
  contentId: string
  currentWorkflowStep: WorkflowStepValue
  contentRowVersion: string
  hasSavedDraft: boolean
  draftRowVersion: string | null
  title: string
  body: string
  titleMaxLength: number
  bodyMaxLength: number
  analysisTitle: string
  shortSummary: string | null
  categoryDisplayName: string
  requiresExternalSourceReuseConfirmation: boolean
  externalSourceReuseConfirmed: boolean
  linkedMedia: BlogDraftMediaItem[]
}

export interface BlogDraftPatch {
  title?: string | null
  body?: string | null
}

export interface MediaItem {
  id: string
  originalFileName: string
  thumbnailUrl: string
  mimeType: string
  sizeBytes: number
  width: number
  height: number
  sortOrder: number
  sourceTimestampMs: number | null
  isSelected: boolean
  isPublicAllowed: boolean
  description: string
  storageStatus: 'PENDING' | 'READY' | 'FAILED'
  sha256: string
  isDeleted: boolean
  deletedAtUtc: string | null
  rowVersion: string
}

export interface MediaPage {
  items: MediaItem[]
  totalCount: number
  selectedCount: number
  duplicateCount: number
  deletedCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface MediaUploadResult {
  item: MediaItem
  reused: boolean
}

export interface UrlIntake {
  id: string
  originalUrl: string
  normalizedUrl: string
  sourceKind: ContentSourceKind
  status: IntakeStatus
  isDuplicate: boolean
  manualBody: string | null
  manualBodyPresent: boolean
  instagramContentType: InstagramContentType | null
  manualCaption: string | null
  pinnedAuthorCommentState: PinnedAuthorCommentState | null
  pinnedAuthorCommentText: string | null
  sourceAcquisitionMode: SourceAcquisitionMode | null
  linkedMediaIds: string[]
}

export interface ExternalFetchAttempt {
  id: string
  contentId: string
  attemptNumber: number
  status: ExternalFetchStatus
  finalUrl: string | null
  httpStatusCode: number | null
  responseMimeType: string | null
  responseBytes: number | null
  contentSha256: string | null
  eTag: string | null
  lastModifiedAtUtc: string | null
  title: string | null
  description: string | null
  authorName: string | null
  publishedAtUtc: string | null
  body: string | null
  errorCode: string | null
  errorMessage: string | null
  startedAtUtc: string
  completedAtUtc: string | null
}

export interface ExternalFetchApply {
  attempt: ExternalFetchAttempt
  intake: UrlIntake
}

export interface LinkableMediaItem {
  id: string
  ownerContentId: string
  originalFileName: string
  thumbnailUrl: string
  mimeType: string
  sizeBytes: number
  width: number
  height: number
}

export interface LinkableMediaPage {
  items: LinkableMediaItem[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface TrashContentItem {
  id: string
  title: string
  deletedAtUtc: string | null
  ownedMediaCount: number
  rowVersion: string
}

export interface OrphanMediaItem {
  id: string
  contentId: string
  originalFileName: string
  linkCount: number
  fileExists: boolean
  fileState: string
  rowVersion: string
}
