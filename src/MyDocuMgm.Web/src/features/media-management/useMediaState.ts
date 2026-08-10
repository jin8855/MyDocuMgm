import { computed, onBeforeUnmount, ref } from 'vue'
import { ApiError, api } from '../../shared/api/client'
import type { MediaFilter, MediaItem, MediaPage, MediaSort } from '../../shared/types'

const allowedTypes = new Set(['image/jpeg', 'image/png', 'image/webp'])
const allowedExtensions = /\.(jpe?g|png|webp)$/i
const maxFileBytes = 20 * 1024 * 1024

function emptyMediaPage(): MediaPage {
  return {
    items: [],
    totalCount: 0,
    selectedCount: 0,
    duplicateCount: 0,
    deletedCount: 0,
    page: 1,
    pageSize: 24,
    totalPages: 1,
  }
}

const errorMessages: Record<string, string> = {
  MEDIA_EXTENSION_NOT_ALLOWED: 'JPEG, PNG, WebP 파일만 선택할 수 있습니다.',
  MEDIA_MIME_MISMATCH: '파일 확장자와 이미지 형식이 일치하지 않습니다.',
  MEDIA_SIGNATURE_INVALID: '이미지 파일 형식을 확인할 수 없습니다.',
  MEDIA_DECODE_FAILED: '손상되었거나 디코딩할 수 없는 이미지입니다.',
  MEDIA_FILE_TOO_LARGE: '이미지는 20 MiB 이하만 등록할 수 있습니다.',
  MEDIA_DIMENSION_TOO_LARGE: '이미지 가로 또는 세로 크기가 허용 한도를 초과합니다.',
  MEDIA_PIXEL_COUNT_TOO_LARGE: '이미지 전체 픽셀 수가 허용 한도를 초과합니다.',
  MEDIA_OPERATION_CANCELLED: '업로드를 취소했습니다.',
  MEDIA_STORAGE_NOT_READY: '로컬 이미지 저장소가 준비되지 않았습니다.',
  MEDIA_CONCURRENCY_CONFLICT: '다른 변경이 먼저 저장되었습니다. 목록을 새로 불러왔습니다.',
  MEDIA_RESTORE_BLOCKED: '원본 이미지 무결성을 확인할 수 없어 복원하지 않았습니다.',
  MEDIA_FILE_MISSING: '저장된 원본 이미지를 찾을 수 없습니다.',
  MEDIA_INTEGRITY_HASH_MISMATCH: '저장된 원본 이미지의 무결성이 일치하지 않습니다.',
}

function operationMessage(error: unknown): string {
  if (error instanceof ApiError) return errorMessages[error.code] ?? error.message
  return error instanceof Error ? error.message : '이미지 작업을 완료하지 못했습니다.'
}

