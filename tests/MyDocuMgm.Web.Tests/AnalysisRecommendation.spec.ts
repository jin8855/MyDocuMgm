import { createApp, nextTick } from 'vue'
import AnalysisReviewStage from '../../src/MyDocuMgm.Web/src/features/analysis-review/AnalysisReviewStage.vue'
import { ApiError, api } from '../../src/MyDocuMgm.Web/src/shared/api/client'
import type { AnalysisRecommendationRun, Category, ContentItem, UrlIntake } from '../../src/MyDocuMgm.Web/src/shared/types'

async function flush() {
  await Promise.resolve()
  await new Promise(resolve => setTimeout(resolve, 0))
  await nextTick()
}

function clickByText(host: HTMLElement, text: string, scope: ParentNode = host) {
  const button = [...scope.querySelectorAll<HTMLButtonElement>('button')]
    .find(value => value.textContent?.trim() === text)
  expect(button).toBeDefined()
  button!.click()
}

function setValue(element: HTMLInputElement | HTMLTextAreaElement, value: string) {
  element.value = value
  element.dispatchEvent(new Event('input', { bubbles: true }))
}

const category: Category = {
  id: '10000000-0000-0000-0000-000000000011', code: 'OTHER', displayName: '기타', sortOrder: 11,
  isActive: true, rowVersion: 'AQID',
}

function content(): ContentItem {
  return {
    id: crypto.randomUUID(), categoryId: category.id, categoryCode: category.code, categoryDisplayName: category.displayName,
    title: '현재 제목', shortSummary: '현재 요약', detailContent: '현재 본문', status: 'REVIEW_REQUIRED', visibility: 'PRIVATE',
    isFavorite: false, experienceStatus: 'NONE', currentWorkflowStep: 'ANALYSIS_REVIEW', blogDraftStatus: '미작성',
    updatedAtUtc: new Date().toISOString(), createdAtUtc: new Date().toISOString(), rowVersion: 'AQID', tags: ['기존태그'],
  }
}

function intake(id: string): UrlIntake {
  return {
    id, originalUrl: 'https://example.test/current', normalizedUrl: 'https://example.test/current', sourceKind: 'GENERIC',
    status: 'CONTENT_READY', isDuplicate: false, manualBody: '현재 본문', manualBodyPresent: true,
    instagramContentType: null, manualCaption: null, pinnedAuthorCommentState: null, pinnedAuthorCommentText: null,
    sourceAcquisitionMode: 'MANUAL', linkedMediaIds: [],
  }
}

function recommendation(contentId: string, status: AnalysisRecommendationRun['status'] = 'SUCCEEDED'): AnalysisRecommendationRun {
  const evidence = [{ evidenceType: 'DETAIL_CONTENT' as const, sourceEvidenceId: null, excerpt: '현재 본문' }]
  return {
    id: crypto.randomUUID(), contentId, requestedAtUtc: new Date().toISOString(), completedAtUtc: new Date().toISOString(),
    status, providerIdentifier: 'test-provider', modelVersion: 'v1', errorCode: status === 'FAILED' ? 'ANALYSIS_PROVIDER_FAILED' : null,
    rowVersion: 'AQID', items: status === 'FAILED' ? [] : [
      { id: crypto.randomUUID(), kind: 'TITLE', recommendedValue: '추천 제목', reason: '본문의 핵심', confidence: 'HIGH', decision: 'PENDING', modifiedValue: null, decidedAtUtc: null, evidence },
      { id: crypto.randomUUID(), kind: 'SUMMARY', recommendedValue: '추천 요약', reason: '본문 요약', confidence: 'MEDIUM', decision: 'PENDING', modifiedValue: null, decidedAtUtc: null, evidence },
      { id: crypto.randomUUID(), kind: 'CATEGORY', recommendedValue: category.id, reason: '내용 분류', confidence: 'LOW', decision: 'PENDING', modifiedValue: null, decidedAtUtc: null, evidence },
      { id: crypto.randomUUID(), kind: 'TAG', recommendedValue: '추천태그', reason: '검색 근거', confidence: 'MEDIUM', decision: 'PENDING', modifiedValue: null, decidedAtUtc: null, evidence },
    ],
  }
}

