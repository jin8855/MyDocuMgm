<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import LocalMediaLinkPicker from '../url-intake/LocalMediaLinkPicker.vue'
import { ApiError, api } from '../../shared/api/client'
import type { LinkableMediaPage, UrlIntake } from '../../shared/types'

const props = defineProps<{ contentId: string; newWork?: boolean }>()
const emit = defineEmits<{ dirty: []; saved: []; accepted: [contentId: string] }>()

const url = ref('')
const intake = ref<UrlIntake>()
const manualBody = ref('')
const manualVisible = ref(false)
const busy = ref(false)
const loading = ref(false)
const error = ref('')
const errorScope = ref<'url' | 'manual' | 'media' | 'load'>('url')
const notice = ref('')
const library = ref<LinkableMediaPage>({ items: [], totalCount: 0, page: 1, pageSize: 24, totalPages: 1 })
const selectedMediaIds = ref(new Set<string>())

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
  error.value = ''
  notice.value = ''
  selectedMediaIds.value = new Set()
  library.value = { items: [], totalCount: 0, page: 1, pageSize: 24, totalPages: 1 }
  if (props.newWork) return
  loading.value = true
  try {
    const value = await api.urlIntake(props.contentId)
    applyIntake(value)
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
    const value = await api.createUrlIntake(url.value)
    applyIntake(value)
    notice.value = value.isDuplicate
      ? '이미 등록된 URL입니다. 기존 작업을 불러왔습니다.'
      : 'URL을 저장했습니다. 외부 수집은 실행하지 않고 수집 대기 상태로 보관합니다.'
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
        <h2 id="url-intake-title">원본 URL</h2>
        <p>URL을 저장하고 중복을 확인합니다. 외부 사이트 접속이나 자동 수집은 실행하지 않습니다.</p>
      </div>
      <span v-if="intake" class="status-badge">{{ statusLabel }}</span>
    </div>

    <form class="url-intake-form" @submit.prevent="submitUrl">
      <label for="source-url">URL</label>
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
          placeholder="https://example.com/content"
          @input="emit('dirty')"
        >
        <button class="button primary" type="submit" :disabled="busy || loading">
          {{ busy ? '저장 중…' : 'URL 추가' }}
        </button>
      </div>
      <small id="url-intake-help">absolute http/https URL만 허용하며 URL 존재 여부는 확인하지 않습니다.</small>
    </form>

    <p v-if="loading" class="muted" role="status">저장된 URL 상태를 불러오는 중입니다.</p>
    <p v-if="error" id="url-intake-error" class="media-error" role="alert">{{ error }}</p>
    <p v-if="notice" class="media-success" role="status">{{ notice }}</p>

    <dl v-if="intake" class="intake-summary">
      <div><dt>정규화 URL</dt><dd>{{ intake.normalizedUrl }}</dd></div>
      <div><dt>출처 유형</dt><dd>{{ intake.sourceKind === 'INSTAGRAM' ? 'Instagram' : '일반 URL' }}</dd></div>
      <div><dt>접수 상태</dt><dd>{{ statusLabel }}</dd></div>
    </dl>

    <div v-if="intake" class="manual-body-panel">
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
      @toggle="toggleMedia"
      @save="saveMediaLinks"
      @page="loadLibrary"
    />
  </section>
</template>
