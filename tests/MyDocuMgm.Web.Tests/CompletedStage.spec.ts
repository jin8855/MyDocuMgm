import { createApp, nextTick } from 'vue'
import { router } from '../../src/MyDocuMgm.Web/src/app/router'
import { ApiError, api } from '../../src/MyDocuMgm.Web/src/shared/api/client'

async function flush() {
  await Promise.resolve()
  await new Promise(resolve => setTimeout(resolve, 0))
  await nextTick()
}

async function createBlogDraftContent() {
  const intake = await api.createInstagramIntake(
    `https://www.instagram.com/p/completion-${crypto.randomUUID()}/`,
  )
  await api.saveManualInstagram(intake.id, '합성 Caption fallback', 'NONE', '', [])
  const content = await api.content(intake.id)
  await api.saveAnalysisReview(intake.id, '완료 콘텐츠 제목', '완료 콘텐츠 요약', true, content.rowVersion)
  const category = await api.categoryEdit(intake.id)
  await api.saveCategoryEdit(intake.id, category.categoryId, { customLabel: '합성 분류' }, true, category.rowVersion)
  const image = await api.imageStage(intake.id)
  await api.saveImageStage(intake.id, [], true, image.rowVersion)
  const detail = await api.detailStage(intake.id)
  await api.saveDetailStage(intake.id, {}, true, detail.rowVersion)
  return intake.id
}

async function createCompletedContent() {
  const contentId = await createBlogDraftContent()
  const draft = await api.blogDraft(contentId)
  await api.saveBlogDraft(
    contentId,
    { title: '저장된 완료 초안', body: '첫 줄\n둘째 줄' },
    true,
    draft.contentRowVersion,
    draft.draftRowVersion,
  )
  return contentId
}

async function mountCompletionPage(contentId: string) {
  await router.push(`/workflow/${contentId}/completed`)
  await router.isReady()
  const host = document.createElement('div')
  const app = createApp({ template: '<RouterView />' })
  app.use(router)
  app.mount(host)
  await flush()
  await flush()
  return { host, app }
}

describe('completed workflow stage', () => {
  afterEach(() => vi.restoreAllMocks())

  it('shows the saved projection read-only and offers only the work list action', async () => {
    const contentId = await createCompletedContent()
    const save = vi.spyOn(api, 'saveBlogDraft')
    const { host, app } = await mountCompletionPage(contentId)

    expect(host.querySelector('[data-testid="completion-stage"]')).not.toBeNull()
    expect(host.querySelector('[data-testid="completion-content-name"]')?.textContent).toBe('완료 콘텐츠 제목')
    expect(host.querySelector('[data-testid="completion-draft-name"]')?.textContent).toBe('저장된 완료 초안')
    expect(host.querySelector('[data-testid="completion-draft-body"]')?.textContent).toBe('첫 줄\n둘째 줄')
    expect(host.textContent).toContain('외부에 게시하거나 전송하지 않았습니다')
    expect(host.textContent).not.toContain('완료 상태 저장')
    expect(host.textContent).not.toContain('이전 · 블로그 초안')
    expect(host.querySelector('[data-testid="workflow-footer"]')).toBeNull()
    expect(save).not.toHaveBeenCalled()

    const action = [...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent === '작업목록으로')!
    action.click()
    await flush()
    expect(router.currentRoute.value.path).toBe('/contents')
    expect(save).not.toHaveBeenCalled()
    app.unmount()
  })

  it('keeps completed state and data after direct re-entry without another save', async () => {
    const contentId = await createCompletedContent()
    const save = vi.spyOn(api, 'saveBlogDraft')
    const first = await mountCompletionPage(contentId)
    first.app.unmount()

    await router.push('/contents')
    const reentered = await mountCompletionPage(contentId)

    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/completed`)
    expect((await api.content(contentId)).currentWorkflowStep).toBe('COMPLETED')
    expect(reentered.host.querySelector('[data-testid="completion-draft-body"]')?.textContent)
      .toBe('첫 줄\n둘째 줄')
    expect(save).not.toHaveBeenCalled()
    reentered.app.unmount()
  })

  it('blocks incomplete and nonexistent direct routes', async () => {
    const incompleteId = await createBlogDraftContent()
    const incomplete = await mountCompletionPage(incompleteId)

    expect(incomplete.host.querySelector('[role="alert"]')?.textContent)
      .toContain('완료된 콘텐츠만 완료 화면을 조회할 수 있습니다')
    incomplete.app.unmount()

    const missing = await mountCompletionPage(crypto.randomUUID())
    expect(missing.host.querySelector('[role="alert"]')?.textContent).toContain('콘텐츠를 찾을 수 없습니다')
    missing.app.unmount()
  })

  it('shows a diagnosable error when the required saved draft is missing', async () => {
    const contentId = await createBlogDraftContent()
    vi.spyOn(api, 'completion').mockRejectedValueOnce(new ApiError(
      'COMPLETION_BLOG_DRAFT_MISSING',
      '완료된 콘텐츠의 저장된 블로그 초안을 찾을 수 없습니다.',
    ))
    const { host, app } = await mountCompletionPage(contentId)

    expect(host.querySelector('[role="alert"]')?.textContent)
      .toContain('완료된 콘텐츠의 저장된 블로그 초안을 찾을 수 없습니다')
    expect(host.querySelector('[data-testid="completion-stage"]')).toBeNull()
    app.unmount()
  })
})
