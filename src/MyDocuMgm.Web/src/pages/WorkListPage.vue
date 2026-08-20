<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { RouterLink } from 'vue-router'
import SearchPanel from '../features/content-search/SearchPanel.vue'
import { api } from '../shared/api/client'
import type { Category, ContentPage, SearchQuery } from '../shared/types'
import { contentStatusLabel, workflowStepLabel, workflowStepRoute } from '../shared/presentation/labels'
import HelpPopover from '../shared/components/HelpPopover.vue'

const categories = ref<Category[]>([])
const result = ref<ContentPage>({ items: [], totalCount: 0, page: 1, pageSize: 24, totalPages: 1 })
const loading = ref(false)
const query = reactive<SearchQuery>({ majorCategory: '', attributeKey: '', attributeValue: '', status: '', workflowStep: '', searchScope: 'ALL', keyword: '', page: 1, pageSize: 24 })
async function search() { loading.value = true; try { result.value = await api.contents(query) } finally { loading.value = false } }
function reset() { Object.assign(query, { majorCategory: '', attributeKey: '', attributeValue: '', status: '', workflowStep: '', searchScope: 'ALL', keyword: '', page: 1 }); void search() }
onMounted(async () => { categories.value = await api.categories(); await search() })
</script>

<template>
  <div class="page">
    <header class="page-title"><div class="title-with-help"><h1>작업목록</h1><HelpPopover label="작업목록 도움말">분류 속성, 상태, 태그와 내용으로 저장된 자료를 찾습니다.</HelpPopover></div></header>
    <SearchPanel :model-value="query" :categories="categories" @update:model-value="Object.assign(query, $event)" @search="search" @reset="reset" />
    <div class="result-summary"><strong>{{ result.totalCount }}</strong>개 결과 <span v-if="loading">· 검색 중…</span></div>
    <section class="surface flush">
      <div class="table-scroll"><table class="data-table work-list">
        <thead><tr><th>제목</th><th>대분류</th><th>현재 단계</th><th>상태</th><th>블로그 초안</th><th>태그</th><th>수정일</th></tr></thead>
        <tbody><tr v-for="item in result.items" :key="item.id">
          <td><RouterLink :to="`/workflow/${item.id}/${workflowStepRoute(item.currentWorkflowStep)}`"><strong>{{ item.title }}</strong><small>{{ item.shortSummary }}</small></RouterLink></td>
          <td>{{ item.categoryDisplayName }}</td><td><span class="badge accent">{{ workflowStepLabel(item.currentWorkflowStep) }}</span></td><td><span class="badge">{{ contentStatusLabel(item.status) }}</span></td><td>{{ item.blogDraftStatus }}</td><td><span v-for="tag in item.tags" :key="tag" class="tag">#{{ tag }}</span></td><td>{{ item.updatedAtUtc.slice(0, 10) }}</td>
        </tr></tbody>
      </table></div>
    </section>
  </div>
</template>
