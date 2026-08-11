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

const presentation = computed(() => {
  if (props.intake.sourceKind === 'GENERIC' &&
    props.intake.sourceAcquisitionMode === 'HTTP_METADATA') {
    return {
      badge: 'URL 분석 검토',
      heading: '웹페이지 내용을 확인하고 제목과 요약을 작성하세요.',
      sourceType: '일반 웹 자료',
      acquisitionMode: 'URL 자동 수집',
      guide: '웹페이지에서 가져온 제목과 설명을 검토하고 필요한 내용을 수정하세요.',
      showManualInstagram: false,
    }
  }

  if (props.intake.sourceKind === 'INSTAGRAM' &&
    props.intake.sourceAcquisitionMode === 'MANUAL') {
    return {
      badge: '수동 분석 검토',
      heading: '원문을 확인하고 제목과 요약을 작성하세요.',
      sourceType: props.intake.instagramContentType === 'POST'
        ? 'Instagram 게시물'
        : props.intake.instagramContentType === 'REEL'
          ? 'Instagram 릴스'
          : 'Instagram 자료',
      acquisitionMode: '수동 입력',
      guide: 'AI 분석이나 외부 수집 없이 사용자가 입력한 자료만 표시합니다.',
      showManualInstagram: true,
    }
  }

  return {
    badge: '분석 검토',
    heading: '자료 내용을 확인하고 제목과 요약을 작성하세요.',
    sourceType: '기타 자료',
    acquisitionMode: '확인 필요',
    guide: '자료 출처와 입력 방식을 확인하고 필요한 내용을 수정하세요.',
    showManualInstagram: false,
  }
})

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
      <span class="badge accent">{{ presentation.badge }}</span>
      <h2>{{ presentation.heading }}</h2>
      <p class="notice">{{ presentation.guide }}</p>
    </div>

    <dl class="intake-summary">
      <div><dt>자료 유형</dt><dd>{{ presentation.sourceType }}</dd></div>
      <div><dt>입력 방식</dt><dd>{{ presentation.acquisitionMode }}</dd></div>
      <div><dt>연결 이미지</dt><dd>{{ intake.linkedMediaIds.length }}개</dd></div>
      <div><dt>원본 URL</dt><dd>{{ intake.normalizedUrl }}</dd></div>
    </dl>

    <article v-if="presentation.showManualInstagram" class="manual-instagram-panel">
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
