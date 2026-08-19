<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import LocalMediaLinkPicker from '../url-intake/LocalMediaLinkPicker.vue'
import ExternalFetchPreview from '../url-intake/ExternalFetchPreview.vue'
import MediaUploadPanel from '../media-management/MediaUploadPanel.vue'
import { useMediaState } from '../media-management/useMediaState'
import { ApiError, api } from '../../shared/api/client'
import type { ExternalFetchAttempt, LinkableMediaPage, PinnedAuthorCommentState, UrlIntake } from '../../shared/types'

const props = defineProps<{ contentId: string; newWork?: boolean }>()
const emit = defineEmits<{ dirty: []; saved: []; accepted: [contentId: string] }>()

const url = ref('')
const intake = ref<UrlIntake>()
const manualBody = ref('')
const manualVisible = ref(false)
const manualCaption = ref('')
const pinnedCommentState = ref<PinnedAuthorCommentState>('NONE')
const pinnedCommentText = ref('')
const busy = ref(false)
const fetchBusy = ref(false)
const fetchError = ref('')
const fetchAttempt = ref<ExternalFetchAttempt>()
const loading = ref(false)
const error = ref('')
const errorScope = ref<'url' | 'manual' | 'media' | 'load'>('url')
const notice = ref('')
const library = ref<LinkableMediaPage>({ items: [], totalCount: 0, page: 1, pageSize: 24, totalPages: 1 })
const selectedMediaIds = ref(new Set<string>())
const newlyUploadedMediaIds = new Set<string>()
const mediaState = useMediaState(() => intake.value?.id ?? props.contentId)

const statusLabel = computed(() => ({
  URL_ACCEPTED: '수집 대기',
  MANUAL_INPUT_REQUIRED: '본문 직접 입력 대기',
  CONTENT_READY: '본문 준비 완료',
}[intake.value?.status ?? 'URL_ACCEPTED']))

function message(errorValue: unknown): string {
  if (errorValue instanceof ApiError) return errorValue.message
  return errorValue instanceof Error ? errorValue.message : 'URL 접수 작업을 완료하지 못했습니다.'
}

function applyIntake(value: UrlIntake) {
  intake.value = value
  url.value = value.originalUrl
  manualBody.value = value.manualBody ?? ''
  manualVisible.value = value.status !== 'URL_ACCEPTED' || value.manualBodyPresent
  manualCaption.value = value.manualCaption ?? ''
  pinnedCommentState.value = value.pinnedAuthorCommentState ?? 'NONE'
  pinnedCommentText.value = value.pinnedAuthorCommentText ?? ''
  selectedMediaIds.value = new Set(value.linkedMediaIds)
}

async function loadLibrary(page = 1) {
  if (!intake.value) return
  library.value = await api.linkableMedia(page, library.value.pageSize)
}

async function load() {
  url.value = ''
  intake.value = undefined
  manualBody.value = ''
  manualVisible.value = false
  manualCaption.value = ''
  pinnedCommentState.value = 'NONE'
  pinnedCommentText.value = ''
  mediaState.reset()
  fetchAttempt.value = undefined
  fetchError.value = ''
  fetchBusy.value = false
  error.value = ''
  notice.value = ''
  selectedMediaIds.value = new Set()
  newlyUploadedMediaIds.clear()
  library.value = { items: [], totalCount: 0, page: 1, pageSize: 24, totalPages: 1 }
  if (props.newWork) return
  loading.value = true
  try {
    const value = await api.urlIntake(props.contentId)
    applyIntake(value)
    if (value.sourceKind === 'GENERIC') {
      try {
        fetchAttempt.value = await api.latestExternalFetch(value.id)
      } catch (latestError) {
        if (!(latestError instanceof ApiError) || latestError.code !== 'NOT_FOUND') {
          throw latestError
        }
      }
    }
    await loadLibrary()
  } catch (errorValue) {
    errorScope.value = 'load'
    error.value = message(errorValue)
  } finally {
    loading.value = false
  }
}

