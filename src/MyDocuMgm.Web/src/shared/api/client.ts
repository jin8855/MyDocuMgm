import type {
  Category,
  ContentItem,
  ContentPage,
  CookingIngredient,
  MediaFilter,
  MediaItem,
  MediaPage,
  MediaSort,
  MediaUploadResult,
  SearchAttribute,
  SearchQuery,
} from '../types'
import { cloneValue } from '../utils/clone'
import { resolveWorkflowStep } from '../presentation/labels'

const mockEnabled = import.meta.env.MODE === 'test' || import.meta.env.VITE_USE_MOCK_API === 'true'
const rowVersion = 'AAAAAAAAB9E='
const mockContentId = 'demo'
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

export class ApiError extends Error {
  constructor(public readonly code: string, message: string) {
    super(message)
    this.name = 'ApiError'
  }
}

function contentPath(contentId: string): string {
  const value = contentId.trim()
  if (!value || (!mockEnabled && !guidPattern.test(value))) {
    throw new Error('올바른 콘텐츠 ID가 필요합니다.')
  }
  return `/api/contents/${encodeURIComponent(value)}`
}

function readFileAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.addEventListener('load', () => resolve(String(reader.result ?? '')))
    reader.addEventListener('error', () => reject(new ApiError(
      'MEDIA_DECODE_FAILED',
      '선택한 이미지의 미리보기를 만들 수 없습니다.',
    )))
    reader.readAsDataURL(file)
  })
}

const categorySource = [
  ['PLACE', '가볼곳'], ['COOKING', '요리'], ['EXERCISE', '운동'], ['CLEANING_LAUNDRY', '청소&세탁'],
  ['TRAVEL', '여행'], ['PHOTO', '사진'], ['STUDY', '공부'], ['PRODUCT', '제품'],
  ['PHONE_COMPUTER', '폰&컴'], ['TIP', '팁'], ['OTHER', '기타'],
] as const

const attributeSource: Record<string, [string, string][]> = {
  COOKING: [['primaryIngredient', '주재료'], ['difficulty', '난이도'], ['time', '소요시간']],
  PRODUCT: [['brand', '브랜드'], ['store', '구매처'], ['price', '가격대']],
  PLACE: [['region', '지역'], ['parking', '주차']],
  TRAVEL: [['destination', '국가·지역'], ['transport', '교통']],
  EXERCISE: [['targetArea', '운동 부위'], ['equipment', '준비물']],
  CLEANING_LAUNDRY: [['target', '대상'], ['supplies', '세제·제품']],
  PHOTO: [['camera', '기기'], ['location', '촬영 장소']],
  STUDY: [['subject', '분야'], ['resource', '참고 자료']],
  PHONE_COMPUTER: [['deviceOrOs', '기기·OS'], ['problem', '문제 유형']],
  TIP: [['situation', '적용 상황'], ['keyPoint', '주제']],
  OTHER: [['customLabel', '주제']],
}

