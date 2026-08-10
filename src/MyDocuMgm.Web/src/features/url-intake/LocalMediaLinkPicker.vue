<script setup lang="ts">
import type { LinkableMediaPage } from '../../shared/types'

defineProps<{ page: LinkableMediaPage; selectedIds: ReadonlySet<string>; busy?: boolean; showSave?: boolean }>()
const emit = defineEmits<{ toggle: [id: string]; save: []; page: [page: number] }>()
</script>

<template>
  <section class="media-link-picker" aria-labelledby="media-link-title">
    <div class="section-title compact">
      <div>
        <h3 id="media-link-title">기존 로컬 이미지 연결</h3>
        <p>기존 파일을 복사하거나 이동하지 않고 콘텐츠 관계만 저장합니다.</p>
      </div>
      <button v-if="showSave !== false" class="button primary" type="button" :disabled="busy" @click="emit('save')">
        {{ busy ? '저장 중…' : `선택 ${selectedIds.size}개 저장` }}
      </button>
    </div>

    <div v-if="page.items.length" class="thumbnail-grid media-link-grid" aria-label="연결 가능한 이미지 목록">
      <label
        v-for="item in page.items"
        :key="item.id"
        class="media-link-card"
        :class="{ selected: selectedIds.has(item.id) }"
      >
        <span class="thumbnail-image">
          <img v-if="item.thumbnailUrl" :src="item.thumbnailUrl" :alt="item.originalFileName" loading="lazy">
          <span v-else>미리보기 없음</span>
        </span>
        <span class="media-link-label">
          <input
            type="checkbox"
            :checked="selectedIds.has(item.id)"
            :aria-label="`${item.originalFileName} 연결`"
            @change="emit('toggle', item.id)"
          >
          <strong>{{ item.originalFileName }}</strong>
        </span>
        <small>{{ item.width }}×{{ item.height }} · {{ Math.ceil(item.sizeBytes / 1024) }}KB</small>
      </label>
    </div>
    <div v-else class="empty-state compact-empty">
      <strong>연결할 수 있는 로컬 이미지가 없습니다.</strong>
      <p>이미지 업로드는 기존 이미지 단계에서만 제공합니다.</p>
    </div>

    <nav v-if="page.totalPages > 1" class="pagination" aria-label="이미지 페이지">
      <button class="button" type="button" :disabled="busy || page.page <= 1" @click="emit('page', page.page - 1)">이전</button>
      <span>{{ page.page }} / {{ page.totalPages }}</span>
      <button class="button" type="button" :disabled="busy || page.page >= page.totalPages" @click="emit('page', page.page + 1)">다음</button>
    </nav>
  </section>
</template>