async function submitUrl() {
  if (busy.value) return
  error.value = ''
  notice.value = ''
  busy.value = true
  try {
    let instagram = false
    try {
      const parsed = new URL(url.value)
      instagram = ['instagram.com', 'www.instagram.com'].includes(parsed.hostname.toLowerCase())
    } catch {
      // The API/client normalizer returns the user-facing URL validation error.
    }
    const value = instagram
      ? await api.createInstagramIntake(url.value)
      : await api.createUrlIntake(url.value)
    applyIntake(value)
    notice.value = value.isDuplicate
      ? '이미 등록된 URL입니다. 기존 작업을 불러왔습니다.'
      : 'URL을 저장했습니다. 외부 수집은 실행하지 않고 수집 대기 상태로 보관합니다.'
    if (!value.isDuplicate) {
      notice.value = value.sourceKind === 'GENERIC'
        ? 'URL을 저장했습니다. 텍스트 미리보기를 실행한 뒤 적용 여부를 선택해 주세요.'
        : 'URL을 저장했습니다. Instagram 자료는 계속 수동으로 입력합니다.'
    }
    await loadLibrary()
    emit('saved')
    if (value.id !== props.contentId) emit('accepted', value.id)
  } catch (errorValue) {
    errorScope.value = 'url'
    error.value = message(errorValue)
  } finally {
    busy.value = false
  }
}

async function startExternalFetch() {
  if (!intake.value || fetchBusy.value) return
  fetchError.value = ''
  fetchBusy.value = true
  try {
    fetchAttempt.value = await api.startExternalFetch(intake.value.id)
  } catch (errorValue) {
    fetchError.value = message(errorValue)
    try {
      fetchAttempt.value = await api.latestExternalFetch(intake.value.id)
    } catch {
      // The primary error remains visible even when no attempt record is available.
    }
  } finally {
    fetchBusy.value = false
  }
}

async function applyExternalFetch(value: { title: string; description: string; body: string }) {
  if (!intake.value || !fetchAttempt.value || fetchBusy.value) return
  fetchError.value = ''
  fetchBusy.value = true
  try {
    const result = await api.applyExternalFetch(
      intake.value.id,
      fetchAttempt.value.id,
      value.title,
      value.description,
      value.body,
    )
    fetchAttempt.value = result.attempt
    applyIntake(result.intake)
    notice.value = '미리보기를 현재 자료에 적용했습니다.'
    emit('saved')
  } catch (errorValue) {
    fetchError.value = message(errorValue)
  } finally {
    fetchBusy.value = false
  }
}

async function beginManualInput() {
  if (!intake.value || busy.value) return
  error.value = ''
  busy.value = true
  try {
    applyIntake(await api.beginManualInput(intake.value.id))
    manualVisible.value = true
    emit('saved')
  } catch (errorValue) {
    errorScope.value = 'manual'
    error.value = message(errorValue)
  } finally {
    busy.value = false
  }
}

async function saveManualBody() {
  if (!intake.value || busy.value) return
  error.value = ''
  notice.value = ''
  busy.value = true
  try {
    applyIntake(await api.saveManualBody(intake.value.id, manualBody.value))
    notice.value = '직접 입력한 본문을 저장했습니다.'
    emit('saved')
  } catch (errorValue) {
    errorScope.value = 'manual'
    error.value = message(errorValue)
  } finally {
    busy.value = false
  }
}

function setPinnedCommentState(value: PinnedAuthorCommentState) {
  pinnedCommentState.value = value
  if (value === 'NONE') pinnedCommentText.value = ''
  emit('dirty')
}

async function uploadLocalMedia(file: File) {
  const uploaded = await mediaState.upload(file)
  if (!uploaded || mediaState.operationError.value) return
  await loadLibrary()
  const uploadedId = uploaded.item.id
  if (!uploaded.reused) newlyUploadedMediaIds.add(uploadedId)
  selectedMediaIds.value = new Set([...selectedMediaIds.value, uploadedId])
  emit('dirty')
}

async function compensateNewUploads(): Promise<boolean> {
  const ids = [...newlyUploadedMediaIds]
  const results = await Promise.allSettled(
    ids.map(id => api.permanentlyDeleteOrphanMedia(id)),
  )
  let complete = true
  results.forEach((result, index) => {
    if (result.status === 'fulfilled') {
      newlyUploadedMediaIds.delete(ids[index]!)
      selectedMediaIds.value.delete(ids[index]!)
    } else {
      complete = false
    }
  })
  selectedMediaIds.value = new Set(selectedMediaIds.value)
  try { await loadLibrary() } catch { complete = false }
  return complete
}

