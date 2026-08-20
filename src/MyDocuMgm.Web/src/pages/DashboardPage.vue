<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { api } from '../shared/api/client'
import type { ContentItem } from '../shared/types'
import { workflowStepLabel, workflowStepRoute } from '../shared/presentation/labels'

const items = ref<ContentItem[]>([])
const activeItems = computed(() => items.value.filter(item => item.currentWorkflowStep !== 'COMPLETED'))
const continuationItems = computed(() => activeItems.value.slice(0, 6))
onMounted(async () => { items.value = (await api.contents({ majorCategory: '', attributeKey: '', attributeValue: '', status: '', workflowStep: '', searchScope: 'ALL', keyword: '', page: 1, pageSize: 24 })).items })
</script>

<template>
  <div class="page">
    <header class="page-title"><h1>작업보드</h1></header>
    <section class="metric-row">
      <article><span>진행 중</span><strong>{{ activeItems.length }}</strong><small>현재 진행 단계가 있는 자료 수</small></article>
      <article><span>확인 필요</span><strong>{{ items.filter((item) => item.status === 'REVIEW_REQUIRED').length }}</strong><small>자료</small></article>
      <article><span>정리 완료</span><strong>{{ items.filter((item) => item.status === 'READY').length }}</strong><small>검색 가능</small></article>
      <article><span>블로그 초안</span><strong>{{ items.filter((item) => item.blogDraftStatus !== '미작성').length }}</strong><small>준비됨</small></article>
    </section>
    <section class="surface">
      <div class="section-title"><div><h2>이어 할 작업</h2></div><RouterLink to="/contents">전체 작업목록 →</RouterLink></div>
      <p v-if="continuationItems.length === 0" class="dashboard-empty">이어 할 작업이 없습니다.</p>
      <RouterLink v-for="item in continuationItems" :key="item.id" class="task-row" :to="`/workflow/${item.id}/${workflowStepRoute(item.currentWorkflowStep)}`">
        <span class="task-number">{{ String(items.indexOf(item) + 1).padStart(2, '0') }}</span>
        <span><small>{{ item.categoryDisplayName }}</small><strong>{{ item.title }}</strong></span>
        <span class="badge">{{ workflowStepLabel(item.currentWorkflowStep) }}</span><span>{{ item.updatedAtUtc.slice(0, 10) }}</span><b>→</b>
      </RouterLink>
    </section>
  </div>
</template>
