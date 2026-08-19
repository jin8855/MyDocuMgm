import { createApp, nextTick } from 'vue'
import { router } from '../../src/MyDocuMgm.Web/src/app/router'
import AnalysisReviewStage from '../../src/MyDocuMgm.Web/src/features/analysis-review/AnalysisReviewStage.vue'
import { api } from '../../src/MyDocuMgm.Web/src/shared/api/client'
import type { UrlIntake } from '../../src/MyDocuMgm.Web/src/shared/types'

async function flush() {
  await Promise.resolve()
  await new Promise(resolve => setTimeout(resolve, 0))
  await nextTick()
}

function setValue(element: HTMLInputElement | HTMLTextAreaElement, value: string) {
  element.value = value
  element.dispatchEvent(new Event('input', { bubbles: true }))
}

describe('Phase 2B manual analysis review', () => {
  afterEach(() => vi.restoreAllMocks())

  it('shows only manual evidence, saves a draft, and completes into category edit', async () => {
    const suffix = crypto.randomUUID()
    const intake = await api.createInstagramIntake(
      `https://www.instagram.com/p/analysis-${suffix}/`,
    )
    await api.saveManualInstagram(
      intake.id,
      '사용자가 직접 입력한 Caption',
      'PRESENT',
      '작성자가 직접 쓴 고정 댓글',
      [],
    )
    const save = vi.spyOn(api, 'saveAnalysisReview')

    await router.push(`/workflow/${intake.id}/analysis-review`)
    await router.isReady()
    const host = document.createElement('div')
    const app = createApp({ template: '<RouterView />' })
    app.use(router)
    app.mount(host)
    await flush()
    await flush()

    expect(host.querySelector('[data-testid="manual-analysis-review"]')).not.toBeNull()
    expect(host.textContent).toContain('수동 분석 검토')
    expect(host.textContent).toContain('사용자가 직접 입력한 Caption')
    expect(host.textContent).toContain('작성자가 직접 쓴 고정 댓글')
    expect(host.textContent).toContain('AI 분석이나 외부 수집 없이')
    expect(host.textContent).not.toContain('자동 분석 결과')

    setValue(host.querySelector<HTMLInputElement>('#analysis-title')!, '  직접 정한 제목  ')
    setValue(host.querySelector<HTMLTextAreaElement>('#analysis-summary')!, '  직접 작성한 요약  ')
    const draft = [...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.trim() === '임시저장')!
    draft.click()
    await flush()

    expect(save).toHaveBeenLastCalledWith(
      intake.id,
      '  직접 정한 제목  ',
      '  직접 작성한 요약  ',
      false,
      expect.any(String),
    )
    expect((await api.content(intake.id)).currentWorkflowStep).toBe('ANALYSIS_REVIEW')
    expect(host.textContent).not.toContain('저장하지 않은 변경')

    const next = [...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.includes('다음 · 분류별 편집'))!
    next.click()
    await flush()
    await flush()

    expect(save).toHaveBeenLastCalledWith(
      intake.id,
      '직접 정한 제목',
      '직접 작성한 요약',
      true,
      expect.any(String),
    )
    expect((await api.content(intake.id)).currentWorkflowStep).toBe('CATEGORY_EDIT')
    expect(router.currentRoute.value.path).toBe(`/workflow/${intake.id}/category-edit`)
    app.unmount()
  })

  it('keeps the user on analysis review when the title is empty', async () => {
    const intake = await api.createInstagramIntake(
      `https://www.instagram.com/reel/analysis-${crypto.randomUUID()}/`,
    )
    await api.saveManualInstagram(intake.id, 'Caption', 'NONE', '', [])
    await router.push(`/workflow/${intake.id}/analysis-review`)
    const host = document.createElement('div')
    const app = createApp({ template: '<RouterView />' })
    app.use(router)
    app.mount(host)
    await flush()
    await flush()

    setValue(host.querySelector<HTMLInputElement>('#analysis-title')!, '   ')
    ;[...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.includes('다음 · 분류별 편집'))!.click()
    await flush()

    expect(host.querySelector('[role="alert"]')?.textContent).toContain('제목을 입력')
    expect(router.currentRoute.value.path).toBe(`/workflow/${intake.id}/analysis-review`)
    app.unmount()
  })

  it('does not advance an incomplete S1 intake', async () => {
    const intake = await api.createInstagramIntake(
      `https://www.instagram.com/p/incomplete-${crypto.randomUUID()}/`,
    )
    await router.push(`/workflow/${intake.id}/analysis-review`)
    const host = document.createElement('div')
    const app = createApp({ template: '<RouterView />' })
    app.use(router)
    app.mount(host)
    await flush()
    await flush()

    setValue(host.querySelector<HTMLInputElement>('#analysis-title')!, '미완료 접수')
    ;[...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.includes('다음 · 분류별 편집'))!.click()
    await flush()

    expect(host.querySelector('[role="alert"]')?.textContent)
      .toContain('수동 Instagram 접수를 완료한 뒤')
    expect((await api.content(intake.id)).currentWorkflowStep).toBe('URL')
    expect(router.currentRoute.value.path).toBe(`/workflow/${intake.id}/analysis-review`)
    app.unmount()
  })

  it.each([
    {
      name: 'generic HTTP metadata',
      sourceKind: 'GENERIC' as const,
      sourceAcquisitionMode: 'HTTP_METADATA' as const,
      instagramContentType: null,
      expectedType: '일반 웹 자료',
      expectedMode: 'URL 자동 수집',
      expectedGuide: '웹페이지에서 가져온 제목과 설명을 검토하고 필요한 내용을 수정하세요.',
      rejects: ['Instagram 게시물', 'Instagram 릴스', '수동 입력'],
    },
    {
      name: 'manual Instagram post',
      sourceKind: 'INSTAGRAM' as const,
      sourceAcquisitionMode: 'MANUAL' as const,
      instagramContentType: 'POST' as const,
      expectedType: 'Instagram 게시물',
      expectedMode: '수동 입력',
      expectedGuide: 'AI 분석이나 외부 수집 없이 사용자가 입력한 자료만 표시합니다.',
      rejects: ['일반 웹 자료', 'URL 자동 수집'],
    },
    {
      name: 'unknown source',
      sourceKind: 'GENERIC' as const,
      sourceAcquisitionMode: null,
      instagramContentType: null,
      expectedType: '기타 자료',
      expectedMode: '확인 필요',
      expectedGuide: '자료 출처와 입력 방식을 확인하고 필요한 내용을 수정하세요.',
      rejects: ['Instagram 게시물', 'Instagram 릴스', 'URL 자동 수집'],
    },
  ])('uses source-neutral presentation labels for $name', async scenario => {
    const intake: UrlIntake = {
      id: crypto.randomUUID(),
      originalUrl: 'https://example.test/article',
      normalizedUrl: 'https://example.test/article',
      sourceKind: scenario.sourceKind,
      status: 'CONTENT_READY',
      isDuplicate: false,
      manualBody: null,
      manualBodyPresent: false,
      instagramContentType: scenario.instagramContentType,
      manualCaption: scenario.sourceKind === 'INSTAGRAM' ? 'manual caption' : null,
      pinnedAuthorCommentState: null,
      pinnedAuthorCommentText: null,
      sourceAcquisitionMode: scenario.sourceAcquisitionMode,
      linkedMediaIds: [],
    }
    const host = document.createElement('div')
    const app = createApp(AnalysisReviewStage, {
      title: '검토 제목',
      shortSummary: '검토 요약',
      intake,
    })
    app.mount(host)

    expect(host.textContent).toContain(scenario.expectedType)
    expect(host.textContent).toContain(scenario.expectedMode)
    expect(host.textContent).toContain(scenario.expectedGuide)
    for (const rejected of scenario.rejects) expect(host.textContent).not.toContain(rejected)

    app.unmount()
  })
})
