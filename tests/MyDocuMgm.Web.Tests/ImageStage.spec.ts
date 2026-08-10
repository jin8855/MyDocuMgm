import { createApp, nextTick } from 'vue'
import { router } from '../../src/MyDocuMgm.Web/src/app/router'
import { ApiError, api } from '../../src/MyDocuMgm.Web/src/shared/api/client'
import type { MediaItem } from '../../src/MyDocuMgm.Web/src/shared/types'

async function flush() {
  await Promise.resolve()
  await new Promise(resolve => setTimeout(resolve, 0))
  await nextTick()
}

async function createImageContent(linkedMediaIds?: string[]) {
  const intake = await api.createInstagramIntake(
    `https://www.instagram.com/p/image-${crypto.randomUUID()}/`,
  )
  const defaultMediaId = (await api.linkableMedia(1, 24)).items[0]!.id
  await api.saveManualInstagram(
    intake.id,
    'synthetic image-stage caption',
    'NONE',
    '',
    linkedMediaIds ?? [defaultMediaId],
  )
  const content = await api.content(intake.id)
  await api.saveAnalysisReview(intake.id, '이미지 단계 테스트', '', true, content.rowVersion)
  const categoryEdit = await api.categoryEdit(intake.id)
  await api.saveCategoryEdit(
    intake.id,
    categoryEdit.categoryId,
    {},
    true,
    categoryEdit.rowVersion,
  )
  return { contentId: intake.id, defaultMediaId }
}

