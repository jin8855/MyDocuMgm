export type WorkflowStep = 'URL' | 'ANALYSIS_REVIEW' | 'CATEGORY_EDIT' | 'MEDIA' | 'DETAIL' | 'BLOG_DRAFT' | 'COMPLETED'
export type ContentStatus = 'INBOX' | 'REVIEW_REQUIRED' | 'READY' | 'ARCHIVED'
export type SearchScope = 'ALL' | 'TAG'
export type MediaFilter = 'ALL' | 'SELECTED' | 'DUPLICATE'
export type MediaSort = 'TIME_ASC' | 'TIME_DESC'

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
  status: ContentStatus
  visibility: 'PRIVATE' | 'PUBLIC_ALLOWED'
  isFavorite: boolean
  experienceStatus: 'NONE' | 'WANT_TO_TRY' | 'TRIED'
  currentWorkflowStep: WorkflowStep
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
  rowVersion: string
}

export interface MediaPage {
  items: MediaItem[]
  totalCount: number
  selectedCount: number
  duplicateCount: number
  page: number
  pageSize: number
  totalPages: number
}