export function useMediaState(contentId: () => string) {
  const filter = ref<MediaFilter>('ALL')
  const sort = ref<MediaSort>('TIME_ASC')
  const page = ref(1)
  const pageSize = ref(24)
  const result = ref<MediaPage>(emptyMediaPage())
  const activeId = ref<string>()
  const uploadFile = ref<File>()
  const uploadPreviewUrl = ref('')
  const uploadProgress = ref(0)
  const uploading = ref(false)
  const uploadResult = ref('')
  const operationError = ref('')
  let uploadController: AbortController | undefined
  let stateVersion = 0
  let loadVersion = 0

  const activeItem = computed(() => result.value.items.find((item) => item.id === activeId.value))

  async function load() {
    const requestStateVersion = stateVersion
    const requestLoadVersion = ++loadVersion
    const nextResult = await api.media(contentId(), filter.value, sort.value, page.value, pageSize.value)
    if (requestStateVersion !== stateVersion || requestLoadVersion !== loadVersion) return
    result.value = nextResult
    if (activeId.value && !result.value.items.some((item) => item.id === activeId.value)) {
      activeId.value = undefined
    }
  }

  async function run(operation: () => Promise<void>) {
    const operationStateVersion = stateVersion
    operationError.value = ''
    try {
      await operation()
    } catch (error) {
      if (operationStateVersion !== stateVersion) return
      operationError.value = operationMessage(error)
      if (error instanceof ApiError && error.code === 'MEDIA_CONCURRENCY_CONFLICT') await load()
    }
  }

  async function setFilter(value: MediaFilter) {
    filter.value = value
    page.value = 1
    await load()
  }
  async function setSort(value: MediaSort) {
    sort.value = value
    page.value = 1
    await load()
  }
  async function setPageSize(value: number) {
    pageSize.value = value
    page.value = 1
    await load()
  }
  async function setPage(value: number) {
    page.value = value
    await load()
  }
  function select(item: MediaItem) {
    activeId.value = item.id
    operationError.value = ''
  }
  async function toggle(item: MediaItem) {
    await run(async () => {
      item.isSelected = !item.isSelected
      await api.updateMedia(contentId(), item)
      await load()
    })
  }
  async function save(item: MediaItem) {
    await run(async () => {
      await api.updateMedia(contentId(), item)
      await load()
      activeId.value = item.id
    })
  }
  async function remove(item: MediaItem) {
    await run(async () => {
      await api.deleteMedia(contentId(), item)
      activeId.value = undefined
      await load()
    })
  }
  async function restore(item: MediaItem) {
    await run(async () => {
      await api.restoreMedia(contentId(), item)
      activeId.value = undefined
      await load()
    })
  }
  async function move(item: MediaItem, direction: -1 | 1) {
    await run(async () => {
      await api.moveMedia(contentId(), item, direction)
      await load()
      activeId.value = item.id
    })
  }

  function clearPreview() {
    if (uploadPreviewUrl.value) URL.revokeObjectURL(uploadPreviewUrl.value)
    uploadPreviewUrl.value = ''
    uploadFile.value = undefined
  }

  async function upload(file: File) {
    const uploadStateVersion = stateVersion
    operationError.value = ''
    uploadResult.value = ''
    if (!allowedTypes.has(file.type) || !allowedExtensions.test(file.name)) {
      operationError.value = errorMessages.MEDIA_EXTENSION_NOT_ALLOWED
      return
    }
    if (file.size > maxFileBytes) {
      operationError.value = errorMessages.MEDIA_FILE_TOO_LARGE
      return
    }

    clearPreview()
    uploadFile.value = file
    uploadPreviewUrl.value = URL.createObjectURL(file)
    uploadProgress.value = 0
    uploading.value = true
    uploadController = new AbortController()
    try {
      const uploaded = await api.uploadMedia(
        contentId(),
        file,
        (value) => { uploadProgress.value = value },
        uploadController.signal,
      )
      if (uploadStateVersion !== stateVersion) return
      uploadProgress.value = 100
      uploadResult.value = uploaded.reused
        ? '같은 콘텐츠의 동일한 원본을 재사용했습니다.'
        : '로컬 이미지 저장을 완료했습니다.'
      filter.value = 'ALL'
      page.value = 1
      await load()
      activeId.value = uploaded.item.id
      return uploaded
    } catch (error) {
      if (uploadStateVersion !== stateVersion) return
      operationError.value = operationMessage(error)
    } finally {
      if (uploadStateVersion === stateVersion) {
        uploading.value = false
        uploadController = undefined
      }
    }
  }

  function cancelUpload() {
    uploadController?.abort()
  }

  function reset() {
    stateVersion += 1
    loadVersion += 1
    uploadController?.abort()
    uploadController = undefined
    clearPreview()
    filter.value = 'ALL'
    sort.value = 'TIME_ASC'
    page.value = 1
    pageSize.value = 24
    result.value = emptyMediaPage()
    activeId.value = undefined
    uploadProgress.value = 0
    uploading.value = false
    uploadResult.value = ''
    operationError.value = ''
  }

  onBeforeUnmount(reset)

  return {
    filter,
    sort,
    page,
    pageSize,
    result,
    activeId,
    activeItem,
    uploadFile,
    uploadPreviewUrl,
    uploadProgress,
    uploading,
    uploadResult,
    operationError,
    load,
    setFilter,
    setSort,
    setPageSize,
    setPage,
    select,
    toggle,
    save,
    remove,
    restore,
    move,
    upload,
    cancelUpload,
    clearPreview,
    reset,
  }
}
