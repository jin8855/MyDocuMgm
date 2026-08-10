import { createApp, nextTick } from 'vue'
import CleanupPage from '../../src/MyDocuMgm.Web/src/pages/CleanupPage.vue'
import { router } from '../../src/MyDocuMgm.Web/src/app/router'
import { ApiError, api } from '../../src/MyDocuMgm.Web/src/shared/api/client'

async function flush() {
  await Promise.resolve()
  await new Promise(resolve => setTimeout(resolve, 0))
  await nextTick()
}

describe('Phase 2A cleanup web contract', () => {
  afterEach(() => vi.restoreAllMocks())

  it('moves one content to trash and removes it from the active list', async () => {
    const intake = await api.createUrlIntake(`https://example.com/cleanup-${crypto.randomUUID()}`)
    const content = await api.content(intake.id)

    await api.softDeleteContent(content.id, content.rowVersion)

    const active = await api.contents({
      majorCategory: '', attributeKey: '', attributeValue: '', status: '', workflowStep: '',
      searchScope: 'ALL', keyword: content.title, page: 1, pageSize: 24,
    })
    expect(active.items.some(item => item.id === content.id)).toBe(false)
    expect((await api.trash()).some(item => item.id === content.id)).toBe(true)

    await api.permanentlyDeleteContent(content.id)
    expect((await api.trash()).some(item => item.id === content.id)).toBe(false)
  })

  it('shows orphan-first order and protects content with owned media', async () => {
    vi.spyOn(api, 'trash').mockResolvedValue([{
      id: 'content-1', title: 'synthetic trash', deletedAtUtc: new Date().toISOString(),
      ownedMediaCount: 1, rowVersion: 'row',
    }])
    vi.spyOn(api, 'orphanMedia').mockResolvedValue([{
      id: 'media-1', contentId: 'content-1', originalFileName: 'synthetic.png',
      linkCount: 0, fileExists: true, fileState: 'MEDIA_FILE_READY_FOR_CLEANUP', rowVersion: 'row',
    }])
    const deleteMedia = vi.spyOn(api, 'permanentlyDeleteOrphanMedia').mockResolvedValue()
    const deleteContent = vi.spyOn(api, 'permanentlyDeleteContent').mockRejectedValue(
      new ApiError('CONTENT_HAS_OWNED_MEDIA', 'owned media remains'),
    )
    vi.spyOn(window, 'confirm').mockReturnValue(true)

    const host = document.createElement('div')
    const app = createApp(CleanupPage)
    app.mount(host)
    await flush()

    expect(host.textContent).toContain('연결 없는 미디어')
    expect(host.textContent).toContain('휴지통')
    const contentButton = [...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.includes('콘텐츠 영구 삭제'))!
    expect(contentButton.disabled).toBe(false)
    contentButton.click()
    await flush()
    expect(deleteContent).toHaveBeenCalledTimes(1)
    expect(host.textContent).toContain('owned media remains')
    expect(host.textContent).toContain('synthetic trash')

    const mediaButton = [...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.includes('미디어 영구 삭제'))!
    mediaButton.click()
    await flush()
    expect(deleteMedia).toHaveBeenCalledTimes(1)
    expect(deleteContent).toHaveBeenCalledTimes(1)
    app.unmount()
  })

  it('calls soft-delete exactly once after confirmation and never on cancel', async () => {
    const softDelete = vi.spyOn(api, 'softDeleteContent').mockResolvedValue()
    await router.push('/workflow/demo-5/detail')
    await router.isReady()
    const host = document.createElement('div')
    const app = createApp({ template: '<RouterView />' })
    app.use(router)
    app.mount(host)
    await flush()
    await flush()
    await flush()

    const openDelete = () => [...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.trim() === '삭제')!
    openDelete().click()
    await nextTick()
    const cancel = [...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.trim() === '취소')!
    cancel.click()
    expect(softDelete).not.toHaveBeenCalled()

    openDelete().click()
    await nextTick()
    const confirm = [...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.trim() === '휴지통으로 이동')!
    confirm.click()
    await flush()
    expect(softDelete).toHaveBeenCalledTimes(1)
    expect(router.currentRoute.value.path).toBe('/contents')
    app.unmount()
  })

  it('loads the media workflow for non-cooking content without calling the ingredient endpoint', async () => {
    const existing = await api.content('demo-2')
    vi.spyOn(api, 'content').mockResolvedValue({ ...existing, categoryCode: 'OTHER', currentWorkflowStep: 'MEDIA' })
    vi.spyOn(api, 'imageStage').mockResolvedValue({
      contentId: existing.id,
      currentWorkflowStep: 'MEDIA',
      rowVersion: existing.rowVersion,
      linkedMediaIds: [],
      linkedMedia: [],
    })
    const ingredients = vi.spyOn(api, 'ingredients').mockRejectedValue(new Error('must not be called'))
    vi.spyOn(api, 'media').mockResolvedValue({
      items: [], totalCount: 0, page: 1, pageSize: 24, totalPages: 0,
      selectedCount: 0, duplicateCount: 0, deletedCount: 0,
    })

    await router.push('/workflow/demo-2/media')
    await router.isReady()
    const host = document.createElement('div')
    const app = createApp({ template: '<RouterView />' })
    app.use(router)
    app.mount(host)
    await flush()
    await flush()

    expect(ingredients).not.toHaveBeenCalled()
    expect(host.textContent).toContain('로컬 이미지 등록')
    expect(host.textContent).not.toContain('요리 분류 콘텐츠에서만 재료를 변경할 수 있습니다.')
    app.unmount()
  })
})
