<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api } from '../shared/api/client'
import type { OrphanMediaItem, TrashContentItem } from '../shared/types'

const trash = ref<TrashContentItem[]>([])
const orphanMedia = ref<OrphanMediaItem[]>([])
const loading = ref(false)
const error = ref('')

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    ;[trash.value, orphanMedia.value] = await Promise.all([api.trash(), api.orphanMedia()])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : '정리 대상을 불러오지 못했습니다.'
  } finally {
    loading.value = false
  }
}

async function deleteMedia(item: OrphanMediaItem) {
  if (!window.confirm(`‘${item.originalFileName}’ 미디어와 managed 파일을 영구 삭제하시겠습니까?`)) return
  error.value = ''
  try {
    await api.permanentlyDeleteOrphanMedia(item.id)
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : '미디어를 영구 삭제하지 못했습니다.'
  }
}

async function deleteContent(item: TrashContentItem) {
  if (!window.confirm(`‘${item.title}’ 콘텐츠를 영구 삭제하시겠습니까?`)) return
  error.value = ''
  try {
    await api.permanentlyDeleteContent(item.id)
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : '콘텐츠를 영구 삭제하지 못했습니다.'
  }
}

function canDeleteMedia(item: OrphanMediaItem) {
  return item.linkCount === 0 && ['MEDIA_FILE_READY_FOR_CLEANUP', 'MEDIA_FILE_ALREADY_ABSENT'].includes(item.fileState)
}

onMounted(refresh)
</script>

<template>
  <main class="page cleanup-page">
    <header class="page-title">
      <div><h1>휴지통·미디어 정리</h1><p>연결이 없는 미디어를 먼저 정리한 뒤, 소유 미디어가 없는 휴지통 콘텐츠만 영구 삭제합니다.</p></div>
      <button class="button" type="button" :disabled="loading" @click="refresh">새로고침</button>
    </header>

    <p v-if="error" class="media-error" role="alert">{{ error }}</p>

    <section class="surface cleanup-section" aria-labelledby="orphan-title">
      <header class="section-title"><div><h2 id="orphan-title">연결 없는 미디어</h2><p>화면 연결이 0건인 미디어입니다. 서버가 삭제 직전에 참조와 파일 경로를 다시 확인합니다.</p></div><strong>{{ orphanMedia.length }}건</strong></header>
      <div v-if="orphanMedia.length" class="table-scroll"><table class="data-table cleanup-table">
        <thead><tr><th>파일명</th><th>Media ID</th><th>연결</th><th>파일</th><th>작업</th></tr></thead>
        <tbody><tr v-for="item in orphanMedia" :key="item.id">
          <td><strong>{{ item.originalFileName }}</strong></td><td><code>{{ item.id }}</code></td><td>{{ item.linkCount }}</td><td>{{ item.fileExists ? '존재' : item.fileState === 'MEDIA_FILE_ALREADY_ABSENT' ? '이미 없음' : '경로 확인 필요' }}</td>
          <td><button class="button danger-quiet" type="button" :disabled="!canDeleteMedia(item)" :title="canDeleteMedia(item) ? '미디어와 managed 파일 영구 삭제' : '서버의 파일 안전성 확인이 필요합니다.'" @click="deleteMedia(item)">미디어 영구 삭제</button></td>
        </tr></tbody>
      </table></div>
      <p v-else class="empty-compact">정리할 orphan 미디어가 없습니다.</p>
    </section>

    <section class="surface cleanup-section" aria-labelledby="trash-title">
      <header class="section-title"><div><h2 id="trash-title">휴지통</h2><p>소유 미디어가 남아 있으면 콘텐츠 영구 삭제가 잠깁니다.</p></div><strong>{{ trash.length }}건</strong></header>
      <div v-if="trash.length" class="table-scroll"><table class="data-table cleanup-table">
        <thead><tr><th>제목</th><th>Content ID</th><th>삭제 시각</th><th>소유 미디어</th><th>작업</th></tr></thead>
        <tbody><tr v-for="item in trash" :key="item.id">
          <td><strong>{{ item.title }}</strong></td><td><code>{{ item.id }}</code></td><td>{{ item.deletedAtUtc?.slice(0, 19).replace('T', ' ') ?? '-' }}</td><td>{{ item.ownedMediaCount }}</td>
          <td><button class="button danger-quiet" type="button" :title="item.ownedMediaCount ? '서버가 소유 미디어를 재확인하며, 남아 있으면 삭제를 차단합니다.' : '콘텐츠 영구 삭제'" @click="deleteContent(item)">콘텐츠 영구 삭제</button></td>
        </tr></tbody>
      </table></div>
      <p v-else class="empty-compact">휴지통이 비어 있습니다.</p>
    </section>
  </main>
</template>
