import { ApiError, api } from '../../src/MyDocuMgm.Web/src/shared/api/client'
import { effectScope } from 'vue'
import { useMediaState } from '../../src/MyDocuMgm.Web/src/features/media-management/useMediaState'

describe('Phase 1B media web contract', () => {
  beforeAll(() => {
    Object.defineProperty(URL, 'createObjectURL', {
      configurable: true,
      value: () => 'blob:phase1b-preview',
    })
    Object.defineProperty(URL, 'revokeObjectURL', {
      configurable: true,
      value: () => undefined,
    })
  })

  it('uploads, exposes progress, soft deletes, lists trash, and restores', async () => {
    const progress: number[] = []
    const file = new File([new Uint8Array([137, 80, 78, 71])], 'phase1b-unique.png', {
      type: 'image/png',
    })

    const uploaded = await api.uploadMedia(
      'demo',
      file,
      (value) => progress.push(value),
      new AbortController().signal,
    )

    expect(uploaded.reused).toBe(false)
    expect(progress).toEqual([20, 55, 85, 100])
    expect(uploaded.item.thumbnailUrl).toMatch(/^data:image\/png;base64,/)

    await api.deleteMedia('demo', uploaded.item)
    const normal = await api.media('demo', 'ALL', 'TIME_ASC', 1, 96)
    const deleted = await api.media('demo', 'DELETED', 'TIME_ASC', 1, 96)
    expect(normal.items.some((item) => item.id === uploaded.item.id)).toBe(false)
    expect(deleted.items.some((item) => item.id === uploaded.item.id)).toBe(true)
    expect(deleted.deletedCount).toBeGreaterThan(0)

    const deletedItem = deleted.items.find((item) => item.id === uploaded.item.id)!
    await api.restoreMedia('demo', deletedItem)
    const restored = await api.media('demo', 'ALL', 'TIME_ASC', 1, 96)
    expect(restored.items.some((item) => item.id === uploaded.item.id)).toBe(true)
  })

  it('returns a stable cancellation code', async () => {
    const controller = new AbortController()
    controller.abort()
    const file = new File([new Uint8Array([1])], 'cancel.png', { type: 'image/png' })

    await expect(api.uploadMedia('demo', file, () => undefined, controller.signal))
      .rejects.toMatchObject({ code: 'MEDIA_OPERATION_CANCELLED' } satisfies Partial<ApiError>)
  })

  it('owns and revokes the local preview URL and rejects invalid client input', async () => {
    const revoke = vi.spyOn(URL, 'revokeObjectURL')
    const scope = effectScope()
    const state = scope.run(() => useMediaState(() => 'demo'))!
    const image = new File([new Uint8Array([137, 80, 78, 71])], 'preview.png', {
      type: 'image/png',
    })

    await state.upload(image)
    expect(state.uploadPreviewUrl.value).toBe('blob:phase1b-preview')
    state.clearPreview()
    expect(revoke).toHaveBeenCalledWith('blob:phase1b-preview')

    await state.upload(new File([new Uint8Array([1])], 'unsafe.svg', { type: 'image/svg+xml' }))
    expect(state.operationError.value).toContain('JPEG, PNG, WebP')
    scope.stop()
    revoke.mockRestore()
  })
})
