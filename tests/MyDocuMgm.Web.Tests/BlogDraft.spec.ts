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
    `https://www.instagram.com/p/blog-draft-${crypto.randomUUID()}/`,
  )
  await api.saveManualInstagram(intake.id, '합성 Caption fallback', 'NONE', '', [])
  const content = await api.content(intake.id)
  await api.saveAnalysisReview(intake.id, '합성 분석 제목', '합성 분석 요약', true, content.rowVersion)
  const category = await api.categoryEdit(intake.id)
  await api.saveCategoryEdit(intake.id, category.categoryId, { customLabel: '합성 분류' }, true, category.rowVersion)
  const image = await api.imageStage(intake.id)
  await api.saveImageStage(intake.id, [], true, image.rowVersion)
  const detail = await api.detailStage(intake.id)
  await api.saveDetailStage(intake.id, {}, true, detail.rowVersion)
  return intake.id
}

async function mountBlogDraftPage(contentId: string) {
  await router.push(`/workflow/${contentId}/blog-draft`)
  await router.isReady()
  const host = document.createElement('div')
  const app = createApp({ template: '<RouterView />' })
  app.use(router)
  app.mount(host)
  await flush()
  await flush()
  return { host, app }
}

function footerButton(host: HTMLElement, primary: boolean) {
  return [...host.querySelectorAll<HTMLButtonElement>('.workflow-footer > div > button')]
    .find(button => button.classList.contains('primary') === primary)!
}

function inputText(element: HTMLInputElement | HTMLTextAreaElement, value: string) {
  element.value = value
  element.dispatchEvent(new Event('input', { bubbles: true }))
}