async function saveManualInstagram() {
  if (!intake.value || busy.value) return
  error.value = ''
  notice.value = ''
  busy.value = true
  try {
    applyIntake(await api.saveManualInstagram(
      intake.value.id,
      manualCaption.value,
      pinnedCommentState.value,
      pinnedCommentText.value,
      [...selectedMediaIds.value],
    ))
    newlyUploadedMediaIds.clear()
    notice.value = '수동으로 입력한 Instagram 자료와 로컬 이미지 연결을 저장했습니다.'
    emit('saved')
  } catch (errorValue) {
    errorScope.value = 'manual'
    const cleanupComplete = await compensateNewUploads()
    error.value = message(errorValue)
    if (!cleanupComplete) {
      error.value += ' 새로 업로드한 이미지 자동 정리에 실패했습니다. 휴지통·미디어 정리에서 확인하세요.'
    }
  } finally {
    busy.value = false
  }
}

function toggleMedia(id: string) {
  const next = new Set(selectedMediaIds.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  selectedMediaIds.value = next
  emit('dirty')
}

async function saveMediaLinks() {
  if (!intake.value || busy.value) return
  error.value = ''
  notice.value = ''
  busy.value = true
  try {
    applyIntake(await api.replaceLinkedMedia(intake.value.id, [...selectedMediaIds.value]))
    notice.value = '기존 로컬 이미지 연결을 저장했습니다. 원본 파일은 변경하지 않았습니다.'
    emit('saved')
  } catch (errorValue) {
    errorScope.value = 'media'
    error.value = message(errorValue)
  } finally {
    busy.value = false
  }
}

watch(() => [props.contentId, props.newWork], load, { immediate: true })
</script>

<template>
  <section class="surface form-stack url-intake" aria-labelledby="url-intake-title">
    <div class="section-title">
      <div>
        <h2 id="url-intake-title">URL 자료 등록</h2>
        <p>일반 공개 웹페이지는 텍스트 미리보기를 제공하며, Instagram은 수동 입력만 지원합니다.</p>
      </div>
      <span v-if="intake" class="status-badge">{{ statusLabel }}</span>
    </div>

    <form class="url-intake-form" @submit.prevent="submitUrl">
      <label for="source-url">공개 HTTP(S) URL</label>
      <div class="url-intake-row">
        <input
          id="source-url"
          v-model="url"
          type="url"
          inputmode="url"
          autocomplete="url"
          :disabled="busy || loading"
          :aria-invalid="Boolean(error) && errorScope === 'url'"
          :aria-describedby="error && errorScope === 'url' ? 'url-intake-help url-intake-error' : 'url-intake-help'"
          placeholder="https://example.com/article"
          @input="emit('dirty')"
        >
        <button class="button primary" type="submit" :disabled="busy || loading">
          {{ busy ? '저장 중…' : 'URL 추가' }}
        </button>
      </div>
      <small id="url-intake-help">일반 URL은 80/443 포트만 허용하고 적용 전 미리보기를 표시합니다. Instagram 게시물·Reel은 기존 수동 입력을 유지합니다.</small>
    </form>

    <p v-if="loading" class="muted" role="status">저장된 URL 상태를 불러오는 중입니다.</p>
    <p v-if="error" id="url-intake-error" class="media-error" role="alert">{{ error }}</p>
    <p v-if="notice" class="media-success" role="status">{{ notice }}</p>

    <dl v-if="intake" class="intake-summary">
      <div><dt>정규화 URL</dt><dd>{{ intake.normalizedUrl }}</dd></div>
      <div><dt>출처 유형</dt><dd>{{ intake.sourceKind === 'INSTAGRAM' ? 'Instagram' : '일반 URL' }}</dd></div>
      <div><dt>접수 상태</dt><dd>{{ statusLabel }}</dd></div>
    </dl>

    <div v-if="intake?.sourceKind === 'INSTAGRAM'" class="manual-instagram-panel form-stack">
      <div class="section-title compact">
        <div>
          <h3>수동 입력 내용</h3>
          <p>Caption과 작성자 고정 댓글은 Instagram에서 자동으로 가져오지 않습니다.</p>
        </div>
        <span class="status-badge">{{ intake.instagramContentType === 'POST' ? '게시물' : 'Reel' }}</span>
      </div>

      <label for="manual-caption">Caption 직접 입력</label>
      <textarea
        id="manual-caption"
        v-model="manualCaption"
        rows="6"
        maxlength="20000"
        :disabled="busy"
        :aria-invalid="Boolean(error) && errorScope === 'manual'"
        aria-describedby="manual-caption-help"
        @input="emit('dirty')"
      />
      <small id="manual-caption-help">사용자가 직접 확인한 Caption만 입력합니다. 자동 수집 결과가 아닙니다.</small>

      <fieldset class="pinned-comment-choice">
        <legend>게시물 작성자가 작성한 고정 댓글</legend>
        <label>
          <input
            type="radio"
            name="pinned-author-comment"
            value="PRESENT"
            :checked="pinnedCommentState === 'PRESENT'"
            :disabled="busy"
            @change="setPinnedCommentState('PRESENT')"
          >
          있음
        </label>
        <label>
          <input
            type="radio"
            name="pinned-author-comment"
            value="NONE"
            :checked="pinnedCommentState === 'NONE'"
            :disabled="busy"
            @change="setPinnedCommentState('NONE')"
          >
          없음
        </label>
      </fieldset>
      <small id="pinned-comment-help">
        게시물 작성자가 직접 작성하고 고정한 댓글을 입력하세요.
        여러 개라면 화면에서 가장 위에 표시되는 댓글 1개를 입력하세요.
      </small>
      <label for="pinned-author-comment-text">작성자 고정 댓글 본문</label>
      <textarea
        id="pinned-author-comment-text"
        v-model="pinnedCommentText"
        rows="4"
        maxlength="10000"
        :disabled="busy || pinnedCommentState === 'NONE'"
        :required="pinnedCommentState === 'PRESENT'"
        :aria-invalid="Boolean(error) && errorScope === 'manual'"
        aria-describedby="pinned-comment-help"
        @input="emit('dirty')"
      />

      <MediaUploadPanel
        :preview-url="mediaState.uploadPreviewUrl.value"
        :file-name="mediaState.uploadFile.value?.name"
        :progress="mediaState.uploadProgress.value"
        :uploading="mediaState.uploading.value"
        :result="mediaState.uploadResult.value"
        :error="mediaState.operationError.value"
        @upload="uploadLocalMedia"
        @cancel="mediaState.cancelUpload"
        @clear="mediaState.clearPreview"
      />
      <small>현재 승인된 로컬 업로드 형식은 JPEG, PNG, WebP입니다. 영상은 별도 저장 정책·decoder 승인 전까지 지원하지 않습니다.</small>

      <div class="inline-actions">
        <button class="button primary" type="button" :disabled="busy || mediaState.uploading.value" @click="saveManualInstagram">
          {{ busy ? '저장 중…' : '수동 등록 저장' }}
        </button>
      </div>
    </div>

    <ExternalFetchPreview
      v-if="intake?.sourceKind === 'GENERIC'"
      :attempt="fetchAttempt"
      :busy="fetchBusy"
      :error="fetchError"
      @fetch="startExternalFetch"
      @apply="applyExternalFetch"
      @manual="beginManualInput"
      @dirty="emit('dirty')"
    />

    <div v-if="intake && intake.sourceKind !== 'INSTAGRAM'" class="manual-body-panel">
      <div class="section-title compact">
        <div><h3>본문 직접 입력</h3><p>자동 수집 없이 사용자가 제공한 본문만 저장합니다.</p></div>
        <button
          v-if="!manualVisible"
          class="button"
          type="button"
          :disabled="busy"
          @click="beginManualInput"
        >본문 직접 입력</button>
      </div>
      <template v-if="manualVisible">
        <label for="manual-body">본문</label>
        <textarea
          id="manual-body"
          v-model="manualBody"
          rows="8"
          maxlength="20000"
          :disabled="busy"
          :aria-invalid="Boolean(error) && errorScope === 'manual'"
          :aria-describedby="error && errorScope === 'manual' ? 'manual-body-help url-intake-error' : 'manual-body-help'"
          @input="emit('dirty')"
        />
        <small id="manual-body-help">공백만 있는 본문은 저장되지 않습니다. 최대 20,000자이며 본문 전체를 로그에 기록하지 않습니다.</small>
        <div class="inline-actions">
          <button class="button" type="button" :disabled="busy" @click="manualVisible = false">접수 상태 보기</button>
          <button class="button primary" type="button" :disabled="busy" @click="saveManualBody">
            {{ busy ? '저장 중…' : '본문 저장' }}
          </button>
        </div>
      </template>
    </div>

    <LocalMediaLinkPicker
      v-if="intake"
      :page="library"
      :selected-ids="selectedMediaIds"
      :busy="busy"
      :show-save="intake.sourceKind !== 'INSTAGRAM'"
      @toggle="toggleMedia"
      @save="intake.sourceKind === 'INSTAGRAM' ? saveManualInstagram() : saveMediaLinks()"
      @page="loadLibrary"
    />
  </section>
</template>
