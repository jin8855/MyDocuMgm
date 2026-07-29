import type {
  Category,
  ContentItem,
  ContentPage,
  CookingIngredient,
  MediaFilter,
  MediaPage,
  MediaSort,
  SearchAttribute,
  SearchQuery,
} from '../types'
import { cloneValue } from '../utils/clone'

const mockEnabled = import.meta.env.MODE === 'test' || import.meta.env.VITE_USE_MOCK_API === 'true'
const rowVersion = 'AAAAAAAAB9E='

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
  rowVersion,
}))

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: init?.body instanceof FormData ? init.headers : { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { title?: string; detail?: string } | null
    throw new Error(problem?.detail ?? problem?.title ?? `요청 실패 (${response.status})`)
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
      const item = contents.find((value) => value.id === id) ?? contents[0]
      return cloneValue(item)
    }
    return request(`/api/contents/${id}`)
  },
  async ingredients(): Promise<CookingIngredient[]> {
    return mockEnabled ? cloneValue(ingredients) : request('/api/contents/demo/ingredients')
  },
  async saveIngredient(value: CookingIngredient): Promise<CookingIngredient> {
    if (mockEnabled) {
      const exists = ingredients.some((item) => item.id === value.id)
      ingredients = exists
        ? ingredients.map((item) => item.id === value.id ? cloneValue(value) : item)
        : [...ingredients, { ...cloneValue(value), id: `ing-${Date.now()}`, sortOrder: ingredients.length + 1 }]
      return cloneValue(value)
    }
    return request(`/api/contents/demo/ingredients/${value.id}`, { method: 'PUT', body: JSON.stringify(value) })
  },
  async deleteIngredient(id: string): Promise<void> {
    if (mockEnabled) { ingredients = ingredients.filter((item) => item.id !== id); return }
    return request(`/api/contents/demo/ingredients/${id}?rowVersion=${encodeURIComponent(rowVersion)}`, { method: 'DELETE' })
  },
  async media(filter: MediaFilter, sort: MediaSort, page: number, pageSize: number): Promise<MediaPage> {
    if (!mockEnabled) return request(`/api/contents/demo/media?filter=${filter}&sort=${sort}&page=${page}&pageSize=${pageSize}`)
    const duplicateHashes = new Set(media.filter((item, index, all) => all.some((other, otherIndex) => otherIndex !== index && other.sha256 === item.sha256)).map((item) => item.sha256))
    let result = filter === 'SELECTED' ? media.filter((item) => item.isSelected)
      : filter === 'DUPLICATE' ? media.filter((item) => duplicateHashes.has(item.sha256)) : [...media]
    result.sort((a, b) => sort === 'TIME_DESC'
      ? (b.sourceTimestampMs ?? b.sortOrder) - (a.sourceTimestampMs ?? a.sortOrder)
      : (a.sourceTimestampMs ?? a.sortOrder) - (b.sourceTimestampMs ?? b.sortOrder))
    const start = (page - 1) * pageSize
    return {
      items: cloneValue(result.slice(start, start + pageSize)),
      totalCount: result.length,
      selectedCount: media.filter((item) => item.isSelected).length,
      duplicateCount: media.filter((item) => duplicateHashes.has(item.sha256)).length,
      page,
      pageSize,
      totalPages: Math.max(1, Math.ceil(result.length / pageSize)),
    }
  },
  async updateMedia(value: import('../types').MediaItem): Promise<void> {
    if (mockEnabled) { media = media.map((item) => item.id === value.id ? cloneValue(value) : item); return }
    await request(`/api/contents/demo/media/${value.id}`, { method: 'PATCH', body: JSON.stringify(value) })
  },
}
