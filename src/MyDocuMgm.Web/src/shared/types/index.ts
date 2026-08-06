export type WorkflowStep = 'URL' | 'ANALYSIS_REVIEW' | 'CATEGORY_EDIT' | 'MEDIA' | 'DETAIL' | 'BLOG_DRAFT' | 'COMPLETED'
export type ContentStatus = 'INBOX' | 'REVIEW_REQUIRED' | 'READY' | 'ARCHIVED'
export type WorkflowStepValue = WorkflowStep | number
export type ContentStatusValue = ContentStatus | number
export type SearchScope = 'ALL' | 'TAG'
export type MediaFilter = 'ALL' | 'SELECTED' | 'DUPLICATE' | 'DELETED'
export type MediaSort = 'TIME_ASC' | 'TIME_DESC'
export type ContentSourceKind = 'GENERIC' | 'INSTAGRAM'
export type IntakeStatus = 'URL_ACCEPTED' | 'MANUAL_INPUT_REQUIRED' | 'CONTENT_READY'

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
  linkedMediaIds: string[]
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