describe('Phase 2B blog draft workflow stage', () => {
  afterEach(() => vi.restoreAllMocks())

  it('shows approved fallback values without persisting on GET', async () => {
    const contentId = await createBlogDraftContent()
    const before = await api.blogDraft(contentId)
    const { host, app } = await mountBlogDraftPage(contentId)

    expect(before.hasSavedDraft).toBe(false)
    expect(before.title).toBe('합성 분석 제목')
    expect(before.body).toBe('합성 Caption fallback')
    expect((host.querySelector('[data-testid="blog-draft-title"]') as HTMLInputElement).value)
      .toBe('합성 분석 제목')
    expect((host.querySelector('[data-testid="blog-draft-body"]') as HTMLTextAreaElement).value)
      .toBe('합성 Caption fallback')
    expect(host.textContent).toContain('로컬 · 미공개')
    expect(host.textContent).toContain('외부 서비스에 게시하거나 전송하지 않습니다')
    expect((await api.blogDraft(contentId)).hasSavedDraft).toBe(false)
    app.unmount()
  })

  it('temporary save restores title, body, and line breaks after re-entry', async () => {
    const contentId = await createBlogDraftContent()
    const mounted = await mountBlogDraftPage(contentId)
    inputText(mounted.host.querySelector('[data-testid="blog-draft-title"]')!, '저장된 초안 제목')
    inputText(mounted.host.querySelector('[data-testid="blog-draft-body"]')!, '첫 줄\n둘째 줄')

    footerButton(mounted.host, false).click()
    await flush()
    await flush()
    const saved = await api.blogDraft(contentId)

    expect(saved.hasSavedDraft).toBe(true)
    expect(saved.title).toBe('저장된 초안 제목')
    expect(saved.body).toBe('첫 줄\n둘째 줄')
    expect(saved.currentWorkflowStep).toBe('BLOG_DRAFT')
    expect(mounted.host.textContent).toContain('블로그 초안을 임시저장했습니다')
    mounted.app.unmount()

    const reentered = await mountBlogDraftPage(contentId)
    expect((reentered.host.querySelector('[data-testid="blog-draft-title"]') as HTMLInputElement).value)
      .toBe('저장된 초안 제목')
    expect((reentered.host.querySelector('[data-testid="blog-draft-body"]') as HTMLTextAreaElement).value)
      .toBe('첫 줄\n둘째 줄')
    reentered.app.unmount()
  })

  it('does not navigate before completion succeeds and then opens completed route', async () => {
    const contentId = await createBlogDraftContent()
    const { host, app } = await mountBlogDraftPage(contentId)
    const failure = vi.spyOn(api, 'saveBlogDraft')
      .mockRejectedValueOnce(new ApiError('SYNTHETIC_BLOG_DRAFT_FAILURE', 'synthetic draft save failed'))

    footerButton(host, true).click()
    await flush()
    await flush()

    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/blog-draft`)
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('synthetic draft save failed')

    failure.mockRestore()
    footerButton(host, true).click()
    await flush()
    await flush()

    expect((await api.content(contentId)).currentWorkflowStep).toBe('COMPLETED')
    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/completed`)
    app.unmount()
  })

  it('keeps the unsaved guard active when modal save fails', async () => {
    const contentId = await createBlogDraftContent()
    const { host, app } = await mountBlogDraftPage(contentId)
    inputText(host.querySelector('[data-testid="blog-draft-title"]')!, 'unsaved after failure')
    const failure = vi.spyOn(api, 'saveBlogDraft')
      .mockRejectedValueOnce(new ApiError('SYNTHETIC_BLOG_DRAFT_FAILURE', 'synthetic draft save failed'))

    void router.push('/contents')
    await flush()
    await flush()
    expect(host.querySelector('.unsaved-dialog')).not.toBeNull()

    host.querySelector<HTMLButtonElement>('.unsaved-dialog .button.primary')!.click()
    await flush()
    await flush()
    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/blog-draft`)
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('synthetic draft save failed')

    host.querySelector<HTMLButtonElement>('.unsaved-dialog .button')!.click()
    await flush()
    void router.push('/contents')
    await flush()
    await flush()

    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/blog-draft`)
    expect(host.querySelector('.unsaved-dialog')).not.toBeNull()
    failure.mockRestore()
    app.unmount()
  })

  it('blocks whitespace completion and preserves the blog draft route', async () => {
    const contentId = await createBlogDraftContent()
    const { host, app } = await mountBlogDraftPage(contentId)
    inputText(host.querySelector('[data-testid="blog-draft-title"]')!, '   ')

    footerButton(host, true).click()
    await flush()

    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/blog-draft`)
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('초안 제목을 입력')
    expect((await api.content(contentId)).currentWorkflowStep).toBe('BLOG_DRAFT')
    app.unmount()
  })

  it('preserves omitted fields, rejects overflow, and makes completion retry idempotent', async () => {
    const contentId = await createBlogDraftContent()
    const initial = await api.blogDraft(contentId)
    const first = await api.saveBlogDraft(
      contentId,
      { title: '부분 저장 제목' },
      false,
      initial.contentRowVersion,
      initial.draftRowVersion,
    )
    const second = await api.saveBlogDraft(
      contentId,
      { body: '부분 저장 본문' },
      false,
      first.contentRowVersion,
      first.draftRowVersion,
    )

    expect(second.title).toBe('부분 저장 제목')
    await expect(api.saveBlogDraft(
      contentId,
      { title: '가'.repeat(201) },
      false,
      second.contentRowVersion,
      second.draftRowVersion,
    )).rejects.toMatchObject({ code: 'BLOG_DRAFT_TITLE_TOO_LONG' })

    const completed = await api.saveBlogDraft(
      contentId,
      { title: second.title, body: second.body },
      true,
      second.contentRowVersion,
      second.draftRowVersion,
    )
    const retry = await api.saveBlogDraft(
      contentId,
      { title: completed.title, body: completed.body },
      true,
      'lost-response-version',
      'lost-response-version',
    )
    expect(retry.currentWorkflowStep).toBe('COMPLETED')
    await expect(api.saveBlogDraft(
      contentId,
      {},
      true,
      'lost-response-version',
      'lost-response-version',
    )).rejects.toMatchObject({ code: 'BLOG_DRAFT_ALREADY_COMPLETED' })
  })

  it('blocks direct entry before detail completion', async () => {
    const intake = await api.createInstagramIntake(
      `https://www.instagram.com/p/blog-draft-bypass-${crypto.randomUUID()}/`,
    )
    await expect(api.blogDraft(intake.id)).rejects.toMatchObject({ code: 'BLOG_DRAFT_NOT_AVAILABLE' })
  })
})
