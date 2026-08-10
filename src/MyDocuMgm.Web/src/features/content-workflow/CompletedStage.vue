<script setup lang="ts">
import { ref, watch } from 'vue'
import type { BlogDraft } from '../../shared/types'

const props = defineProps<{ stage?: BlogDraft }>()
const emit = defineEmits<{ close: [] }>()
const failedMediaIds = ref(new Set<string>())

watch(() => props.stage?.contentId, () => {
  failedMediaIds.value = new Set()
})

function markMediaFailed(id: string) {
  failedMediaIds.value = new Set(failedMediaIds.value).add(id)
}
</script>

<template>
  <section v-if="!stage" class="surface completion-card empty-state" aria-busy="true" data-testid="completion-loading">
    <h2>완료 기록을 불러오는 중입니다</h2>
    <p>저장된 콘텐츠와 블로그 초안을 읽기 전용으로 확인하고 있습니다.</p>
  </section>

  <section v-else class="surface completion-card" aria-labelledby="completion-title" data-testid="completion-stage">
    <header class="completion-header">
      <div class="completion-mark" aria-hidden="true">✓</div>
      <div>
        <span class="eyebrow">7단계 · 완료</span>
        <h2 id="completion-title">생활 기록 정리가 끝났습니다</h2>
        <p>이 기록은 내 PC에 저장된 읽기 전용 결과입니다. 외부에 게시하거나 전송하지 않았습니다.</p>
      </div>
    </header>

    <section class="completion-section" aria-labelledby="completion-content-title">
      <h3 id="completion-content-title">최종 자료</h3>
      <dl class="completion-summary">
        <div><dt>콘텐츠 제목</dt><dd data-testid="completion-content-name">{{ stage.analysisTitle }}</dd></div>
        <div><dt>분류</dt><dd>{{ stage.categoryDisplayName }}</dd></div>
        <div class="wide"><dt>요약</dt><dd>{{ stage.shortSummary || '입력된 요약 없음' }}</dd></div>
      </dl>
    </section>

    <section class="completion-section" aria-labelledby="completion-draft-title">
      <h3 id="completion-draft-title">저장된 블로그 초안</h3>
      <div class="completion-draft">
        <strong data-testid="completion-draft-name">{{ stage.title }}</strong>
        <p data-testid="completion-draft-body">{{ stage.body }}</p>
      </div>
    </section>

    <section class="completion-section" aria-labelledby="completion-media-title">
      <h3 id="completion-media-title">연결 이미지</h3>
      <div v-if="stage.linkedMedia.length" class="completion-media-grid">
        <figure v-for="item in stage.linkedMedia" :key="item.id">
          <img
            v-if="item.thumbnailUrl && !failedMediaIds.has(item.id)"
            :src="item.thumbnailUrl"
            :alt="item.originalFileName"
            @error="markMediaFailed(item.id)"
          >
          <div v-else class="neutral-media-placeholder" role="img" :aria-label="`${item.originalFileName} 미리보기 없음`" />
          <figcaption>{{ item.originalFileName }}</figcaption>
        </figure>
      </div>
      <p v-else class="empty-inline">연결된 이미지 없음</p>
    </section>

    <footer class="completion-actions">
      <button class="button primary" @click="emit('close')">작업목록으로</button>
    </footer>
  </section>
</template>
