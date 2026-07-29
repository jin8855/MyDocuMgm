import type { Category, ContentItem, ContentPage, MediaItem, SaveContent } from './types'

const categories: Category[] = [
  ['10000000-0000-0000-0000-000000000001', 'PLACE', '가볼곳'],
  ['10000000-0000-0000-0000-000000000002', 'COOKING', '요리'],
  ['10000000-0000-0000-0000-000000000003', 'EXERCISE', '운동'],
  ['10000000-0000-0000-0000-000000000004', 'CLEANING_LAUNDRY', '청소&세탁'],
  ['10000000-0000-0000-0000-000000000005', 'TRAVEL', '여행'],
  ['10000000-0000-0000-0000-000000000006', 'PHOTO', '사진'],
  ['10000000-0000-0000-0000-000000000007', 'STUDY', '공부'],
  ['10000000-0000-0000-0000-000000000008', 'PRODUCT', '제품'],
  ['10000000-0000-0000-0000-000000000009', 'PHONE_COMPUTER', '폰·컴퓨터'],
  ['10000000-0000-0000-0000-000000000010', 'TIP', '팁'],
  ['10000000-0000-0000-0000-000000000011', 'OTHER', '기타'],
].map(([id, code, displayName], index) => ({ id, code, displayName, sortOrder: index + 1 }))

let mockItems: ContentItem[] = [
  {
    id: '20000000-0000-0000-0000-000000000001',
    categoryId: categories[1].id,
    categoryCode: 'COOKING',
    categoryDisplayName: '요리',
    title: '바쁜 날의 토마토 파스타',
    shortSummary: '냄비 하나로 만드는 20분 저녁',
    detailContent: '면수와 토마토의 농도를 마지막에 맞춘다.',
    status: 'READY',
    visibility: 'PRIVATE',
    isFavorite: true,
    experienceStatus: 'TRIED',
    updatedAtUtc: '2026-07-29T07:30:00Z',
    createdAtUtc: '2026-07-28T07:30:00Z',
    rowVersion: 'AAAAAAAAB9E=',
    tags: ['빠른요리', '저녁'],
  },
]

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: init?.body instanceof FormData
      ? init.headers
      : { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { title?: string; detail?: string } | null
    throw new Error(problem?.detail ?? problem?.title ?? `요청 실패 (${response.status})`)
  }
  return response.status === 204 ? undefined as T : await response.json() as T
}

const isTest = import.meta.env.MODE === 'test'

export const api = {
  async categories(): Promise<Category[]> {
    return isTest ? categories : request('/api/categories')
  },
  async contents(search = '', categoryId = '', status = ''): Promise<ContentPage> {
    if (isTest) {
      const term = search.trim().toLocaleLowerCase('ko')
      const items = mockItems.filter((item) =>
        (!term || `${item.title} ${item.shortSummary}`.toLocaleLowerCase('ko').includes(term)) &&
        (!categoryId || item.categoryId === categoryId) &&
        (!status || item.status === status))
      return { items, totalCount: items.length, page: 1, pageSize: 30 }
    }
    const params = new URLSearchParams({ search, categoryId, status })
    return request(`/api/contents?${params}`)
  },
  async content(id: string): Promise<ContentItem> {
    if (isTest) {
      const item = mockItems.find((value) => value.id === id)
      if (!item) throw new Error('콘텐츠를 찾을 수 없습니다.')
      return structuredClone(item)
    }
    return request(`/api/contents/${id}`)
  },
  async save(value: SaveContent, id?: string): Promise<ContentItem> {
    if (isTest) {
      const category = categories.find((item) => item.id === value.categoryId)!
      const item: ContentItem = {
        ...value,
        id: id ?? crypto.randomUUID(),
        categoryCode: category.code,
        categoryDisplayName: category.displayName,
        shortSummary: value.shortSummary,
        detailContent: value.detailContent,
        updatedAtUtc: new Date().toISOString(),
        rowVersion: 'AAAAAAAAB9I=',
      }
      mockItems = id ? mockItems.map((old) => old.id === id ? item : old) : [item, ...mockItems]
      return structuredClone(item)
    }
    return request(id ? `/api/contents/${id}` : '/api/contents', {
      method: id ? 'PUT' : 'POST',
      body: JSON.stringify(value),
    })
  },
  async media(contentId: string): Promise<MediaItem[]> {
    return isTest ? [] : request(`/api/contents/${contentId}/media`)
  },
  async upload(contentId: string, file: File): Promise<MediaItem> {
    const form = new FormData()
    form.append('file', file)
    return request(`/api/contents/${contentId}/media`, { method: 'POST', body: form })
  },
}
