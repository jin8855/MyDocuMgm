<script setup lang="ts">
import { computed } from 'vue'
import type { UrlIntake } from '../../shared/types'

const props = defineProps<{
  title: string
  shortSummary: string
  intake: UrlIntake
  error?: string
}>()

const emit = defineEmits<{
  dirty: []
  title: [value: string]
  summary: [value: string]
}>()

const contentTypeLabel = computed(() => props.intake.instagramContentType === 'POST'
  ? '게시물'
  : props.intake.instagramContentType === 'REEL'
    ? '릴스'
    : '확인 필요')

function updateTitle(event: Event) {
  emit('title', (event.target as HTMLInputElement).value)
  emit('dirty')
}

function updateSummary(event: Event) {
  emit('summary', (event.target as HTMLTextAreaElement).value)
  emit('dirty')
}
</script>

<template>
  <section class="surface review-grid" data-testid="manual-analysis-review">
    <div>
      <span class="badge accent">수동 분석 검토</span>
      <h2>원문을 확인하고 제목과 요약을 작성하세요</h2>
      <p class="notice">AI 분석이나 외부 수집 없이 사용자가 입력한 자료만 표시합니다.</p>
    </div>

    <dl class="intake-summary">
      <div><dt>자료 유형</dt><dd>Instagram {{ contentTypeLabel }}</dd></div>
      <div><dt>입력 방식</dt><dd>수동 입력</dd></div>
      <div><dt>연결 이미지</dt><dd>{{ intake.linkedMediaIds.length }}개</dd></div>
      <div><dt>원본 URL</dt><dd>{{ intake.normalizedUrl }}</dd></div>
    </dl>

    <article class="manual-instagram-panel">
      <h3>수동 입력 Caption</h3>
      <p class="empty-compact">{{ intake.manualCaption || '입력된 Caption이 없습니다.' }}</p>
      <h3>작성자 고정 댓글</h3>
      <p class="empty-compact">
        {{ intake.pinnedAuthorCommentState === 'PRESENT'
          ? intake.pinnedAuthorCommentText
          : '작성자 고정 댓글 없음' }}
      </p>
    </article>

    <label for="analysis-title">
      제목
      <input
        id="analysis-title"
        :value="title"
        maxlength="200"
        required
        autocomplete="off"
        @input="updateTitle"
      />
      <small>{{ title.length }} / 200</small>
    </label>

    <label for="analysis-summary">
      직접 작성한 요약
      <textarea
        id="analysis-summary"
        :value="shortSummary"
        maxlength="500"
        rows="6"
        @input="updateSummary"
      />
      <small>{{ shortSummary.length }} / 500</small>
    </label>

    <p v-if="error" class="media-error" role="alert">{{ error }}</p>
  </section>
</template>
