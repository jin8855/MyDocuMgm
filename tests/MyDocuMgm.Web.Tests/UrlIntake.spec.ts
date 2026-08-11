import { createApp, nextTick } from 'vue'
import UrlStage from '../../src/MyDocuMgm.Web/src/features/content-workflow/UrlStage.vue'
import { ApiError, api } from '../../src/MyDocuMgm.Web/src/shared/api/client'

async function flush() {
  await Promise.resolve()
  await new Promise(resolve => setTimeout(resolve, 0))
  await nextTick()
}

function setValue(element: HTMLInputElement | HTMLTextAreaElement, value: string) {
  element.value = value
  element.dispatchEvent(new Event('input', { bubbles: true }))
}

function mountStage(contentId = `new-${crypto.randomUUID()}`, newWork = true) {
  const host = document.createElement('div')
  let acceptedId = ''
  const app = createApp(UrlStage, {
    contentId,
    newWork,
    onAccepted: (id: string) => { acceptedId = id },
  })
  app.mount(host)
  return { host, app, acceptedId: () => acceptedId }
}

async function submitUrl(host: HTMLElement, url: string) {
  setValue(host.querySelector<HTMLInputElement>('#source-url')!, url)
  host.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
  await flush()
  await flush()
}

describe('Phase 2A URL intake web contract', () => {
  it('loads an existing generic intake with no fetch attempt as an explicit empty state', async () => {
    const intake = await api.createUrlIntake(`https://example.com/empty-${crypto.randomUUID()}`)
    const { host, app } = mountStage(intake.id, false)

    await flush()
    await flush()

    expect(await api.latestExternalFetch(intake.id)).toBeUndefined()
    expect(host.querySelector('[role="alert"]')).toBeNull()
    expect(host.textContent).toContain('미리보기 가져오기')
    app.unmount()
  })

  it('shows invalid URL validation and supports retry', async () => {
    const { host, app } = mountStage()

    await submitUrl(host, 'file:///c:/private.txt')
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('http 또는 https')

    await submitUrl(host, `https://www.instagram.com/p/retry-${crypto.randomUUID()}/`)
    expect(host.textContent).toContain('URL을 저장했습니다')
    expect(host.textContent).toContain('수집 대기')
    app.unmount()
  })

  it('loads the existing record and duplicate message for an equivalent URL', async () => {
    const suffix = crypto.randomUUID()
    const first = await api.createInstagramIntake(`https://instagram.com/p/Case-${suffix}/?share=one#fragment`)
    const { host, app } = mountStage()

    await submitUrl(host, `https://www.instagram.com/p/Case-${suffix}/?share=two#second`)

    expect(host.textContent).toContain('이미 등록된 URL입니다. 기존 작업을 불러왔습니다.')
    expect(host.textContent).toContain(first.normalizedUrl)
    app.unmount()
  })

  it('saves and reloads manual caption, author-pinned comment state, and media', async () => {
    const suffix = crypto.randomUUID()
    const sourceUrl = 'https://www.instagram.com/p/manual-' + suffix + '/?share=1#fragment'
    const { host, app } = mountStage()
    await submitUrl(host, sourceUrl)
    const id = (await api.createInstagramIntake(sourceUrl)).id

    const caption = host.querySelector<HTMLTextAreaElement>('#manual-caption')!
    const comment = host.querySelector<HTMLTextAreaElement>('#pinned-author-comment-text')!
    expect(comment.disabled).toBe(true)
    setValue(caption, '  synthetic manual caption  ')

    const present = host.querySelector<HTMLInputElement>('input[value="PRESENT"]')!
    present.checked = true
    present.dispatchEvent(new Event('change', { bubbles: true }))
    await nextTick()
    expect(comment.disabled).toBe(false)
    setValue(comment, '   ')
    ;[...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.trim() === '수동 등록 저장')!.click()
    await flush()
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('고정 댓글 본문')

    setValue(comment, '  author pinned comment  ')
    const checkbox = host.querySelector<HTMLInputElement>('.media-link-card input[type="checkbox"]')!
    checkbox.checked = true
    checkbox.dispatchEvent(new Event('change', { bubbles: true }))
    await nextTick()
    ;[...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.trim() === '수동 등록 저장')!.click()
    await flush()
    expect(host.textContent).toContain('수동으로 입력한 Instagram 자료')
    app.unmount()

    const reentered = mountStage(id, false)
    await flush()
    await flush()
    expect(reentered.host.querySelector<HTMLTextAreaElement>('#manual-caption')?.value)
      .toBe('synthetic manual caption')
    expect(reentered.host.querySelector<HTMLInputElement>('input[value="PRESENT"]')?.checked).toBe(true)
    expect(reentered.host.querySelector<HTMLTextAreaElement>('#pinned-author-comment-text')?.value)
      .toBe('author pinned comment')
    expect(reentered.host.querySelector<HTMLInputElement>('.media-link-card input[type="checkbox"]')?.checked)
      .toBe(true)

    const noneRadio = reentered.host.querySelector<HTMLInputElement>('input[value="NONE"]')!
    noneRadio.checked = true
    noneRadio.dispatchEvent(new Event('change', { bubbles: true }))
    await nextTick()
    const cleared = reentered.host.querySelector<HTMLTextAreaElement>('#pinned-author-comment-text')!
    expect(cleared.disabled).toBe(true)
    expect(cleared.value).toBe('')
    ;[...reentered.host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.trim() === '수동 등록 저장')!.click()
    await flush()
    const none = await api.urlIntake(id)
    expect(none.pinnedAuthorCommentState).toBe('NONE')
    expect(none.pinnedAuthorCommentText).toBeNull()
    reentered.app.unmount()
  })
  it('disables duplicate submission while saving and exposes API retry', async () => {
    const { host, app } = mountStage()
    let resolveRequest!: (value: Awaited<ReturnType<typeof api.createInstagramIntake>>) => void
    const delayed = new Promise<Awaited<ReturnType<typeof api.createInstagramIntake>>>(resolve => {
      resolveRequest = resolve
    })
    const original = api.createInstagramIntake.bind(api)
    const spy = vi.spyOn(api, 'createInstagramIntake').mockReturnValueOnce(delayed)
    setValue(host.querySelector<HTMLInputElement>('#source-url')!, 'https://www.instagram.com/p/delayed/')
    host.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    await nextTick()
    expect(host.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(true)
    resolveRequest(await original(`https://www.instagram.com/p/delayed-${crypto.randomUUID()}/`))
    await flush()
    spy.mockRejectedValueOnce(new ApiError('UNEXPECTED_ERROR', '일시적인 API 오류'))
    await submitUrl(host, `https://www.instagram.com/p/error-${crypto.randomUUID()}/`)
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('일시적인 API 오류')
    spy.mockRestore()
    app.unmount()
  })

  it('reuses the existing upload control and does not save manual fields after a rejected file', async () => {
    const suffix = crypto.randomUUID()
    const sourceUrl = 'https://www.instagram.com/p/media-' + suffix + '/'
    const { host, app } = mountStage()
    await submitUrl(host, sourceUrl)
    const id = (await api.createInstagramIntake(sourceUrl)).id
    setValue(host.querySelector<HTMLTextAreaElement>('#manual-caption')!, 'unsaved caption')

    const input = host.querySelector<HTMLInputElement>('input[type="file"]')!
    expect(input).toBeTruthy()
    const invalid = new File(['not an image'], 'unsafe.txt', { type: 'text/plain' })
    Object.defineProperty(input, 'files', { configurable: true, value: { 0: invalid, length: 1, item: () => invalid } })
    input.dispatchEvent(new Event('change', { bubbles: true }))
    await flush()

    expect(host.querySelector('[role="alert"]')?.textContent).toContain('JPEG, PNG, WebP')
    const intake = await api.urlIntake(id)
    expect(intake.manualCaption).toBeNull()
    expect(intake.status).toBe('URL_ACCEPTED')
    app.unmount()
  })
  it('compensates a newly uploaded orphan when final manual save fails', async () => {
    const sourceUrl = `https://www.instagram.com/p/compensation-${crypto.randomUUID()}/`
    const { host, app } = mountStage()
    await submitUrl(host, sourceUrl)
    const uploadedId = `uploaded-${crypto.randomUUID()}`
    const uploadSpy = vi.spyOn(api, 'uploadMedia').mockResolvedValueOnce({
      item: {
        id: uploadedId,
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
      },
      reused: false,
    })
    const cleanupSpy = vi.spyOn(api, 'permanentlyDeleteOrphanMedia').mockResolvedValueOnce()
    const saveSpy = vi.spyOn(api, 'saveManualInstagram')
      .mockRejectedValueOnce(new ApiError('SYNTHETIC_SAVE_FAILED', 'synthetic save failed'))

    setValue(host.querySelector<HTMLTextAreaElement>('#manual-caption')!, 'caption before failure')
    const input = host.querySelector<HTMLInputElement>('input[type="file"]')!
    const file = new File(['x'], 'synthetic.png', { type: 'image/png' })
    Object.defineProperty(input, 'files', { configurable: true, value: { 0: file, length: 1, item: () => file } })
    input.dispatchEvent(new Event('change', { bubbles: true }))
    await flush()
    await flush()

    host.querySelector<HTMLButtonElement>('.manual-instagram-panel .inline-actions button.primary')!.click()
    await flush()
    await flush()

    expect(uploadSpy).toHaveBeenCalledOnce()
    expect(saveSpy).toHaveBeenCalledOnce()
    expect(cleanupSpy).toHaveBeenCalledWith(uploadedId)
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('synthetic save failed')
    saveSpy.mockRestore()
    cleanupSpy.mockRestore()
    uploadSpy.mockRestore()
    app.unmount()
  })

  it('does not compensate reused media and exposes the finite text limits', async () => {
    const sourceUrl = `https://www.instagram.com/p/reused-compensation-${crypto.randomUUID()}/`
    const { host, app } = mountStage()
    await submitUrl(host, sourceUrl)
    const reusedId = `reused-${crypto.randomUUID()}`
    const uploadSpy = vi.spyOn(api, 'uploadMedia').mockResolvedValueOnce({
      item: {
        id: reusedId,
        originalFileName: 'existing.png',
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
        sha256: 'EXISTING',
        isDeleted: false,
        deletedAtUtc: null,
        rowVersion: 'AQID',
      },
      reused: true,
    })
    const cleanupSpy = vi.spyOn(api, 'permanentlyDeleteOrphanMedia').mockResolvedValue()
    const saveSpy = vi.spyOn(api, 'saveManualInstagram')
      .mockRejectedValueOnce(new ApiError('SYNTHETIC_SAVE_FAILED', 'synthetic reused save failed'))

    const caption = host.querySelector<HTMLTextAreaElement>('#manual-caption')!
    const comment = host.querySelector<HTMLTextAreaElement>('#pinned-author-comment-text')!
    expect(caption.maxLength).toBe(20_000)
    expect(comment.maxLength).toBe(10_000)
    setValue(caption, 'caption before reused failure')
    const input = host.querySelector<HTMLInputElement>('input[type="file"]')!
    const file = new File(['x'], 'existing.png', { type: 'image/png' })
    Object.defineProperty(input, 'files', { configurable: true, value: { 0: file, length: 1, item: () => file } })
    input.dispatchEvent(new Event('change', { bubbles: true }))
    await flush()
    await flush()

    host.querySelector<HTMLButtonElement>('.manual-instagram-panel .inline-actions button.primary')!.click()
    await flush()
    await flush()

    expect(uploadSpy).toHaveBeenCalledOnce()
    expect(saveSpy).toHaveBeenCalledOnce()
    expect(cleanupSpy).not.toHaveBeenCalled()
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('synthetic reused save failed')
    saveSpy.mockRestore()
    cleanupSpy.mockRestore()
    uploadSpy.mockRestore()
    app.unmount()
  })

  it('previews a generic public page before explicit apply', async () => {
    const { host, app } = mountStage()
    const sourceUrl = 'https://example.com/article-' + crypto.randomUUID()

    await submitUrl(host, sourceUrl)
    const intakeBefore = await api.createUrlIntake(sourceUrl)
    expect(intakeBefore.status).toBe('URL_ACCEPTED')
    expect(intakeBefore.sourceAcquisitionMode).toBeNull()

    host.querySelector<HTMLButtonElement>('[data-testid="external-fetch-start"]')!.click()
    await flush()
    await flush()

    expect(host.querySelector<HTMLTextAreaElement>('#external-fetch-body')?.value)
      .toContain('합성 HTML 추출 결과')
    const stillPreview = await api.urlIntake(intakeBefore.id)
    expect(stillPreview.status).toBe('URL_ACCEPTED')
    expect(stillPreview.manualBody).toBeNull()

    host.querySelector<HTMLButtonElement>('[data-testid="external-fetch-apply"]')!.click()
    await flush()
    await flush()

    const applied = await api.urlIntake(intakeBefore.id)
    expect(applied.status).toBe('CONTENT_READY')
    expect(applied.sourceAcquisitionMode).toBe('HTTP_METADATA')
    expect(applied.manualBody).toContain('합성 HTML 추출 결과')
    expect(host.textContent).toContain('미리보기를 현재 자료에 적용했습니다')
    app.unmount()
  })

  it('shows localized failure, preserves manual input, and blocks a fourth fetch', async () => {
    const { host, app } = mountStage()
    const sourceUrl = 'https://example.com/mock-fetch-failure-' + crypto.randomUUID()

    await submitUrl(host, sourceUrl)
    const manualButton = Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find(button => button.textContent?.includes('본문 직접 입력'))!
    manualButton.click()
    await flush()
    setValue(host.querySelector<HTMLTextAreaElement>('#manual-body')!, '보존할 수동 입력')

    const fetchButton = host.querySelector<HTMLButtonElement>('[data-testid="external-fetch-start"]')!
    fetchButton.click()
    await flush()
    await flush()

    expect(host.querySelector('[role="alert"]')?.textContent).toContain('원격 문서를 가져오지 못했습니다')
    expect(host.textContent).toContain('가져오기 실패')
    expect(host.textContent).not.toContain('FAILED')
    expect(host.querySelector<HTMLTextAreaElement>('#manual-body')?.value).toBe('보존할 수동 입력')

    fetchButton.click()
    await flush()
    await flush()
    expect(host.textContent).toContain('가져오기 완료')
    expect(host.textContent).not.toContain('SUCCEEDED')

    fetchButton.click()
    await flush()
    await flush()
    expect((await api.latestExternalFetch((await api.createUrlIntake(sourceUrl)).id))!.attemptNumber).toBe(3)

    fetchButton.click()
    await flush()
    await flush()
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('최대 3회')
    expect((await api.latestExternalFetch((await api.createUrlIntake(sourceUrl)).id))!.attemptNumber).toBe(3)
    expect(host.querySelector<HTMLTextAreaElement>('#manual-body')?.value).toBe('보존할 수동 입력')

    host.querySelector<HTMLButtonElement>('[data-testid="external-fetch-apply"]')!.click()
    await flush()
    await flush()
    expect(host.textContent).toContain('적용 완료')
    expect(host.textContent).not.toContain('APPLIED')
    app.unmount()
  })

  it('keeps Instagram manual-only and does not expose external fetch controls', async () => {
    const { host, app } = mountStage()
    await submitUrl(host, 'https://www.instagram.com/p/manual-only-' + crypto.randomUUID() + '/')

    expect(host.querySelector('[data-testid="external-fetch-start"]')).toBeNull()
    expect(host.querySelector('#manual-caption')).toBeTruthy()
    app.unmount()
  })
})