let categories: Category[] = categorySource.map(([code, displayName], index) => ({
  id: `10000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
  code,
  displayName,
  sortOrder: index + 1,
  isActive: true,
  rowVersion,
  attributes: (attributeSource[code] ?? []).map(([attributeKey, name], attributeIndex) => ({
    id: `${code}-${attributeKey}`,
    categoryCode: code,
    attributeKey,
    displayName: name,
    sortOrder: attributeIndex + 1,
    isActive: true,
    isSearchable: true,
    rowVersion,
  })),
}))

let ingredients: CookingIngredient[] = [
  { id: 'ing-1', sortOrder: 1, name: '새우', quantity: '200g', ingredientType: '주재료', isPrimary: true, note: '', rowVersion },
  { id: 'ing-2', sortOrder: 2, name: '새우 페이스트', quantity: '1.5큰술', ingredientType: '주재료', isPrimary: true, note: '', rowVersion },
  { id: 'ing-3', sortOrder: 3, name: '식빵', quantity: '4장', ingredientType: '부재료', isPrimary: false, note: '', rowVersion },
  { id: 'ing-4', sortOrder: 4, name: '버터', quantity: '20g', ingredientType: '부재료', isPrimary: false, note: '', rowVersion },
]

const workflowSequence = ['URL', 'ANALYSIS_REVIEW', 'CATEGORY_EDIT', 'MEDIA', 'DETAIL', 'BLOG_DRAFT', 'COMPLETED'] as const
let contents: ContentItem[] = Array.from({ length: 31 }, (_, index) => {
  const category = categories[index % categories.length]
  const step = workflowSequence[index % workflowSequence.length]
  return {
    id: index === 0 ? 'demo' : `demo-${index + 1}`,
    categoryId: category.id,
    categoryCode: category.code,
    categoryDisplayName: category.displayName,
    title: index === 0 ? '바삭한 새우 토스트' : `${category.displayName} 생활 기록 ${index + 1}`,
    shortSummary: index === 0 ? '새우 반죽을 식빵에 얹어 노릇하게 익히는 간단한 간식' : '다시 찾기 쉽게 정리한 생활 자료',
    detailContent: '단계와 주의사항을 구조화해 둔 개인 생활 기록입니다.',
    status: index % 4 === 0 ? 'REVIEW_REQUIRED' : 'READY',
    visibility: 'PRIVATE',
    isFavorite: index % 5 === 0,
    experienceStatus: index % 3 === 0 ? 'TRIED' : 'NONE',
    currentWorkflowStep: step,
    blogDraftStatus: step === 'BLOG_DRAFT' || step === 'COMPLETED' ? '초안 준비' : '미작성',
    updatedAtUtc: new Date(Date.now() - index * 86_400_000).toISOString(),
    createdAtUtc: new Date(Date.now() - (index + 3) * 86_400_000).toISOString(),
    rowVersion,
    tags: index === 0 ? ['새우', '간단요리', '간식'] : [category.displayName, '생활팁'],
  }
})

let media: import('../types').MediaItem[] = Array.from({ length: 137 }, (_, index) => ({
  id: `media-${index + 1}`,
  originalFileName: `capture-${String(index + 1).padStart(3, '0')}.webp`,
  thumbnailUrl: '',
  mimeType: 'image/webp',
  sizeBytes: 120_000 + index * 341,
  width: 1280,
  height: 720,
  sortOrder: index + 1,
  sourceTimestampMs: index * 2_500,
  isSelected: index % 11 === 0,
  isPublicAllowed: false,
  description: index === 0 ? '새우 손질 단계' : '',
  storageStatus: 'READY',
  sha256: index < 4 ? 'DUPLICATE-A' : `HASH-${index}`,
  isDeleted: false,
  deletedAtUtc: null,
  rowVersion,
}))

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: init?.body instanceof FormData ? init.headers : { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as {
      title?: string
      detail?: string
      code?: string
    } | null
    throw new ApiError(
      problem?.code ?? 'UNEXPECTED_ERROR',
      problem?.detail ?? problem?.title ?? `요청 실패 (${response.status})`,
    )
  }
  return response.status === 204 ? undefined as T : await response.json() as T
}

export const api = {
  async categories(): Promise<Category[]> {
    return mockEnabled ? cloneValue(categories) : request('/api/categories')
  },
  async updateCategory(code: string, value: Category): Promise<Category> {
    if (mockEnabled) {
      categories = categories.map((category) => category.code === code ? cloneValue(value) : category)
      return cloneValue(value)
    }
    return request(`/api/categories/${code}`, { method: 'PATCH', body: JSON.stringify(value) })
  },
  async attributes(code: string): Promise<SearchAttribute[]> {
    if (mockEnabled) return cloneValue(categories.find((value) => value.code === code)?.attributes ?? [])
    return request(`/api/categories/${code}/search-attributes`)
  },
  async updateAttribute(code: string, value: SearchAttribute): Promise<SearchAttribute> {
    if (mockEnabled) {
      const category = categories.find((item) => item.code === code)
      if (category) category.attributes = category.attributes?.map((item) => item.attributeKey === value.attributeKey ? cloneValue(value) : item)
      return cloneValue(value)
    }
    return request(`/api/categories/${code}/search-attributes/${value.attributeKey}`, { method: 'PATCH', body: JSON.stringify(value) })
  },
  async contents(query: SearchQuery): Promise<ContentPage> {
    if (!mockEnabled) {
      const params = new URLSearchParams(Object.entries(query).map(([key, value]) => [key, String(value)]))
      return request(`/api/contents?${params}`)
    }
    let result = [...contents]
    if (query.majorCategory) result = result.filter((item) => item.categoryCode === query.majorCategory)
    if (query.status) result = result.filter((item) => item.status === query.status)
    if (query.workflowStep !== '') {
      const workflowStep = resolveWorkflowStep(query.workflowStep)
      if (!workflowStep) throw new Error('알 수 없는 workflow 단계입니다.')
      result = result.filter((item) => resolveWorkflowStep(item.currentWorkflowStep) === workflowStep)
    }
    if (query.keyword) {
      const term = query.keyword.toLocaleLowerCase('ko')
      result = result.filter((item) => query.searchScope === 'TAG'
        ? item.tags.some((tag) => tag.toLocaleLowerCase('ko').includes(term))
        : `${item.title} ${item.shortSummary} ${item.detailContent} ${item.tags.join(' ')}`.toLocaleLowerCase('ko').includes(term))
    }
    if (query.attributeKey === 'primaryIngredient' && query.attributeValue) {
      result = result.filter((item) => item.categoryCode === 'COOKING' &&
        ingredients.some((ingredient) => ingredient.isPrimary && ingredient.name.includes(query.attributeValue)))
    }
    const start = (query.page - 1) * query.pageSize
    return {
      items: cloneValue(result.slice(start, start + query.pageSize)),
      totalCount: result.length,
      page: query.page,
      pageSize: query.pageSize,
      totalPages: Math.max(1, Math.ceil(result.length / query.pageSize)),
    }
  },
  async content(id: string): Promise<ContentItem> {
    if (mockEnabled) {
      const item = contents.find((value) => value.id === id)
      if (!item) throw new Error('콘텐츠를 찾을 수 없습니다.')
      return cloneValue(item)
    }
    return request(contentPath(id))
  },
  async ingredients(contentId: string): Promise<CookingIngredient[]> {
    if (mockEnabled) return contentId === mockContentId ? cloneValue(ingredients) : []
    return request(`${contentPath(contentId)}/ingredients`)
  },
  async saveIngredient(contentId: string, value: CookingIngredient): Promise<CookingIngredient> {
    if (mockEnabled) {
      if (contentId !== mockContentId) throw new Error('콘텐츠와 재료가 일치하지 않습니다.')
      const exists = ingredients.some((item) => item.id === value.id)
      ingredients = exists
        ? ingredients.map((item) => item.id === value.id ? cloneValue(value) : item)
        : [...ingredients, { ...cloneValue(value), id: `ing-${Date.now()}`, sortOrder: ingredients.length + 1 }]
      return cloneValue(value)
    }
    const path = `${contentPath(contentId)}/ingredients`
    return value.id
      ? request(`${path}/${encodeURIComponent(value.id)}`, { method: 'PUT', body: JSON.stringify(value) })
      : request(path, { method: 'POST', body: JSON.stringify(value) })
  },
  async deleteIngredient(contentId: string, value: CookingIngredient): Promise<void> {
    if (mockEnabled) {
      if (contentId !== mockContentId) throw new Error('콘텐츠와 재료가 일치하지 않습니다.')
      ingredients = ingredients.filter((item) => item.id !== value.id)
      return
    }
    return request(`${contentPath(contentId)}/ingredients/${encodeURIComponent(value.id)}?rowVersion=${encodeURIComponent(value.rowVersion)}`, { method: 'DELETE' })
  },
  async media(contentId: string, filter: MediaFilter, sort: MediaSort, page: number, pageSize: number): Promise<MediaPage> {
    if (!mockEnabled) return request(`${contentPath(contentId)}/media?filter=${filter}&sort=${sort}&page=${page}&pageSize=${pageSize}`)
    if (contentId !== mockContentId) {
      return { items: [], totalCount: 0, selectedCount: 0, duplicateCount: 0, deletedCount: 0, page, pageSize, totalPages: 1 }
    }
    const ready = media.filter((item) => !item.isDeleted)
    const duplicateHashes = new Set(ready.filter((item, index, all) => all.some((other, otherIndex) => otherIndex !== index && other.sha256 === item.sha256)).map((item) => item.sha256))
    let result = filter === 'SELECTED' ? ready.filter((item) => item.isSelected)
      : filter === 'DUPLICATE' ? ready.filter((item) => duplicateHashes.has(item.sha256))
        : filter === 'DELETED' ? media.filter((item) => item.isDeleted) : [...ready]
    result.sort((a, b) => sort === 'TIME_DESC'
      ? (b.sourceTimestampMs ?? b.sortOrder) - (a.sourceTimestampMs ?? a.sortOrder)
      : (a.sourceTimestampMs ?? a.sortOrder) - (b.sourceTimestampMs ?? b.sortOrder))
    const start = (page - 1) * pageSize
    return {
      items: cloneValue(result.slice(start, start + pageSize)),
      totalCount: result.length,
      selectedCount: ready.filter((item) => item.isSelected).length,
      duplicateCount: ready.filter((item) => duplicateHashes.has(item.sha256)).length,
      deletedCount: media.filter((item) => item.isDeleted).length,
      page,
      pageSize,
      totalPages: Math.max(1, Math.ceil(result.length / pageSize)),
    }
  },
  async updateMedia(contentId: string, value: MediaItem): Promise<void> {
    if (mockEnabled) {
      if (contentId !== mockContentId) throw new Error('콘텐츠와 이미지가 일치하지 않습니다.')
      media = media.map((item) => item.id === value.id ? cloneValue(value) : item)
      return
    }
    await request(`${contentPath(contentId)}/media/${encodeURIComponent(value.id)}`, { method: 'PATCH', body: JSON.stringify(value) })
  },
  async uploadMedia(
    contentId: string,
    file: File,
    onProgress: (value: number) => void,
    signal: AbortSignal,
  ): Promise<MediaUploadResult> {
    if (mockEnabled) {
      if (contentId !== mockContentId) throw new ApiError('MEDIA_CONTENT_NOT_FOUND', '콘텐츠를 찾을 수 없습니다.')
      for (const value of [20, 55, 85, 100]) {
        if (signal.aborted) throw new ApiError('MEDIA_OPERATION_CANCELLED', '이미지 작업이 취소되었습니다.')
        await new Promise((resolve) => setTimeout(resolve, 15))
        onProgress(value)
      }
      const existing = media.find((item) => !item.isDeleted && item.sizeBytes === file.size && item.originalFileName === file.name)
      if (existing) return { item: cloneValue(existing), reused: true }
      const item: MediaItem = {
        id: `media-${Date.now()}`,
        originalFileName: file.name,
        thumbnailUrl: await readFileAsDataUrl(file),
        mimeType: file.type,
        sizeBytes: file.size,
        width: 1,
        height: 1,
        sortOrder: media.length + 1,
        sourceTimestampMs: null,
        isSelected: false,
        isPublicAllowed: false,
        description: '',
        storageStatus: 'READY',
        sha256: `MOCK-${file.name}-${file.size}`,
        isDeleted: false,
        deletedAtUtc: null,
        rowVersion,
      }
      media.push(item)
      return { item: cloneValue(item), reused: false }
    }

    return new Promise<MediaUploadResult>((resolve, reject) => {
      const xhr = new XMLHttpRequest()
      const abort = () => xhr.abort()
      signal.addEventListener('abort', abort, { once: true })
      xhr.open('POST', `${contentPath(contentId)}/media`)
      xhr.responseType = 'json'
      xhr.upload.addEventListener('progress', (event) => {
        if (event.lengthComputable) onProgress(Math.round(event.loaded * 100 / event.total))
      })
      xhr.addEventListener('load', () => {
        signal.removeEventListener('abort', abort)
        if (xhr.status >= 200 && xhr.status < 300) {
          resolve(xhr.response as MediaUploadResult)
          return
        }
        const problem = xhr.response as { code?: string; detail?: string; title?: string } | null
        reject(new ApiError(
          problem?.code ?? 'UNEXPECTED_ERROR',
          problem?.detail ?? problem?.title ?? `요청 실패 (${xhr.status})`,
        ))
      })
      xhr.addEventListener('error', () => {
        signal.removeEventListener('abort', abort)
        reject(new ApiError('MEDIA_UPLOAD_NETWORK_FAILED', '업로드 요청을 완료하지 못했습니다.'))
      })
      xhr.addEventListener('abort', () => {
        signal.removeEventListener('abort', abort)
        reject(new ApiError('MEDIA_OPERATION_CANCELLED', '이미지 작업이 취소되었습니다.'))
      })
      const body = new FormData()
      body.append('file', file)
      xhr.send(body)
    })
  },
  async deleteMedia(contentId: string, value: MediaItem): Promise<void> {
    if (mockEnabled) {
      media = media.map((item) => item.id === value.id
        ? { ...item, isDeleted: true, deletedAtUtc: new Date().toISOString() }
        : item)
      return
    }
    await request(
      `${contentPath(contentId)}/media/${encodeURIComponent(value.id)}?rowVersion=${encodeURIComponent(value.rowVersion)}`,
      { method: 'DELETE' },
    )
  },
  async restoreMedia(contentId: string, value: MediaItem): Promise<MediaItem> {
    if (mockEnabled) {
      const restored = { ...value, isDeleted: false, deletedAtUtc: null }
      media = media.map((item) => item.id === value.id ? restored : item)
      return cloneValue(restored)
    }
    return request(`${contentPath(contentId)}/media/${encodeURIComponent(value.id)}/restore`, {
      method: 'POST',
      body: JSON.stringify({ rowVersion: value.rowVersion }),
    })
  },
  async moveMedia(contentId: string, value: MediaItem, direction: -1 | 1): Promise<void> {
    if (mockEnabled) {
      const ordered = media.filter((item) => !item.isDeleted).sort((a, b) => a.sortOrder - b.sortOrder)
      const index = ordered.findIndex((item) => item.id === value.id)
      const target = ordered[index + direction]
      if (index >= 0 && target) {
        const sortOrder = value.sortOrder
        value.sortOrder = target.sortOrder
        target.sortOrder = sortOrder
      }
      return
    }
    await request(`${contentPath(contentId)}/media/${encodeURIComponent(value.id)}/move`, {
      method: 'POST',
      body: JSON.stringify({ direction, rowVersion: value.rowVersion }),
    })
  },
}
