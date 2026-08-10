<script setup lang="ts">
import { ref } from 'vue'
import MediaUploadPanel from './MediaUploadPanel.vue'
import type { ImageStageMediaItem, LinkableMediaPage } from '../../shared/types'

defineProps<{
  library: LinkableMediaPage
  selectedItems: ImageStageMediaItem[]
  selectedIds: ReadonlySet<string>
  busy: boolean
  error: string
  notice: string
  previewUrl: string
  fileName?: string
  progress: number
  uploading: boolean
  uploadResult: string
}>()

const emit = defineEmits<{
  upload: [file: File]
  cancelUpload: []
  clearPreview: []
  toggle: [id: string]
  page: [page: number]
}>()

const brokenPreviewIds = ref(new Set<string>())

function markPreviewBroken(mediaId: string) {
  const next = new Set(brokenPreviewIds.value)
  next.add(mediaId)
  brokenPreviewIds.value = next
}
</script>

<template>
  <div class="image-editing-stage" data-testid="image-editing-stage">
    <section class="surface image-stage-intro">
      <div class="section-title">
        <div>
          <span class="eyebrow">4단계</span>
          <h2>이미지</h2>
          <p>현재 콘텐츠에 연결할 이미지를 선택합니다. 연결 해제는 관계만 변경하며 원본 파일은 삭제하지 않습니다.</p>
        </div>
        <span class="status-badge">선택 {{ selectedIds.size }}개</span>
      </div>
      <p v-if="error" class="media-error" role="alert">{{ error }}</p>
      <p v-if="notice" class="media-success" role="status">{{ notice }}</p>
    </section>

    <MediaUploadPanel
      :preview-url="previewUrl"
      :file-name="fileName"
      :progress="progress"
      :uploading="uploading"
      :result="uploadResult"
      :error="''"
      @upload="emit('upload', $event)"
      @cancel="emit('cancelUpload')"
      @clear="emit('clearPreview')"
    />

    <section class="surface image-selected-panel" aria-labelledby="image-selected-title">
      <div class="section-title compact">
        <div>
          <h3 id="image-selected-title">현재 연결된 이미지</h3>
          <p>임시저장 후 다시 들어와도 이 선택 상태가 복원됩니다.</p>
        </div>
      </div>
      <div v-if="selectedItems.length" class="image-selected-grid">
        <article v-for="item in selectedItems" :key="item.id" class="image-selected-card">
          <span class="thumbnail-image">
            <img
              v-if="item.thumbnailUrl && !brokenPreviewIds.has(item.id)"
              :src="item.thumbnailUrl"
              :alt="item.originalFileName"
              loading="lazy"
              @error="markPreviewBroken(item.id)"
            >
            <span v-else>미리보기 없음</span>
          </span>
          <strong>{{ item.originalFileName }}</strong>
          <small>{{ item.width }}×{{ item.height }} · {{ Math.ceil(item.sizeBytes / 1024) }}KB</small>
          <button
            class="button danger-quiet"
            type="button"
            :disabled="busy"
            :aria-label="`${item.originalFileName} 연결 해제`"
            @click="emit('toggle', item.id)"
          >연결 해제</button>
        </article>
      </div>
      <div v-else class="empty-state compact-empty" data-testid="image-empty-selection">
        <strong>선택한 이미지가 없습니다.</strong>
        <p>이미지 없이도 임시저장하거나 다음 단계로 진행할 수 있습니다.</p>
      </div>
    </section>

    <section class="surface image-library-panel" aria-labelledby="image-library-title">
      <div class="section-title compact">
        <div>
          <h3 id="image-library-title">이 콘텐츠의 이미지</h3>
          <p>현재 콘텐츠에 업로드된 JPEG, PNG, WebP 중 사용할 이미지를 선택합니다.</p>
        </div>
      </div>
      <div v-if="library.items.length" class="thumbnail-grid media-link-grid" aria-label="선택 가능한 이미지 목록">
        <label
          v-for="item in library.items"
          :key="item.id"
          class="media-link-card"
          :class="{ selected: selectedIds.has(item.id) }"
        >
          <span class="thumbnail-image">
            <img
              v-if="item.thumbnailUrl && !brokenPreviewIds.has(item.id)"
              :src="item.thumbnailUrl"
              :alt="item.originalFileName"
              loading="lazy"
              @error="markPreviewBroken(item.id)"
            >
            <span v-else>미리보기 없음</span>
          </span>
          <span class="media-link-label">
            <input
              type="checkbox"
              :checked="selectedIds.has(item.id)"
              :disabled="busy"
              :aria-label="`${item.originalFileName} 연결`"
              @change="emit('toggle', item.id)"
            >
            <strong>{{ item.originalFileName }}</strong>
          </span>
          <small>{{ item.width }}×{{ item.height }} · {{ Math.ceil(item.sizeBytes / 1024) }}KB</small>
        </label>
      </div>
      <div v-else class="empty-state compact-empty" data-testid="image-empty-library">
        <strong>등록된 이미지가 없습니다.</strong>
        <p>위 업로드 영역에서 JPEG, PNG 또는 WebP 이미지를 추가할 수 있습니다.</p>
      </div>
      <nav v-if="library.totalPages > 1" class="pagination" aria-label="이미지 페이지">
        <button class="button" type="button" :disabled="busy || library.page <= 1" @click="emit('page', library.page - 1)">이전</button>
        <span>{{ library.page }} / {{ library.totalPages }}</span>
        <button class="button" type="button" :disabled="busy || library.page >= library.totalPages" @click="emit('page', library.page + 1)">다음</button>
      </nav>
    </section>
  </div>
</template>
