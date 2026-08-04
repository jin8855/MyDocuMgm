import { effectScope } from 'vue'
import { createNewWorkPath, isNewWorkId } from '../../src/MyDocuMgm.Web/src/app/router'
import { useMediaState } from '../../src/MyDocuMgm.Web/src/features/media-management/useMediaState'
import { api } from '../../src/MyDocuMgm.Web/src/shared/api/client'
import type { MediaPage } from '../../src/MyDocuMgm.Web/src/shared/types'

const emptyPage = (): MediaPage => ({
  items: [],
  totalCount: 0,
  selectedCount: 0,
  duplicateCount: 0,
  deletedCount: 0,
  page: 1,
  pageSize: 24,
  totalPages: 1,
})

describe('new work state boundary', () => {
  it('creates a new draft identity for every new-work action', () => {
    const first = createNewWorkPath()
    const second = createNewWorkPath()

    expect(first).not.toBe(second)
    expect(first).toMatch(/^\/workflow\/new-[0-9a-f-]+\/url$/)
    expect(isNewWorkId(first.split('/')[2])).toBe(true)
    expect(isNewWorkId('demo')).toBe(false)
  })

  it('keeps a late media response from repopulating reset state', async () => {
    let resolveRequest!: (value: MediaPage) => void
    const delayed = new Promise<MediaPage>((resolve) => {
      resolveRequest = resolve
    })
    const mediaSpy = vi.spyOn(api, 'media').mockReturnValueOnce(delayed)
    const scope = effectScope()
    const state = scope.run(() => useMediaState(() => 'demo'))!

    const loading = state.load()
    state.reset()
    resolveRequest({
      ...emptyPage(),
      items: [{
        id: 'old-media',
        originalFileName: 'old.png',
        thumbnailUrl: '',
        mimeType: 'image/png',
        sizeBytes: 1,
        width: 1,
        height: 1,
        sortOrder: 1,
        sourceTimestampMs: null,
        isSelected: true,
        isPublicAllowed: false,
        description: 'old work',
        storageStatus: 'READY',
        sha256: 'OLD',
        isDeleted: false,
        deletedAtUtc: null,
        rowVersion: 'AAAA',
      }],
      totalCount: 1,
      selectedCount: 1,
    })
    await loading

    expect(state.result.value).toEqual(emptyPage())
    expect(state.activeId.value).toBeUndefined()
    expect(state.uploadResult.value).toBe('')
    expect(state.operationError.value).toBe('')

    scope.stop()
    mediaSpy.mockRestore()
  })
})
