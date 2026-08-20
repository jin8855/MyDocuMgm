<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { categoryFields } from '../category-editing/categoryFields'
import type { DetailStage } from '../../shared/types'
import HelpPopover from '../../shared/components/HelpPopover.vue'

const props = defineProps<{
  stage: DetailStage
  saving: boolean
  error: string
  notice: string
}>()

const brokenMediaIds = ref(new Set<string>())
const fields = computed(() => categoryFields[props.stage.categoryCode] ?? [])
const sourceLabel = computed(() => props.stage.sourceKind === 'INSTAGRAM' ? 'Instagram 수동 입력' : '일반 URL 입력')
const instagramTypeLabel = computed(() => props.stage.instagramContentType === 'REEL' ? '릴스' : '게시물')
const pinnedCommentLabel = computed(() => props.stage.pinnedAuthorCommentState === 'PRESENT'
  ? display(props.stage.pinnedAuthorCommentText)
  : props.stage.pinnedAuthorCommentState === 'NONE' ? '고정된 작성자 댓글 없음' : '입력된 내용 없음')

watch(() => props.stage.contentId, () => { brokenMediaIds.value = new Set() })

function display(value: string | null | undefined) {
  return value?.trim() || '입력된 내용 없음'
}

function fieldLabel(key: string) {
  return fields.value.find(field => field.key === key)?.label ?? key
}

function markBroken(mediaId: string) {
  brokenMediaIds.value = new Set(brokenMediaIds.value).add(mediaId)
}

function formatBytes(value: number) {
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${Math.round(value / 1024)} KB`
  return `${(value / 1024 / 1024).toFixed(1)} MB`
}
</script>

<template>
  <section
    class="detail-review-stage"
    data-testid="detail-review-stage"
    :aria-busy="saving"
  >
    <header class="surface detail-review-intro">
      <div class="title-with-help">
        <h2>저장 전 통합 검토</h2>
        <HelpPopover label="자료 상세 도움말">앞 단계에서 확정한 정보를 한곳에서 확인한 뒤 블로그 초안으로 이동합니다.</HelpPopover>
      </div>
      <span class="badge accent">{{ stage.categoryDisplayName }}</span>
    </header>

    <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
    <p v-if="notice" class="notice" role="status">{{ notice }}</p>
    <p class="notice detail-readonly-notice">
      현재 스키마에는 자료 상세 단계가 새로 편집하는 전용 필드가 없습니다. 아래 정보는 이전 단계의 읽기 전용 결과이며 이 단계에서 변경되지 않습니다.
    </p>

    <section class="surface detail-review-section" aria-labelledby="detail-source-title">
      <div class="section-title compact">
        <div><span class="eyebrow">입력 자료</span><h3 id="detail-source-title">URL·원문 확인</h3></div>
        <span class="status-badge">{{ sourceLabel }}</span>
      </div>
      <dl class="detail-review-grid">
        <div><dt>원본 URL</dt><dd class="detail-source-url">{{ display(stage.originalUrl) }}</dd></div>
        <div><dt>정규화 URL</dt><dd class="detail-source-url">{{ display(stage.normalizedUrl) }}</dd></div>
        <div v-if="stage.sourceKind === 'INSTAGRAM'"><dt>콘텐츠 유형</dt><dd>{{ instagramTypeLabel }}</dd></div>
        <div><dt>Caption</dt><dd>{{ display(stage.manualCaption) }}</dd></div>
        <div><dt>고정된 작성자 댓글</dt><dd>{{ pinnedCommentLabel }}</dd></div>
        <div><dt>직접 입력 본문</dt><dd>{{ display(stage.manualBody) }}</dd></div>
      </dl>
    </section>

    <section class="surface detail-review-section" aria-labelledby="detail-analysis-title">
      <div class="section-title compact"><div><span class="eyebrow">분석 검토</span><h3 id="detail-analysis-title">제목·요약</h3></div></div>
      <dl class="detail-review-grid">
        <div><dt>제목</dt><dd>{{ display(stage.title) }}</dd></div>
        <div><dt>요약</dt><dd>{{ display(stage.shortSummary) }}</dd></div>
      </dl>
    </section>

    <section class="surface detail-review-section" aria-labelledby="detail-category-title">
      <div class="section-title compact"><div><span class="eyebrow">분류별 편집</span><h3 id="detail-category-title">{{ stage.categoryDisplayName }} 상세</h3></div></div>
      <dl v-if="Object.keys(stage.categoryValues).length" class="detail-review-grid detail-category-grid">
        <div v-for="(value, key) in stage.categoryValues" :key="key">
          <dt>{{ fieldLabel(key) }}</dt><dd>{{ display(value) }}</dd>
        </div>
      </dl>
      <p v-else class="empty-compact" data-testid="detail-empty-category">입력된 분류 상세가 없습니다.</p>
      <div v-if="stage.categoryCode === 'COOKING'" class="detail-ingredient-panel">
        <h4>재료</h4>
        <ol v-if="stage.ingredients.length" class="detail-ingredient-list">
          <li v-for="ingredient in stage.ingredients" :key="ingredient.id">
            <strong>{{ ingredient.name }}</strong>
            <span>{{ display(ingredient.quantity) }}</span>
            <small>{{ ingredient.ingredientType }}{{ ingredient.isPrimary ? ' · 주재료' : '' }}</small>
          </li>
        </ol>
        <p v-else class="empty-compact">입력된 재료가 없습니다.</p>
      </div>
    </section>

    <section class="surface detail-review-section" aria-labelledby="detail-media-title">
      <div class="section-title compact"><div><span class="eyebrow">이미지</span><h3 id="detail-media-title">연결 이미지</h3></div><span>{{ stage.linkedMedia.length }}개</span></div>
      <div v-if="stage.linkedMedia.length" class="detail-media-grid">
        <article v-for="media in stage.linkedMedia" :key="media.id" class="detail-media-card">
          <img
            v-if="media.thumbnailUrl && !brokenMediaIds.has(media.id)"
            :src="media.thumbnailUrl"
            :alt="`${media.originalFileName} 미리보기`"
            @error="markBroken(media.id)"
          >
          <div v-else class="thumbnail-image detail-media-placeholder">미리보기 없음</div>
          <strong>{{ media.originalFileName }}</strong>
          <small>{{ media.width }}×{{ media.height }} · {{ formatBytes(media.sizeBytes) }}</small>
        </article>
      </div>
      <p v-else class="empty-compact" data-testid="detail-empty-media">연결된 이미지가 없습니다.</p>
    </section>
  </section>
</template>
