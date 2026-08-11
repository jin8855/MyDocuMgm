<script setup lang="ts">
import type { BlogDraft } from '../../shared/types'

defineProps<{
  stage?: BlogDraft
  title: string
  body: string
  saving: boolean
  error: string
  reuseConfirmed: boolean
  notice: string
}>()

const emit = defineEmits<{
  title: [value: string]
  body: [value: string]
  reuseConfirmation: [value: boolean]
  dirty: []
}>()

function updateTitle(event: Event) {
  emit('title', (event.target as HTMLInputElement).value)
  emit('dirty')
}

function updateBody(event: Event) {
  emit('body', (event.target as HTMLTextAreaElement).value)
  emit('dirty')
}

function updateReuseConfirmation(event: Event) {
  emit('reuseConfirmation', (event.target as HTMLInputElement).checked)
  emit('dirty')
}
</script>

<template>
  <section v-if="!stage" class="surface blog-preview empty-state" aria-busy="true" data-testid="blog-draft-loading">
    <h2>블로그 초안을 불러오는 중입니다</h2>
    <p>현재 콘텐츠에 저장된 초안이 있는지 확인하고 있습니다.</p>
  </section>

  <section v-else class="surface blog-preview blog-draft-form" data-testid="blog-draft-stage">
    <div class="section-title">
      <div>
        <h2>블로그 초안</h2>
        <p>내 PC에 보관되는 미공개 초안입니다. 외부 서비스에 게시하거나 전송하지 않습니다.</p>
      </div>
      <span class="badge">로컬 · 미공개</span>
    </div>

    <p class="blog-draft-state" data-testid="blog-draft-persistence-state">
      {{ stage.hasSavedDraft ? '저장된 초안을 불러왔습니다.' : '아직 저장되지 않은 최초 편집값입니다.' }}
    </p>
    <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
    <p v-if="notice" class="inline-notice" role="status">{{ notice }}</p>

    <label
      v-if="stage.requiresExternalSourceReuseConfirmation && !stage.externalSourceReuseConfirmed"
      class="blog-draft-field external-reuse-confirmation"
    >
      <span>외부 자료 재사용 확인</span>
      <span>
        <input
          type="checkbox"
          data-testid="external-reuse-confirmation"
          :checked="reuseConfirmed"
          :disabled="saving"
          @change="updateReuseConfirmation"
        >
        개인 보관용으로 가져온 외부 본문을 블로그 초안에 재사용할 권리를 확인했습니다.
      </span>
      <small>개인 자료 보관에는 확인이 필요하지 않지만 블로그 초안 재사용 전에는 명시적 확인이 필요합니다.</small>
    </label>

    <div class="blog-draft-context" aria-label="이전 단계 참고 정보">
      <div><span>분석 제목</span><strong>{{ stage.analysisTitle }}</strong></div>
      <div><span>분류</span><strong>{{ stage.categoryDisplayName }}</strong></div>
      <div class="wide"><span>요약</span><p>{{ stage.shortSummary || '입력된 요약 없음' }}</p></div>
    </div>

    <label class="blog-draft-field" for="blog-draft-title">
      <span>초안 제목</span>
      <input
        id="blog-draft-title"
        data-testid="blog-draft-title"
        :value="title"
        :maxlength="stage.titleMaxLength"
        :disabled="saving"
        autocomplete="off"
        @input="updateTitle"
      >
      <small>{{ title.length.toLocaleString() }} / {{ stage.titleMaxLength.toLocaleString() }}자</small>
    </label>

    <label class="blog-draft-field" for="blog-draft-body">
      <span>초안 본문</span>
      <textarea
        id="blog-draft-body"
        data-testid="blog-draft-body"
        :value="body"
        :maxlength="stage.bodyMaxLength"
        :disabled="saving"
        rows="16"
        @input="updateBody"
      />
      <small>{{ body.length.toLocaleString() }} / {{ stage.bodyMaxLength.toLocaleString() }}자 · 줄바꿈이 그대로 저장됩니다.</small>
    </label>

    <section class="blog-draft-media" aria-labelledby="blog-draft-media-title">
      <h3 id="blog-draft-media-title">연결 이미지 참고</h3>
      <p>이미지 선택과 삭제는 이미지 단계에서만 변경할 수 있습니다.</p>
      <div v-if="stage.linkedMedia.length" class="blog-draft-media-grid">
        <figure v-for="item in stage.linkedMedia" :key="item.id">
          <img v-if="item.thumbnailUrl" :src="item.thumbnailUrl" :alt="item.originalFileName">
          <div v-else class="neutral-media-placeholder" aria-hidden="true" />
          <figcaption>{{ item.originalFileName }}</figcaption>
        </figure>
      </div>
      <p v-else class="empty-inline">연결된 이미지 없음</p>
    </section>
  </section>
</template>
