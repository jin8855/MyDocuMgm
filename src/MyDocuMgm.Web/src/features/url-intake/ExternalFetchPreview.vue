<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { ExternalFetchAttempt } from '../../shared/types'

const props = defineProps<{
  attempt?: ExternalFetchAttempt
  busy: boolean
  error: string
}>()
const emit = defineEmits<{
  fetch: []
  apply: [value: { title: string; description: string; body: string }]
  manual: []
  dirty: []
}>()

const title = ref('')
const description = ref('')
const body = ref('')
const statusLabels: Record<ExternalFetchAttempt['status'], string> = {
  STARTED: '가져오는 중',
  SUCCEEDED: '가져오기 완료',
  FAILED: '가져오기 실패',
  CANCELLED: '가져오기 취소',
  APPLIED: '적용 완료',
}
const statusLabel = computed(() => props.attempt ? statusLabels[props.attempt.status] : '')

watch(() => props.attempt, value => {
  title.value = value?.title ?? ''
  description.value = value?.description ?? ''
  body.value = value?.body ?? ''
}, { immediate: true })

const canApply = computed(() =>
  props.attempt?.status === 'SUCCEEDED' &&
  Boolean(body.value.trim()) &&
  !props.busy)

function changed() {
  emit('dirty')
}

function apply() {
  if (!canApply.value) return
  emit('apply', {
    title: title.value,
    description: description.value,
    body: body.value,
  })
}
</script>

<template>
  <section class="external-fetch-preview form-stack" aria-labelledby="external-fetch-title">
    <div class="section-title compact">
      <div>
        <h3 id="external-fetch-title">외부 웹페이지 텍스트 미리보기</h3>
        <p>가져오기는 미리보기만 만들며 적용 전까지 자료를 변경하지 않습니다. HTML 원문과 원격 이미지는 저장하지 않습니다.</p>
      </div>
      <span v-if="attempt" class="status-badge">{{ statusLabel }}</span>
    </div>

    <div class="inline-actions">
      <button class="button" type="button" :disabled="busy" data-testid="external-fetch-start" @click="emit('fetch')">
        {{ busy ? '가져오는 중' : attempt ? '다시 가져오기' : '미리보기 가져오기' }}
      </button>
      <button class="button" type="button" :disabled="busy" @click="emit('manual')">
        본문 직접 입력
      </button>
    </div>

    <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
    <p v-if="attempt?.status === 'FAILED' && !error" class="inline-error" role="alert">
      {{ attempt.errorMessage || '가져오기에 실패했습니다. 잠시 후 다시 시도할 수 있습니다.' }}
    </p>

    <template v-if="attempt && ['SUCCEEDED', 'APPLIED'].includes(attempt.status)">
      <dl class="intake-summary">
        <div><dt>최종 URL</dt><dd>{{ attempt.finalUrl }}</dd></div>
        <div><dt>응답</dt><dd>{{ attempt.httpStatusCode }} · {{ attempt.responseMimeType }}</dd></div>
        <div><dt>크기</dt><dd>{{ attempt.responseBytes?.toLocaleString() }} bytes</dd></div>
      </dl>

      <label for="external-fetch-title-value">제목</label>
      <input
        id="external-fetch-title-value"
        v-model="title"
        maxlength="200"
        :disabled="busy || attempt.status === 'APPLIED'"
        @input="changed"
      >

      <label for="external-fetch-description">요약</label>
      <textarea
        id="external-fetch-description"
        v-model="description"
        rows="3"
        maxlength="500"
        :disabled="busy || attempt.status === 'APPLIED'"
        @input="changed"
      />

      <label for="external-fetch-body">본문 미리보기</label>
      <textarea
        id="external-fetch-body"
        v-model="body"
        rows="10"
        maxlength="20000"
        :disabled="busy || attempt.status === 'APPLIED'"
        @input="changed"
      />
      <small>최대 20,000자이며 적용 버튼을 누르기 전에는 현재 자료를 변경하지 않습니다.</small>

      <div class="inline-actions">
        <button
          class="button primary"
          type="button"
          :disabled="!canApply"
          data-testid="external-fetch-apply"
          @click="apply"
        >
          {{ attempt.status === 'APPLIED' ? '적용 완료' : '이 미리보기 적용' }}
        </button>
      </div>
    </template>
  </section>
</template>
