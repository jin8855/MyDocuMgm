export type ContentStatus = 'INBOX' | 'REVIEW_REQUIRED' | 'READY' | 'ARCHIVED'

export interface Category {
  id: string
  sortOrder: number
  code: string
  displayName: string
}

export interface ContentItem {
  id: string
  categoryId?: string
  categoryCode: string
  categoryDisplayName?: string
  title: string
  shortSummary: string | null
  detailContent?: string | null
  status: ContentStatus
  visibility: 'PRIVATE' | 'PUBLIC_ALLOWED'
  isFavorite: boolean
  experienceStatus?: 'NONE' | 'WANT_TO_TRY' | 'TRIED'
  updatedAtUtc: string
  createdAtUtc?: string
  rowVersion: string
  tags?: string[]
}

export interface ContentPage {
  items: ContentItem[]
  totalCount: number
  page: number
  pageSize: number
}

export interface SaveContent {
  categoryId: string
  title: string
  shortSummary: string
  detailContent: string
  status: ContentStatus
  visibility: 'PRIVATE' | 'PUBLIC_ALLOWED'
  isFavorite: boolean
  experienceStatus: 'NONE' | 'WANT_TO_TRY' | 'TRIED'
  tags: string[]
  rowVersion?: string
}

export interface MediaItem {
  id: string
  originalFileName: string
  description: string | null
  storageStatus: 'PENDING' | 'READY' | 'FAILED'
  isPublicAllowed: boolean
  isDeleted: boolean
  width: number
  height: number
  sizeBytes: number
}