describe('Phase 2D recommendation review', () => {
  afterEach(() => vi.restoreAllMocks())

  it('does not mutate current values until item decisions are saved', async () => {
    const current = content()
    const generated = recommendation(current.id)
    vi.spyOn(api, 'latestAnalysisRecommendations').mockResolvedValue(null)
    vi.spyOn(api, 'categories').mockResolvedValue([category])
    vi.spyOn(api, 'requestAnalysisRecommendations').mockResolvedValue(generated)
    const updated = { ...current, title: '사용자 수정 제목' }
    const decidedRun = structuredClone(generated)
    decidedRun.items[0].decision = 'MODIFIED'
    decidedRun.items[0].modifiedValue = '사용자 수정 제목'
    decidedRun.items[2].decision = 'REJECTED'
    const save = vi.spyOn(api, 'saveAnalysisRecommendationDecisions').mockResolvedValue({ content: updated, run: decidedRun })
    let saved: ContentItem | undefined
    const host = document.createElement('div')
    const app = createApp(AnalysisReviewStage, {
      content: current, title: current.title, shortSummary: current.shortSummary ?? '', intake: intake(current.id),
      onRecommendationSaved: (value: ContentItem) => { saved = value },
    })
    app.mount(host)
    await flush()

    const help = host.querySelector<HTMLButtonElement>('[aria-label="분석 추천 도움말"]')!
    help.click()
    await flush()
    expect(host.textContent).toContain('추천 확신은 정확한 확률이 아니라')
    expect(host.textContent).toContain('최종 결정은 사용자가 합니다.')
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
    await flush()
    expect(host.textContent).not.toContain('추천 확신은 정확한 확률이 아니라')

    clickByText(host, '추천 요청')
    await flush()
    expect(host.querySelectorAll('.recommendation-card')).toHaveLength(4)
    expect(host.textContent).toContain('추천 확신 높음')
    expect(host.textContent).toContain('추천 확신 보통')
    expect(host.textContent).toContain('추천 확신 낮음 · 확인 필요')
    expect(host.textContent).not.toContain('신뢰도')
    expect(current.title).toBe('현재 제목')

    const titleCard = host.querySelector<HTMLElement>('[data-kind="TITLE"]')!
    clickByText(host, '수정 후 적용', titleCard)
    await flush()
    setValue(titleCard.querySelector<HTMLInputElement>('input')!, '사용자 수정 제목')
    const categoryCard = host.querySelector<HTMLElement>('[data-kind="CATEGORY"]')!
    clickByText(host, '사용 안 함', categoryCard)
    clickByText(host, '선택한 결정 저장')
    await flush()

    expect(save).toHaveBeenCalledWith(current.id, generated.id, current.rowVersion, [
      { itemId: generated.items[0].id, decision: 'MODIFIED', modifiedValue: '사용자 수정 제목' },
      { itemId: generated.items[2].id, decision: 'REJECTED', modifiedValue: null },
    ])
    expect(saved?.title).toBe('사용자 수정 제목')
    expect(host.textContent).toContain('선택한 추천 결정을 저장했습니다.')
    app.unmount()
  })

  it('shows the fail-closed fallback and keeps manual editing available', async () => {
    const current = content()
    const failed = recommendation(current.id, 'FAILED')
    vi.spyOn(api, 'latestAnalysisRecommendations').mockResolvedValueOnce(null).mockResolvedValueOnce(failed)
    vi.spyOn(api, 'categories').mockResolvedValue([category])
    vi.spyOn(api, 'requestAnalysisRecommendations').mockRejectedValue(new ApiError(
      'ANALYSIS_PROVIDER_NOT_CONNECTED', '추천 기능이 아직 연결되지 않았습니다. 직접 작성으로 계속할 수 있습니다.',
    ))
    const host = document.createElement('div')
    document.body.append(host)
    const app = createApp(AnalysisReviewStage, {
      content: current, title: current.title, shortSummary: current.shortSummary ?? '', intake: intake(current.id),
    })
    app.mount(host)
    await flush()

    clickByText(host, '추천 요청')
    await flush()
    const providerMessage = '추천 기능이 아직 연결되지 않았습니다. 직접 작성으로 계속할 수 있습니다.'
    expect(host.textContent?.split(providerMessage)).toHaveLength(2)
    expect(host.querySelectorAll('[role="alert"]')).toHaveLength(1)
    expect(host.querySelector('[role="alert"]')?.textContent?.trim()).toBe(providerMessage)
    clickByText(host, '수동으로 계속 작성')
    expect(document.activeElement).toBe(host.querySelector('#analysis-title'))
    app.unmount()
    host.remove()
  })

  it('preserves the current content when decision save has a concurrency conflict', async () => {
    const current = content()
    const generated = recommendation(current.id, 'PARTIALLY_SUCCEEDED')
    generated.items = generated.items.slice(0, 2)
    vi.spyOn(api, 'latestAnalysisRecommendations').mockResolvedValue(generated)
    vi.spyOn(api, 'categories').mockResolvedValue([category])
    vi.spyOn(api, 'saveAnalysisRecommendationDecisions').mockRejectedValue(new ApiError('CONCURRENCY_CONFLICT', '다른 변경이 먼저 저장되었습니다.'))
    let saved: ContentItem | undefined
    const host = document.createElement('div')
    const app = createApp(AnalysisReviewStage, {
      content: current, title: current.title, shortSummary: current.shortSummary ?? '', intake: intake(current.id),
      onRecommendationSaved: (value: ContentItem) => { saved = value },
    })
    app.mount(host)
    await flush()

    expect(host.textContent).toContain('일부 항목만 추천되었습니다.')
    expect(host.textContent).toContain('추천 없음: 분류, 태그')
    clickByText(host, '적용', host.querySelector('[data-kind="TITLE"]')!)
    clickByText(host, '선택한 결정 저장')
    await flush()

    expect(host.querySelector('[role="alert"]')?.textContent).toContain('다른 변경이 먼저 저장되었습니다.')
    expect(saved).toBeUndefined()
    expect(current.title).toBe('현재 제목')
    app.unmount()
  })
})
