import { createApp, nextTick } from 'vue'
import { router } from '../../src/MyDocuMgm.Web/src/app/router'
import { ApiError, api } from '../../src/MyDocuMgm.Web/src/shared/api/client'

async function flush() {
  await Promise.resolve()
  await new Promise(resolve => setTimeout(resolve, 0))
  await nextTick()
}

async function createDetailContent(options?: { summary?: string; media?: boolean; categoryValues?: Record<string, string> }) {
  const intake = await api.createInstagramIntake(
    `https://www.instagram.com/p/detail-${crypto.randomUUID()}/`,
  )
  const mediaId = options?.media === false ? undefined : (await api.linkableMedia(1, 24)).items[0]!.id
  await api.saveManualInstagram(
    intake.id,
    '합성 DETAIL caption',
    'PRESENT',
    '합성 고정 댓글',
    mediaId ? [mediaId] : [],
  )
  const content = await api.content(intake.id)
  await api.saveAnalysisReview(
    intake.id,
    '합성 자료 상세 제목',
    options?.summary ?? '합성 자료 상세 요약',
    true,
    content.rowVersion,
  )
  const categoryEdit = await api.categoryEdit(intake.id)
  await api.saveCategoryEdit(
    intake.id,
    categoryEdit.categoryId,
    options?.categoryValues ?? { customLabel: '합성 주제', additionalInfo: '합성 추가 정보' },
    true,
    categoryEdit.rowVersion,
  )
  const imageStage = await api.imageStage(intake.id)
  await api.saveImageStage(
    intake.id,
    mediaId ? [mediaId] : [],
    true,
    imageStage.rowVersion,
  )
  return { contentId: intake.id, mediaId }
}

async function mountDetailPage(contentId: string) {
  await router.push(`/workflow/${contentId}/detail`)
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

describe('Phase 2B detail workflow stage', () => {
  afterEach(() => vi.restoreAllMocks())

  it('renders prior-stage data as a read-only integrated review', async () => {
    const { contentId } = await createDetailContent()
    const { host, app } = await mountDetailPage(contentId)
    const text = host.textContent ?? ''

    expect(host.querySelector('[data-testid="detail-review-stage"]')).not.toBeNull()
    expect(text).toContain('합성 DETAIL caption')
    expect(text).toContain('합성 고정 댓글')
    expect(text).toContain('합성 자료 상세 제목')
    expect(text).toContain('합성 자료 상세 요약')
    expect(text).toContain('합성 주제')
    expect(text).toContain('연결 이미지')
    expect(host.querySelector('input, textarea, select')).toBeNull()
    expect([...host.querySelectorAll<HTMLButtonElement>('button')]
      .some(button => button.textContent?.trim() === '편집'))
      .toBe(false)
    expect((await api.detailStage(contentId)).editableFields).toEqual([])
    app.unmount()
  })

  it('keeps DETAIL on repeated drafts and preserves every prior-stage value', async () => {
    const { contentId } = await createDetailContent()
    const before = await api.detailStage(contentId)

    const first = await api.saveDetailStage(contentId, {}, false, before.rowVersion)
    const second = await api.saveDetailStage(contentId, {}, false, first.rowVersion)

    expect(first.currentWorkflowStep).toBe('DETAIL')
    expect(second.currentWorkflowStep).toBe('DETAIL')
    expect(second).toMatchObject({
      title: before.title,
      shortSummary: before.shortSummary,
      manualCaption: before.manualCaption,
      pinnedAuthorCommentText: before.pinnedAuthorCommentText,
      categoryId: before.categoryId,
      categoryValues: before.categoryValues,
      linkedMedia: before.linkedMedia,
    })
  })

  it('does not navigate before completion succeeds and then advances to blog draft', async () => {
    const { contentId } = await createDetailContent()
    const { host, app } = await mountDetailPage(contentId)
    const failure = vi.spyOn(api, 'saveDetailStage')
      .mockRejectedValueOnce(new ApiError('SYNTHETIC_DETAIL_SAVE_FAILURE', 'synthetic detail save failed'))

    footerButton(host, true).click()
    await flush()
    await flush()

    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/detail`)
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('synthetic detail save failed')

    failure.mockRestore()
    footerButton(host, true).click()
    await flush()
    await flush()

    expect((await api.content(contentId)).currentWorkflowStep).toBe('BLOG_DRAFT')
    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/blog-draft`)
    app.unmount()
  })

  it('rejects entry bypass and every unapproved detail field', async () => {
    const intake = await api.createInstagramIntake(
      `https://www.instagram.com/p/detail-bypass-${crypto.randomUUID()}/`,
    )
    await expect(api.detailStage(intake.id)).rejects.toMatchObject({ code: 'DETAIL_STAGE_NOT_AVAILABLE' })

    const { contentId } = await createDetailContent()
    const stage = await api.detailStage(contentId)
    await expect(api.saveDetailStage(contentId, { title: '덮어쓰기 시도' }, false, stage.rowVersion))
      .rejects.toMatchObject({ code: 'DETAIL_FIELD_NOT_EDITABLE' })
    expect((await api.detailStage(contentId)).title).toBe('합성 자료 상세 제목')
  })

  it('shows neutral empty states for omitted optional summary, category values, and media', async () => {
    const { contentId } = await createDetailContent({ summary: '', media: false, categoryValues: {} })
    const { host, app } = await mountDetailPage(contentId)

    expect(host.querySelector('[data-testid="detail-empty-category"]')).not.toBeNull()
    expect(host.querySelector('[data-testid="detail-empty-media"]')).not.toBeNull()
    expect(host.textContent).toContain('입력된 내용 없음')
    app.unmount()
  })
})