async function mountImagePage(contentId: string) {
  await router.push(`/workflow/${contentId}/media`)
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

function syntheticMedia(id: string): MediaItem {
  return {
    id,
    originalFileName: 'synthetic.png',
    thumbnailUrl: 'data:image/png;base64,AA==',
    mimeType: 'image/png',
    sizeBytes: 1,
    width: 1,
    height: 1,
    sortOrder: 1,
    sourceTimestampMs: null,
    isSelected: false,
    isPublicAllowed: false,
    description: '',
    storageStatus: 'READY',
    sha256: 'SYNTHETIC',
    isDeleted: false,
    deletedAtUtc: null,
    rowVersion: 'AQID',
  }
}

async function uploadThroughInput(host: HTMLElement, fileName: string) {
  const input = host.querySelector<HTMLInputElement>('input[type="file"]')!
  const file = new File(['x'], fileName, { type: 'image/png' })
  Object.defineProperty(input, 'files', {
    configurable: true,
    value: { 0: file, length: 1, item: () => file },
  })
  input.dispatchEvent(new Event('change', { bubbles: true }))
  await flush()
  await flush()
}

describe('Phase 2B image workflow stage', () => {
  afterEach(() => vi.restoreAllMocks())

  it('restores the S1 link, unlinks only the relation, and restores the draft on reentry', async () => {
    const { contentId, defaultMediaId } = await createImageContent()
    const { host, app } = await mountImagePage(contentId)

    expect(host.querySelector('[data-testid="image-editing-stage"]')).not.toBeNull()
    expect(host.querySelector('.image-selected-card')).not.toBeNull()
    expect(host.querySelector('.image-selected-card img')).toBeNull()
    expect(host.querySelector('.image-selected-card .thumbnail-image')?.textContent).toContain('미리보기 없음')
    expect((await api.imageStage(contentId)).linkedMediaIds).toEqual([defaultMediaId])

    host.querySelector<HTMLButtonElement>('.image-selected-card button')!.click()
    footerButton(host, false).click()
    await flush()
    await flush()

    expect((await api.imageStage(contentId)).currentWorkflowStep).toBe('MEDIA')
    expect((await api.imageStage(contentId)).linkedMediaIds).toEqual([])
    expect(host.querySelector('[data-testid="image-empty-selection"]')).not.toBeNull()

    app.unmount()
    const reentered = await mountImagePage(contentId)
    expect(reentered.host.querySelector('[data-testid="image-empty-selection"]')).not.toBeNull()
    reentered.app.unmount()
  })

  it('does not navigate before completion succeeds and then advances to detail', async () => {
    const { contentId } = await createImageContent([])
    const { host, app } = await mountImagePage(contentId)
    const failure = vi.spyOn(api, 'saveImageStage')
      .mockRejectedValueOnce(new ApiError('SYNTHETIC_IMAGE_SAVE_FAILURE', 'synthetic image save failed'))

    footerButton(host, true).click()
    await flush()
    await flush()

    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/media`)
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('synthetic image save failed')

    failure.mockRestore()
    footerButton(host, true).click()
    await flush()
    await flush()

    expect((await api.content(contentId)).currentWorkflowStep).toBe('DETAIL')
    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/detail`)
    app.unmount()
  })

  it('cleans only a newly uploaded image when final save fails', async () => {
    const { contentId } = await createImageContent([])
    const { host, app } = await mountImagePage(contentId)
    const uploadedId = `new-image-${crypto.randomUUID()}`
    vi.spyOn(api, 'uploadMedia').mockResolvedValueOnce({
      item: syntheticMedia(uploadedId),
      reused: false,
    })
    const cleanup = vi.spyOn(api, 'permanentlyDeleteOrphanMedia').mockResolvedValueOnce()
    vi.spyOn(api, 'saveImageStage')
      .mockRejectedValueOnce(new ApiError('SYNTHETIC_IMAGE_SAVE_FAILURE', 'synthetic image save failed'))

    await uploadThroughInput(host, 'new-image.png')
    footerButton(host, true).click()
    await flush()
    await flush()

    expect(cleanup).toHaveBeenCalledTimes(1)
    expect(cleanup).toHaveBeenCalledWith(uploadedId)
    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/media`)
    app.unmount()
  })

  it('never cleans a reused media item after final save fails', async () => {
    const { contentId } = await createImageContent([])
    const { host, app } = await mountImagePage(contentId)
    const reusedId = `reused-image-${crypto.randomUUID()}`
    vi.spyOn(api, 'uploadMedia').mockResolvedValueOnce({
      item: syntheticMedia(reusedId),
      reused: true,
    })
    const cleanup = vi.spyOn(api, 'permanentlyDeleteOrphanMedia')
    vi.spyOn(api, 'saveImageStage')
      .mockRejectedValueOnce(new ApiError('SYNTHETIC_IMAGE_SAVE_FAILURE', 'synthetic image save failed'))

    await uploadThroughInput(host, 'reused-image.png')
    footerButton(host, true).click()
    await flush()
    await flush()

    expect(cleanup).not.toHaveBeenCalled()
    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/media`)
    app.unmount()
  })

  it('blocks footer actions while an image upload is pending', async () => {
    const { contentId } = await createImageContent([])
    const { host, app } = await mountImagePage(contentId)
    const uploaded = syntheticMedia(`pending-image-${crypto.randomUUID()}`)
    let finishUpload!: () => void
    vi.spyOn(api, 'uploadMedia').mockImplementation(async () => {
      await new Promise<void>(resolve => { finishUpload = resolve })
      return { item: uploaded, reused: false }
    })
    const save = vi.spyOn(api, 'saveImageStage')

    await uploadThroughInput(host, 'pending-image.png')

    const footerButtons = [...host.querySelectorAll<HTMLButtonElement>('[data-testid="workflow-footer"] button')]
    expect(footerButtons.length).toBeGreaterThan(0)
    expect(footerButtons.every(button => button.disabled)).toBe(true)
    footerButton(host, true).click()
    await flush()
    expect(save).not.toHaveBeenCalled()
    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/media`)
    await router.push('/contents')
    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/media`)

    finishUpload()
    await flush()
    await flush()
    expect(footerButtons.every(button => button.disabled)).toBe(false)
    app.unmount()
  })

  it('allows zero images and rejects a new foreign media id', async () => {
    const { contentId, defaultMediaId } = await createImageContent([])
    const otherMediaId = (await api.linkableMedia(1, 24)).items
      .find(item => item.id !== defaultMediaId)!.id

    await expect(api.saveImageStage(contentId, [otherMediaId], false, (await api.imageStage(contentId)).rowVersion))
      .rejects.toMatchObject({ code: 'IMAGE_MEDIA_NOT_AVAILABLE' })

    const completed = await api.saveImageStage(
      contentId,
      [],
      true,
      (await api.imageStage(contentId)).rowVersion,
    )
    expect(completed.currentWorkflowStep).toBe('DETAIL')
    expect(completed.linkedMediaIds).toEqual([])
  })
})
