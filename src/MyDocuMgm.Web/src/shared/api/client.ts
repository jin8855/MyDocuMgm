import type {
  BlogDraft,
  BlogDraftPatch,
  Category,
  CategoryEdit,
  ContentItem,
  ContentPage,
  DetailStage,
  ExternalFetchApply,
  ExternalFetchAttempt,
  CookingIngredient,
  ImageStage,
  ImageStageMediaItem,
  MediaFilter,
  MediaItem,
  MediaPage,
  MediaSort,
  MediaUploadResult,
  LinkableMediaPage,
  OrphanMediaItem,
  PinnedAuthorCommentState,
  SearchAttribute,
  SearchQuery,
  UrlIntake,
  TrashContentItem,
  WorkflowStep,
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
const categoryEditValues = new Map<string, Record<string, Record<string, string | null>>>([[
  mockContentId,
  {
    COOKING: {
      servings: '2', preparationMinutes: '15', cookingMinutes: '20', difficulty: '보통',
    },
  },
]])

const deletedContentIds = new Set<string>()

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

const demoIntake: UrlIntake = {
  id: mockContentId,
  originalUrl: 'https://example.com/recipe/shrimp-toast',
  normalizedUrl: 'https://example.com/recipe/shrimp-toast',
  sourceKind: 'GENERIC',
  status: 'URL_ACCEPTED',
  isDuplicate: false,
  manualBody: null,
  manualBodyPresent: false,
  instagramContentType: null,
  manualCaption: null,
  pinnedAuthorCommentState: null,
  pinnedAuthorCommentText: null,
  sourceAcquisitionMode: null,
  linkedMediaIds: [],
}
const urlIntakes = new Map<string, UrlIntake>([[mockContentId, demoIntake]])
const imageStageLinks = new Map<string, string[]>([[mockContentId, []]])
const blogDrafts = new Map<string, { title: string; body: string; rowVersion: string }>()
const externalFetchAttempts = new Map<string, ExternalFetchAttempt[]>()
const externalBlogReuseConfirmations = new Set<string>()

function normalizeMockUrl(
  input: string,
): Pick<UrlIntake, 'originalUrl' | 'normalizedUrl' | 'sourceKind' | 'instagramContentType'> {
  const originalUrl = input.trim()
  if (!originalUrl) throw new ApiError('URL_REQUIRED', 'URL을 입력해 주세요.')
  let parsed: URL
  try {
    parsed = new URL(originalUrl)
  } catch {
    throw new ApiError('URL_INVALID', 'absolute http 또는 https URL을 입력해 주세요.')
  }
  if (!['http:', 'https:'].includes(parsed.protocol)) {
    throw new ApiError('URL_SCHEME_NOT_ALLOWED', 'http 또는 https URL만 입력할 수 있습니다.')
  }
  if (parsed.username || parsed.password) {
    throw new ApiError('URL_USER_INFO_NOT_ALLOWED', '사용자 정보가 포함된 URL은 입력할 수 없습니다.')
  }
  const instagramHosts = new Set(['instagram.com', 'www.instagram.com', 'm.instagram.com'])
  if (instagramHosts.has(parsed.hostname.toLowerCase())) {
    if (parsed.port) {
      throw new ApiError('INSTAGRAM_URL_NOT_SUPPORTED', '기본 포트를 사용하는 Instagram 콘텐츠 URL만 지원합니다.')
    }
    const match = parsed.pathname.match(/^\/(p|reel|tv)\/([A-Za-z0-9_-]+)\/?$/i)
    if (!match) {
      throw new ApiError('INSTAGRAM_URL_NOT_SUPPORTED', 'Instagram 게시물, 릴스 또는 TV 콘텐츠 URL만 지원합니다.')
    }
    return {
      originalUrl,
      normalizedUrl: `https://www.instagram.com/${match[1].toLowerCase()}/${match[2]}/`,
      sourceKind: 'INSTAGRAM',
      instagramContentType: match[1].toLowerCase() === 'p'
        ? 'POST'
        : match[1].toLowerCase() === 'reel' ? 'REEL' : null,
    }
  }
  parsed.hash = ''
  return {
    originalUrl,
    normalizedUrl: parsed.toString(),
    sourceKind: 'GENERIC',
    instagramContentType: null,
  }
}

function createMockUrlIntake(
  normalized: Pick<UrlIntake, 'originalUrl' | 'normalizedUrl' | 'sourceKind' | 'instagramContentType'>,
): UrlIntake {
  const duplicate = [...urlIntakes.values()].find(item => item.normalizedUrl === normalized.normalizedUrl)
  if (duplicate) return { ...cloneValue(duplicate), isDuplicate: true }
  const id = globalThis.crypto.randomUUID()
  const intake: UrlIntake = {
    id,
    ...normalized,
    status: 'URL_ACCEPTED',
    isDuplicate: false,
    manualBody: null,
    manualBodyPresent: false,
    manualCaption: null,
    pinnedAuthorCommentState: null,
    pinnedAuthorCommentText: null,
    sourceAcquisitionMode: null,
    linkedMediaIds: [],
  }
  urlIntakes.set(id, intake)
  const other = categories.find(category => category.code === 'OTHER')!
  contents.push({
    id,
    categoryId: other.id,
    categoryCode: other.code,
    categoryDisplayName: other.displayName,
    title: `URL 접수 · ${new URL(normalized.normalizedUrl).hostname}`,
    shortSummary: null,
    detailContent: null,
    status: 'INBOX',
    visibility: 'PRIVATE',
    isFavorite: false,
    experienceStatus: 'NONE',
    currentWorkflowStep: 'URL',
    blogDraftStatus: '미작성',
    updatedAtUtc: new Date().toISOString(),
    createdAtUtc: new Date().toISOString(),
    rowVersion,
    tags: [],
  })
  return cloneValue(intake)
}

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
  async createUrlIntake(url: string): Promise<UrlIntake> {
    if (!mockEnabled) return request('/api/url-intakes', { method: 'POST', body: JSON.stringify({ url }) })
    return createMockUrlIntake(normalizeMockUrl(url))
  },
  async createInstagramIntake(url: string): Promise<UrlIntake> {
    if (!mockEnabled) {
      return request('/api/url-intakes/instagram', { method: 'POST', body: JSON.stringify({ url }) })
    }
    const normalized = normalizeMockUrl(url)
    const originalHost = new URL(normalized.originalUrl).hostname.toLowerCase()
    if (!['instagram.com', 'www.instagram.com'].includes(originalHost) ||
        normalized.sourceKind !== 'INSTAGRAM') {
      throw new ApiError('INSTAGRAM_URL_REQUIRED', 'Instagram 게시물 또는 Reel URL만 입력할 수 있습니다.')
    }
    if (!normalized.instagramContentType) {
      throw new ApiError('INSTAGRAM_URL_NOT_SUPPORTED', 'Instagram 게시물 또는 Reel URL만 지원합니다.')
    }
    return createMockUrlIntake(normalized)
  },
  async urlIntake(contentId: string): Promise<UrlIntake> {
    if (!mockEnabled) return request(`/api/url-intakes/${encodeURIComponent(contentId)}`)
    const intake = urlIntakes.get(contentId)
    if (!intake) throw new ApiError('URL_INTAKE_NOT_FOUND', '이 콘텐츠에는 URL 접수 정보가 없습니다.')
    return cloneValue(intake)
  },
  async startExternalFetch(contentId: string): Promise<ExternalFetchAttempt> {
    if (!mockEnabled) {
      return request('/api/url-intakes/' + encodeURIComponent(contentId) + '/external-fetches', {
        method: 'POST',
      })
    }
    const intake = await this.urlIntake(contentId)
    if (intake.sourceKind !== 'GENERIC') {
      throw new ApiError('EXTERNAL_FETCH_GENERIC_URL_REQUIRED', '일반 공개 URL만 가져올 수 있습니다.')
    }
      const previous = externalFetchAttempts.get(contentId) ?? []
      if (previous.length >= 3) {
        throw new ApiError(
          'FETCH_RETRY_LIMIT',
          '10분 동안 같은 자료에서 최대 3회까지 가져올 수 있습니다.',
        )
      }
      const now = new Date().toISOString()
      const syntheticFailure = previous.length === 0 &&
        new URL(intake.normalizedUrl).pathname.includes('mock-fetch-failure')
      const attempt: ExternalFetchAttempt = {
      id: crypto.randomUUID(),
      contentId,
      attemptNumber: previous.length + 1,
        status: syntheticFailure ? 'FAILED' : 'SUCCEEDED',
      finalUrl: intake.normalizedUrl,
      httpStatusCode: 200,
      responseMimeType: 'text/html',
      responseBytes: 512,
      contentSha256: 'MOCK_PHASE2C_SHA256',
      eTag: null,
      lastModifiedAtUtc: null,
      title: '가져온 문서 - ' + new URL(intake.normalizedUrl).hostname,
      description: '로컬 테스트용 Phase 2C 합성 미리보기입니다.',
      authorName: null,
      publishedAtUtc: null,
      body: '외부 요청 없이 생성된 합성 HTML 추출 결과입니다.',
        errorCode: syntheticFailure ? 'FETCH_REMOTE_STATUS' : null,
        errorMessage: syntheticFailure ? '원격 문서를 가져오지 못했습니다. 잠시 후 다시 시도하세요.' : null,
      startedAtUtc: now,
      completedAtUtc: now,
      }
      externalFetchAttempts.set(contentId, [...previous, attempt])
      if (syntheticFailure) {
        throw new ApiError(attempt.errorCode!, attempt.errorMessage!)
      }
      return cloneValue(attempt)
  },
  async latestExternalFetch(contentId: string): Promise<ExternalFetchAttempt> {
    if (!mockEnabled) {
      return request('/api/url-intakes/' + encodeURIComponent(contentId) + '/external-fetches/latest')
    }
    const attempt = externalFetchAttempts.get(contentId)?.at(-1)
    if (!attempt) throw new ApiError('NOT_FOUND', '가져오기 기록을 찾을 수 없습니다.')
    return cloneValue(attempt)
  },
  async applyExternalFetch(
    contentId: string,
    attemptId: string,
    title: string,
    description: string,
    body: string,
  ): Promise<ExternalFetchApply> {
    if (!mockEnabled) {
      return request(
        '/api/url-intakes/' + encodeURIComponent(contentId) +
          '/external-fetches/' + encodeURIComponent(attemptId) + '/apply',
        {
          method: 'PUT',
          body: JSON.stringify({ title, description, body }),
        },
      )
    }
    const attempts = externalFetchAttempts.get(contentId) ?? []
    const attempt = attempts.find(value => value.id === attemptId)
    if (!attempt) throw new ApiError('NOT_FOUND', '가져오기 기록을 찾을 수 없습니다.')
    if (!['SUCCEEDED', 'APPLIED'].includes(attempt.status)) {
      throw new ApiError('EXTERNAL_FETCH_NOT_APPLICABLE', '성공한 미리보기만 적용할 수 있습니다.')
    }
    const intake = await this.urlIntake(contentId)
    intake.manualBody = body.trim()
    intake.manualBodyPresent = true
    intake.status = 'CONTENT_READY'
    intake.sourceAcquisitionMode = 'HTTP_METADATA'
    urlIntakes.set(contentId, intake)
    attempt.status = 'APPLIED'
    const item = contents.find(value => value.id === contentId)
    if (item) {
      item.title = title.trim() || item.title
      item.shortSummary = description.trim() || null
      item.detailContent = body.trim()
    }
    return cloneValue({ attempt, intake })
  },
  async beginManualInput(contentId: string): Promise<UrlIntake> {
    if (!mockEnabled) return request(`/api/url-intakes/${encodeURIComponent(contentId)}/manual-input`, { method: 'POST' })
    const intake = await this.urlIntake(contentId)
    intake.status = intake.status === 'CONTENT_READY' ? 'CONTENT_READY' : 'MANUAL_INPUT_REQUIRED'
    urlIntakes.set(contentId, intake)
    return cloneValue(intake)
  },
  async saveManualBody(contentId: string, body: string): Promise<UrlIntake> {
    if (!mockEnabled) return request(`/api/url-intakes/${encodeURIComponent(contentId)}/manual-body`, {
      method: 'PUT', body: JSON.stringify({ body }),
    })
    if (!body.trim()) throw new ApiError('MANUAL_BODY_REQUIRED', '본문을 입력해 주세요.')
    if (body.trim().length > 20_000) throw new ApiError('MANUAL_BODY_TOO_LONG', '본문은 20,000자 이하여야 합니다.')
    const intake = await this.urlIntake(contentId)
    intake.manualBody = body.trim()
    intake.manualBodyPresent = true
    intake.status = 'CONTENT_READY'
    intake.sourceAcquisitionMode = 'MANUAL'
    urlIntakes.set(contentId, intake)
    return cloneValue(intake)
  },
  async saveManualInstagram(
    contentId: string,
    caption: string,
    pinnedAuthorCommentState: PinnedAuthorCommentState,
    pinnedAuthorCommentText: string,
    mediaIds: string[],
  ): Promise<UrlIntake> {
    if (!mockEnabled) {
      return request(`/api/url-intakes/${encodeURIComponent(contentId)}/manual-instagram`, {
        method: 'PUT',
        body: JSON.stringify({
          caption,
          pinnedAuthorCommentState,
          pinnedAuthorCommentText,
          mediaIds,
        }),
      })
    }
    const trimmedCaption = caption.trim()
    const trimmedComment = pinnedAuthorCommentText.trim()
    if (!trimmedCaption) {
      throw new ApiError('MANUAL_CAPTION_REQUIRED', 'Caption을 직접 입력해 주세요.')
    }
    if (pinnedAuthorCommentState === 'PRESENT' && !trimmedComment) {
      throw new ApiError('PINNED_AUTHOR_COMMENT_REQUIRED', '작성자가 작성한 고정 댓글 본문을 입력해 주세요.')
    }
    if (mediaIds.some(id => !media.some(item =>
      item.id === id && !item.isDeleted && item.storageStatus === 'READY'))) {
      throw new ApiError('MEDIA_LINK_NOT_FOUND', '연결할 수 없는 이미지가 포함되어 있습니다.')
    }
    const intake = await this.urlIntake(contentId)
    if (intake.sourceKind !== 'INSTAGRAM' || !intake.instagramContentType) {
      throw new ApiError('INSTAGRAM_INTAKE_REQUIRED', 'Instagram 게시물 또는 Reel 접수 정보가 필요합니다.')
    }
    intake.manualCaption = trimmedCaption
    intake.pinnedAuthorCommentState = pinnedAuthorCommentState
    intake.pinnedAuthorCommentText = pinnedAuthorCommentState === 'NONE' ? null : trimmedComment
    intake.sourceAcquisitionMode = 'MANUAL'
    intake.linkedMediaIds = [...new Set(mediaIds)]
    imageStageLinks.set(contentId, [...intake.linkedMediaIds])
    intake.status = 'CONTENT_READY'
    urlIntakes.set(contentId, intake)
    return cloneValue(intake)
  },
  async linkableMedia(page = 1, pageSize = 24): Promise<LinkableMediaPage> {
    if (!mockEnabled) return request(`/api/url-intakes/media-library?page=${page}&pageSize=${pageSize}`)
    const start = (page - 1) * pageSize
    const available = media.filter(item => !item.isDeleted && item.storageStatus === 'READY')
    const items = available
      .slice(start, start + pageSize)
      .map(item => ({
        id: item.id,
        ownerContentId: mockContentId,
        originalFileName: item.originalFileName,
        thumbnailUrl: item.thumbnailUrl,
        mimeType: item.mimeType,
        sizeBytes: item.sizeBytes,
        width: item.width,
        height: item.height,
      }))
    return {
      items: cloneValue(items),
      totalCount: available.length,
      page,
      pageSize,
      totalPages: Math.max(1, Math.ceil(available.length / pageSize)),
    }
  },
  async replaceLinkedMedia(contentId: string, mediaIds: string[]): Promise<UrlIntake> {
    if (!mockEnabled) return request(`/api/url-intakes/${encodeURIComponent(contentId)}/media-links`, {
      method: 'PUT', body: JSON.stringify({ mediaIds }),
    })
    if (mediaIds.some(id => !media.some(item =>
      item.id === id && !item.isDeleted && item.storageStatus === 'READY'))) {
      throw new ApiError('MEDIA_LINK_NOT_FOUND', '연결할 수 없는 이미지가 포함되어 있습니다.')
    }
    const intake = await this.urlIntake(contentId)
    intake.linkedMediaIds = [...new Set(mediaIds)]
    imageStageLinks.set(contentId, [...intake.linkedMediaIds])
    urlIntakes.set(contentId, intake)
    return cloneValue(intake)
  },
  async imageStage(contentId: string): Promise<ImageStage> {
    if (!mockEnabled) return request(`${contentPath(contentId)}/image-stage`)
    const item = contents.find(value => value.id === contentId)
    if (!item) throw new ApiError('NOT_FOUND', '콘텐츠를 찾을 수 없습니다.')
    const step = resolveWorkflowStep(item.currentWorkflowStep) as WorkflowStep | undefined
    if (!step || workflowSequence.indexOf(step) < workflowSequence.indexOf('MEDIA')) {
      throw new ApiError('IMAGE_STAGE_NOT_AVAILABLE', '분류별 편집을 완료한 뒤 이미지 단계를 진행해 주세요.')
    }
    const linkedMediaIds = imageStageLinks.get(contentId)
      ?? urlIntakes.get(contentId)?.linkedMediaIds
      ?? []
    imageStageLinks.set(contentId, [...linkedMediaIds])
    const linkedMedia = linkedMediaIds.flatMap((id): ImageStageMediaItem[] => {
      const value = media.find(candidate =>
        candidate.id === id && !candidate.isDeleted && candidate.storageStatus === 'READY')
      return value ? [{
        id: value.id,
        ownerContentId: mockContentId,
        originalFileName: value.originalFileName,
        thumbnailUrl: value.thumbnailUrl,
        mimeType: value.mimeType,
        sizeBytes: value.sizeBytes,
        width: value.width,
        height: value.height,
      }] : []
    })
    return cloneValue({
      contentId,
      currentWorkflowStep: item.currentWorkflowStep,
      rowVersion: item.rowVersion,
      linkedMediaIds: linkedMedia.map(value => value.id),
      linkedMedia,
    })
  },
  async saveImageStage(
    contentId: string,
    mediaIds: string[],
    complete: boolean,
    currentRowVersion: string,
  ): Promise<ImageStage> {
    if (!mockEnabled) return request(`${contentPath(contentId)}/image-stage`, {
      method: 'PUT',
      body: JSON.stringify({ mediaIds, complete, rowVersion: currentRowVersion }),
    })
    const item = contents.find(value => value.id === contentId)
    if (!item) throw new ApiError('NOT_FOUND', '콘텐츠를 찾을 수 없습니다.')
    const currentStep = resolveWorkflowStep(item.currentWorkflowStep) as WorkflowStep | undefined
    if (currentStep !== 'MEDIA') {
      throw new ApiError(
        !currentStep || workflowSequence.indexOf(currentStep) < workflowSequence.indexOf('MEDIA')
          ? 'IMAGE_STAGE_NOT_AVAILABLE' : 'IMAGE_STAGE_ALREADY_COMPLETED',
        '현재 콘텐츠에서 이미지 단계를 저장할 수 없습니다.',
      )
    }
    const existingIds = new Set(imageStageLinks.get(contentId) ?? urlIntakes.get(contentId)?.linkedMediaIds ?? [])
    const requestedIds = [...new Set(mediaIds)]
    const unavailable = requestedIds.some(id => !existingIds.has(id) && !(
      contentId === mockContentId && media.some(value =>
        value.id === id && !value.isDeleted && value.storageStatus === 'READY')
    ))
    if (unavailable) {
      throw new ApiError('IMAGE_MEDIA_NOT_AVAILABLE', '현재 콘텐츠에서 선택할 수 없는 이미지가 포함되어 있습니다.')
    }
    imageStageLinks.set(contentId, requestedIds)
    const intake = urlIntakes.get(contentId)
    if (intake) intake.linkedMediaIds = [...requestedIds]
    const updated = { ...item, currentWorkflowStep: complete ? 'DETAIL' as const : 'MEDIA' as const }
    contents = contents.map(value => value.id === contentId ? updated : value)
    return this.imageStage(contentId)
  },
  async detailStage(contentId: string): Promise<DetailStage> {
    if (!mockEnabled) return request(`${contentPath(contentId)}/detail-stage`)
    const item = contents.find(value => value.id === contentId)
    if (!item) throw new ApiError('NOT_FOUND', '콘텐츠를 찾을 수 없습니다.')
    const step = resolveWorkflowStep(item.currentWorkflowStep) as WorkflowStep | undefined
    if (!step || workflowSequence.indexOf(step) < workflowSequence.indexOf('DETAIL')) {
      throw new ApiError('DETAIL_STAGE_NOT_AVAILABLE', '이미지 단계를 완료한 뒤 자료 상세를 검토해 주세요.')
    }
    const intake = urlIntakes.get(contentId)
    const linkedMediaIds = imageStageLinks.get(contentId) ?? intake?.linkedMediaIds ?? []
    const linkedMedia = linkedMediaIds.flatMap(id => {
      const value = media.find(candidate =>
        candidate.id === id && !candidate.isDeleted && candidate.storageStatus === 'READY')
      return value ? [{
        id: value.id,
        ownerContentId: mockContentId,
        originalFileName: value.originalFileName,
        thumbnailUrl: value.thumbnailUrl,
        mimeType: value.mimeType,
        sizeBytes: value.sizeBytes,
        width: value.width,
        height: value.height,
      }] : []
    })
    const values = categoryEditValues.get(contentId)?.[item.categoryCode] ?? {}
    const detailIngredients = item.categoryCode === 'COOKING' && contentId === mockContentId
      ? ingredients.map(value => ({
        id: value.id,
        sortOrder: value.sortOrder,
        name: value.name,
        quantity: value.quantity || null,
        ingredientType: value.ingredientType,
        isPrimary: value.isPrimary,
        note: value.note || null,
      }))
      : []
    return cloneValue({
      contentId,
      currentWorkflowStep: item.currentWorkflowStep,
      rowVersion: item.rowVersion,
      title: item.title,
      shortSummary: item.shortSummary,
      originalUrl: intake?.originalUrl ?? null,
      normalizedUrl: intake?.normalizedUrl ?? null,
      sourceKind: intake?.sourceKind ?? null,
      instagramContentType: intake?.instagramContentType ?? null,
      manualCaption: intake?.manualCaption ?? null,
      pinnedAuthorCommentState: intake?.pinnedAuthorCommentState ?? null,
      pinnedAuthorCommentText: intake?.pinnedAuthorCommentText ?? null,
      manualBody: intake?.manualBody ?? item.detailContent,
      categoryId: item.categoryId,
      categoryCode: item.categoryCode,
      categoryDisplayName: item.categoryDisplayName,
      categoryValues: values,
      ingredients: detailIngredients,
      linkedMedia,
      editableFields: [],
    })
  },
  async saveDetailStage(
    contentId: string,
    values: Record<string, string | null>,
    complete: boolean,
    currentRowVersion: string,
  ): Promise<DetailStage> {
    if (!mockEnabled) return request(`${contentPath(contentId)}/detail-stage`, {
      method: 'PUT', body: JSON.stringify({ values, complete, rowVersion: currentRowVersion }),
    })
    const item = contents.find(value => value.id === contentId)
    if (!item) throw new ApiError('NOT_FOUND', '콘텐츠를 찾을 수 없습니다.')
    const currentStep = resolveWorkflowStep(item.currentWorkflowStep) as WorkflowStep | undefined
    if (currentStep !== 'DETAIL') {
      throw new ApiError(
        !currentStep || workflowSequence.indexOf(currentStep) < workflowSequence.indexOf('DETAIL')
          ? 'DETAIL_STAGE_NOT_AVAILABLE' : 'DETAIL_STAGE_ALREADY_COMPLETED',
        '현재 콘텐츠에서는 자료 상세를 저장할 수 없습니다.',
      )
    }
    if (item.rowVersion !== currentRowVersion) {
      throw new ApiError('CONCURRENCY_CONFLICT', '다른 변경이 먼저 저장되었습니다.')
    }
    if (Object.keys(values).length > 0) {
      throw new ApiError('DETAIL_FIELD_NOT_EDITABLE', '현재 자료 상세 단계에는 별도로 편집하도록 승인된 필드가 없습니다.')
    }
    if (complete && !item.title.trim()) {
      throw new ApiError('DETAIL_REQUIRED_DATA_MISSING', '제목이 없는 콘텐츠는 자료 상세 검토를 완료할 수 없습니다.')
    }
    if (!complete) return this.detailStage(contentId)
    const updated: ContentItem = {
      ...item,
      currentWorkflowStep: complete ? 'BLOG_DRAFT' : 'DETAIL',
      updatedAtUtc: new Date().toISOString(),
    }
    contents = contents.map(value => value.id === contentId ? updated : value)
    return this.detailStage(contentId)
  },
  async blogDraft(contentId: string): Promise<BlogDraft> {
    if (!mockEnabled) return request(`${contentPath(contentId)}/blog-draft`)
    const item = contents.find(value => value.id === contentId)
    if (!item) throw new ApiError('NOT_FOUND', '콘텐츠를 찾을 수 없습니다.')
    const currentStep = resolveWorkflowStep(item.currentWorkflowStep) as WorkflowStep | undefined
    if (currentStep !== 'BLOG_DRAFT') {
      throw new ApiError(
        !currentStep || workflowSequence.indexOf(currentStep) < workflowSequence.indexOf('BLOG_DRAFT')
          ? 'BLOG_DRAFT_NOT_AVAILABLE' : 'BLOG_DRAFT_ALREADY_COMPLETED',
        '현재 콘텐츠에서는 블로그 초안을 불러올 수 없습니다.',
      )
    }
    return mapMockBlogDraft(contentId, item)
  },
  async completion(contentId: string): Promise<BlogDraft> {
    if (!mockEnabled) return request(`${contentPath(contentId)}/completion`)
    const item = contents.find(value => value.id === contentId)
    if (!item) throw new ApiError('NOT_FOUND', '콘텐츠를 찾을 수 없습니다.')
    if (resolveWorkflowStep(item.currentWorkflowStep) !== 'COMPLETED') {
      throw new ApiError('COMPLETION_NOT_AVAILABLE', '완료된 콘텐츠만 완료 화면을 조회할 수 있습니다.')
    }
    if (!blogDrafts.has(contentId)) {
      throw new ApiError('COMPLETION_BLOG_DRAFT_MISSING', '완료된 콘텐츠의 저장된 블로그 초안을 찾을 수 없습니다.')
    }
    return mapMockBlogDraft(contentId, item)
  },
  async saveBlogDraft(
    contentId: string,
    values: BlogDraftPatch,
    complete: boolean,
    contentRowVersion: string,
    draftRowVersion: string | null,
    confirmExternalSourceReuse = false,
  ): Promise<BlogDraft> {
    if (!mockEnabled) return request(`${contentPath(contentId)}/blog-draft`, {
      method: 'PUT',
      body: JSON.stringify({ ...values, complete, contentRowVersion, draftRowVersion, confirmExternalSourceReuse }),
    })
    const item = contents.find(value => value.id === contentId)
    if (!item) throw new ApiError('NOT_FOUND', '콘텐츠를 찾을 수 없습니다.')
    const currentStep = resolveWorkflowStep(item.currentWorkflowStep) as WorkflowStep | undefined
    const existing = blogDrafts.get(contentId)
    if (currentStep === 'COMPLETED' && complete && existing) {
      if (values.title === undefined || values.body === undefined) {
        throw new ApiError('BLOG_DRAFT_ALREADY_COMPLETED', '완료 응답 재확인에는 기존 요청의 제목과 본문이 필요합니다.')
      }
      const retryTitle = values.title ?? ''
      const retryBody = values.body ?? ''
      if (retryTitle === existing.title && retryBody === existing.body) {
        return mapMockBlogDraft(contentId, item)
      }
      throw new ApiError('BLOG_DRAFT_ALREADY_COMPLETED', '이미 완료된 블로그 초안은 변경할 수 없습니다.')
    }
    if (currentStep !== 'BLOG_DRAFT') {
      throw new ApiError(
        !currentStep || workflowSequence.indexOf(currentStep) < workflowSequence.indexOf('BLOG_DRAFT')
          ? 'BLOG_DRAFT_NOT_AVAILABLE' : 'BLOG_DRAFT_ALREADY_COMPLETED',
        '현재 콘텐츠에서는 블로그 초안을 저장할 수 없습니다.',
      )
    }
    if (item.rowVersion !== contentRowVersion) {
      throw new ApiError('CONCURRENCY_CONFLICT', '다른 변경이 먼저 저장되었습니다.')
    }
    if (existing && existing.rowVersion !== draftRowVersion) {
      throw new ApiError('CONCURRENCY_CONFLICT', '다른 초안 변경이 먼저 저장되었습니다.')
    }
    const intake = urlIntakes.get(contentId)
    if (intake?.sourceAcquisitionMode === 'HTTP_METADATA' &&
        !externalBlogReuseConfirmations.has(contentId)) {
      if (!confirmExternalSourceReuse) {
        throw new ApiError(
          'BLOG_DRAFT_SOURCE_REUSE_CONFIRMATION_REQUIRED',
          '외부 출처의 내용을 블로그 초안에 재사용하려면 권리 확인이 필요합니다.',
        )
      }
      externalBlogReuseConfirmations.add(contentId)
    }
    const title = values.title === undefined ? existing?.title ?? item.title : values.title ?? ''
    const body = values.body === undefined
      ? existing?.body ?? intake?.manualBody ?? item.detailContent ?? intake?.manualCaption ?? item.shortSummary ?? ''
      : values.body ?? ''
    if (title.length > 200) throw new ApiError('BLOG_DRAFT_TITLE_TOO_LONG', '초안 제목은 200자 이하여야 합니다.')
    if (body.length > 20_000) throw new ApiError('BLOG_DRAFT_BODY_TOO_LONG', '초안 본문은 20,000자 이하여야 합니다.')
    if (complete && !title.trim()) throw new ApiError('BLOG_DRAFT_TITLE_REQUIRED', '완료하려면 초안 제목을 입력해 주세요.')
    if (complete && !body.trim()) throw new ApiError('BLOG_DRAFT_BODY_REQUIRED', '완료하려면 초안 본문을 입력해 주세요.')
    blogDrafts.set(contentId, { title, body, rowVersion })
    if (complete) {
      const updated: ContentItem = {
        ...item,
        currentWorkflowStep: 'COMPLETED',
        updatedAtUtc: new Date().toISOString(),
      }
      contents = contents.map(value => value.id === contentId ? updated : value)
      return mapMockBlogDraft(contentId, updated)
    }
    return mapMockBlogDraft(contentId, item)
  },
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
    let result = contents.filter(item => !deletedContentIds.has(item.id))
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
  async saveAnalysisReview(
    contentId: string,
    title: string,
    shortSummary: string,
    complete: boolean,
    currentRowVersion: string,
  ): Promise<ContentItem> {
    if (!mockEnabled) {
      return request(`${contentPath(contentId)}/analysis-review`, {
        method: 'PUT',
        body: JSON.stringify({ title, shortSummary, complete, rowVersion: currentRowVersion }),
      })
    }

    const item = contents.find(value => value.id === contentId)
    if (!item) throw new ApiError('NOT_FOUND', '콘텐츠를 찾을 수 없습니다.')
    const intake = urlIntakes.get(contentId)
    const commentIsValid = intake?.pinnedAuthorCommentState === 'NONE'
      ? !intake.pinnedAuthorCommentText
      : intake?.pinnedAuthorCommentState === 'PRESENT' && Boolean(intake.pinnedAuthorCommentText?.trim())
    const instagramReady = intake?.sourceKind === 'INSTAGRAM' &&
      Boolean(intake.instagramContentType) &&
      intake.status === 'CONTENT_READY' &&
      intake.sourceAcquisitionMode === 'MANUAL' &&
      Boolean(intake.manualCaption?.trim()) &&
      commentIsValid
    const genericReady = intake?.sourceKind === 'GENERIC' &&
      intake.status === 'CONTENT_READY' &&
      (intake.sourceAcquisitionMode === 'MANUAL' ||
        intake.sourceAcquisitionMode === 'HTTP_METADATA') &&
      Boolean(intake.manualBody?.trim())
    if (!instagramReady && !genericReady) {
      const instagramIncomplete = intake?.sourceKind === 'INSTAGRAM'
      throw new ApiError(
        instagramIncomplete ? 'MANUAL_INSTAGRAM_INTAKE_NOT_READY' : 'URL_INTAKE_NOT_READY',
        instagramIncomplete
          ? '수동 Instagram 접수를 완료한 뒤 분석 검토를 진행해 주세요.'
          : 'URL 콘텐츠를 준비한 뒤 분석 검토를 진행해 주세요.',
      )
    }
    const trimmedTitle = title.trim()
    if (!trimmedTitle || trimmedTitle.length > 200) {
      throw new ApiError('INVALID_TITLE', '제목은 1~200자여야 합니다.')
    }
    if (shortSummary.length > 500) {
      throw new ApiError('ANALYSIS_SUMMARY_TOO_LONG', '요약은 500자 이하여야 합니다.')
    }
    const currentStep = resolveWorkflowStep(item.currentWorkflowStep) as WorkflowStep | undefined
    if (currentStep !== 'URL' && currentStep !== 'ANALYSIS_REVIEW') {
      throw new ApiError('ANALYSIS_REVIEW_ALREADY_COMPLETED', '분석 검토를 완료한 콘텐츠입니다.')
    }
    const updated: ContentItem = {
      ...item,
      title: trimmedTitle,
      shortSummary: shortSummary.trim() || null,
      currentWorkflowStep: complete ? 'CATEGORY_EDIT' : 'ANALYSIS_REVIEW',
      updatedAtUtc: new Date().toISOString(),
    }
    contents = contents.map(value => value.id === contentId ? updated : value)
    return cloneValue(updated)
  },
  async categoryEdit(contentId: string): Promise<CategoryEdit> {
    if (!mockEnabled) return request(`${contentPath(contentId)}/category-edit`)
    const item = contents.find(value => value.id === contentId)
    if (!item) throw new ApiError('NOT_FOUND', '콘텐츠를 찾을 수 없습니다.')
    const step = resolveWorkflowStep(item.currentWorkflowStep) as WorkflowStep | undefined
    if (!step || workflowSequence.indexOf(step) < workflowSequence.indexOf('CATEGORY_EDIT')) {
      throw new ApiError('CATEGORY_EDIT_NOT_AVAILABLE', '분석 검토를 완료한 뒤 분류별 편집을 진행해 주세요.')
    }
    return cloneValue({
      contentId: item.id, title: item.title, shortSummary: item.shortSummary,
      categoryId: item.categoryId, categoryCode: item.categoryCode,
      currentWorkflowStep: item.currentWorkflowStep, rowVersion: item.rowVersion,
      valuesByCategory: categoryEditValues.get(contentId) ?? {},
    })
  },
  async saveCategoryEdit(
    contentId: string,
    categoryId: string,
    values: Record<string, string | null>,
    complete: boolean,
    currentRowVersion: string,
  ): Promise<CategoryEdit> {
    if (!mockEnabled) return request(`${contentPath(contentId)}/category-edit`, {
      method: 'PUT', body: JSON.stringify({ categoryId, values, complete, rowVersion: currentRowVersion }),
    })
    const item = contents.find(value => value.id === contentId)
    if (!item) throw new ApiError('NOT_FOUND', '콘텐츠를 찾을 수 없습니다.')
    const currentStep = resolveWorkflowStep(item.currentWorkflowStep) as WorkflowStep | undefined
    if (currentStep !== 'CATEGORY_EDIT') {
      throw new ApiError(
        !currentStep || workflowSequence.indexOf(currentStep) < workflowSequence.indexOf('CATEGORY_EDIT')
          ? 'CATEGORY_EDIT_NOT_AVAILABLE' : 'CATEGORY_EDIT_ALREADY_COMPLETED',
        '현재 콘텐츠에서 분류별 편집을 저장할 수 없습니다.',
      )
    }
    const category = categories.find(value => value.id === categoryId)
    if (!category) throw new ApiError('CATEGORY_REQUIRED', '분류를 선택해 주세요.')
    const stored = categoryEditValues.get(contentId) ?? {}
    stored[category.code] = cloneValue(values)
    categoryEditValues.set(contentId, stored)
    const updated: ContentItem = {
      ...item,
      categoryId: category.id,
      categoryCode: category.code,
      categoryDisplayName: category.displayName,
      currentWorkflowStep: complete ? 'MEDIA' : 'CATEGORY_EDIT',
      updatedAtUtc: new Date().toISOString(),
    }
    contents = contents.map(value => value.id === contentId ? updated : value)
    return this.categoryEdit(contentId)
  },
  async softDeleteContent(id: string, currentRowVersion: string): Promise<void> {
    if (mockEnabled) {
      if (!contents.some(item => item.id === id)) throw new ApiError('NOT_FOUND', '콘텐츠를 찾을 수 없습니다.')
      deletedContentIds.add(id)
      return
    }
    return request(`${contentPath(id)}?rowVersion=${encodeURIComponent(currentRowVersion)}`, { method: 'DELETE' })
  },
  async trash(): Promise<TrashContentItem[]> {
    if (!mockEnabled) return request('/api/cleanup/trash')
    return contents.filter(item => deletedContentIds.has(item.id)).map(item => ({
      id: item.id,
      title: item.title,
      deletedAtUtc: new Date().toISOString(),
      ownedMediaCount: item.id === mockContentId ? media.length : 0,
      rowVersion: item.rowVersion,
    }))
  },
  async permanentlyDeleteContent(id: string): Promise<void> {
    if (!mockEnabled) return request(`/api/cleanup/trash/${encodeURIComponent(id)}`, { method: 'DELETE' })
    if (!deletedContentIds.has(id)) throw new ApiError('CONTENT_NOT_SOFT_DELETED', '휴지통 콘텐츠만 영구 삭제할 수 있습니다.')
    if (id === mockContentId && media.length > 0) throw new ApiError('CONTENT_HAS_OWNED_MEDIA', '소유 미디어를 먼저 삭제해 주세요.')
    contents = contents.filter(item => item.id !== id)
    deletedContentIds.delete(id)
  },
  async orphanMedia(): Promise<OrphanMediaItem[]> {
    if (!mockEnabled) return request('/api/cleanup/orphan-media')
    const linked = new Set([...urlIntakes.values()].flatMap(item => item.linkedMediaIds).concat(...imageStageLinks.values()))
    return media.filter(item => !linked.has(item.id)).map(item => ({
      id: item.id,
      contentId: mockContentId,
      originalFileName: item.originalFileName,
      linkCount: 0,
      fileExists: true,
      fileState: 'MEDIA_FILE_READY_FOR_CLEANUP',
      rowVersion: item.rowVersion,
    }))
  },
  async permanentlyDeleteOrphanMedia(id: string): Promise<void> {
    if (!mockEnabled) return request(`/api/cleanup/orphan-media/${encodeURIComponent(id)}`, { method: 'DELETE' })
    if ([...urlIntakes.values()].some(item => item.linkedMediaIds.includes(id)) ||
        [...imageStageLinks.values()].some(mediaIds => mediaIds.includes(id))) {
      throw new ApiError('MEDIA_STILL_REFERENCED', '연결된 미디어는 삭제할 수 없습니다.')
    }
    media = media.filter(item => item.id !== id)
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

function mapMockBlogDraft(contentId: string, item: ContentItem): BlogDraft {
  const intake = urlIntakes.get(contentId)
  const draft = blogDrafts.get(contentId)
  const linkedIds = imageStageLinks.get(contentId) ?? intake?.linkedMediaIds ?? []
  const linkedMedia = linkedIds.flatMap(id => {
    const value = media.find(candidate =>
      candidate.id === id && !candidate.isDeleted && candidate.storageStatus === 'READY')
    return value ? [{
      id: value.id,
      ownerContentId: contentId,
      originalFileName: value.originalFileName,
      thumbnailUrl: value.thumbnailUrl,
      mimeType: value.mimeType,
      width: value.width,
      height: value.height,
    }] : []
  })
  return cloneValue({
    contentId,
    currentWorkflowStep: item.currentWorkflowStep,
    contentRowVersion: item.rowVersion,
    hasSavedDraft: Boolean(draft),
    draftRowVersion: draft?.rowVersion ?? null,
    title: draft?.title ?? item.title,
    body: draft?.body ?? intake?.manualBody ?? item.detailContent ?? intake?.manualCaption ?? item.shortSummary ?? '',
    titleMaxLength: 200,
    bodyMaxLength: 20_000,
    analysisTitle: item.title,
    shortSummary: item.shortSummary,
    requiresExternalSourceReuseConfirmation: intake?.sourceAcquisitionMode === 'HTTP_METADATA',
    externalSourceReuseConfirmed: intake?.sourceAcquisitionMode !== 'HTTP_METADATA' || externalBlogReuseConfirmations.has(contentId),
    categoryDisplayName: item.categoryDisplayName,
    linkedMedia,
  })
}
