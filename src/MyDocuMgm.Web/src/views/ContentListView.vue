<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { api } from '../api'
import StatePanel from '../components/StatePanel.vue'
import type { Category, ContentItem } from '../types'

const items = ref<ContentItem[]>([])
const categories = ref<Category[]>([])
const search = ref('')
const categoryId = ref('')
const status = ref('')
const loading = ref(true)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try { items.value = (await api.contents(search.value, categoryId.value, status.value)).items }
  catch (reason) { error.value = reason instanceof Error ? reason.message : '목록을 불러오지 못했습니다.' }
  finally { loading.value = false }
}

let timer = 0
watch([search, categoryId, status], () => {
  window.clearTimeout(timer)
  timer = window.setTimeout(load, 180)
})
onMounted(async () => { categories.value = await api.categories(); await load() })
</script>

<template>
  <div class="page">
    <div class="page-heading">
      <div><p class="eyebrow">나의 서가</p><h1>생활 기록</h1><p>필요한 순간 다시 찾을 수 있도록 짧고 분명하게 정리합니다.</p></div>
      <RouterLink class="button primary" to="/contents/new">새 기록</RouterLink>
    </div>
    <section class="filter-bar" aria-label="기록 검색과 필터">
      <label class="search-field"><span aria-hidden="true">⌕</span><input v-model="search" placeholder="제목이나 요약으로 찾기" aria-label="검색"></label>
      <select v-model="categoryId" aria-label="분류"><option value="">모든 분류</option><option v-for="category in categories" :key="category.id" :value="category.id">{{ category.displayName }}</option></select>
      <select v-model="status" aria-label="상태"><option value="">모든 상태</option><option value="INBOX">받은 기록</option><option value="REVIEW_REQUIRED">확인 필요</option><option value="READY">정리 완료</option><option value="ARCHIVED">보관</option></select>
    </section>
    <StatePanel v-if="loading" kind="loading" title="기록을 찾는 중입니다" />
    <StatePanel v-else-if="error" kind="error" title="기록을 불러오지 못했습니다" :detail="error"><button class="text-link" @click="load">다시 시도</button></StatePanel>
    <StatePanel v-else-if="!items.length" kind="empty" title="조건에 맞는 기록이 없습니다" detail="검색어나 필터를 바꾸거나 새 기록을 시작해 보세요." />
    <div v-else class="record-table">
      <div class="table-labels"><span>분류 / 제목</span><span>요약</span><span>상태</span><span>최근 수정</span></div>
      <RouterLink v-for="item in items" :key="item.id" :to="`/contents/${item.id}`" class="table-row">
        <span><small>{{ item.categoryDisplayName }}</small><strong>{{ item.isFavorite ? '★ ' : '' }}{{ item.title }}</strong></span>
        <span>{{ item.shortSummary || '요약 없음' }}</span>
        <span class="status" :data-status="item.status">{{ item.status }}</span>
        <time :datetime="item.updatedAtUtc">{{ new Date(item.updatedAtUtc).toLocaleDateString('ko-KR') }}</time>
      </RouterLink>
    </div>
  </div>
</template>
