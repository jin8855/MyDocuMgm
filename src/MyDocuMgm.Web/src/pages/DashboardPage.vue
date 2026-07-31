<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { api } from '../shared/api/client'
import type { ContentItem } from '../shared/types'
import { workflowStepLabel } from '../shared/presentation/labels'

const items = ref<ContentItem[]>([])
onMounted(async () => { items.value = (await api.contents({ majorCategory: '', attributeKey: '', attributeValue: '', status: '', searchScope: 'ALL', keyword: '', page: 1, pageSize: 24 })).items })
</script>

<template>
  <div class="page">
    <header class="page-title"><div><p class="eyebrow">WORK BOARD</p><h1>작업보드</h1><p>수집부터 완료까지 현재 작업 흐름을 확인합니다.</p></div><RouterLink class="button primary" to="/workflow/demo/url">새 URL 작업</RouterLink></header>
    <section class="metric-row">
      <article><span>진행 중</span><strong>7</strong><small>단계 작업</small></article>
      <article><span>확인 필요</span><strong>{{ items.filter((item) => item.status === 'REVIEW_REQUIRED').length }}</strong><small>자료</small></article>
      <article><span>정리 완료</span><strong>{{ items.filter((item) => item.status === 'READY').length }}</strong><small>검색 가능</small></article>
      <article><span>블로그 초안</span><strong>{{ items.filter((item) => item.blogDraftStatus !== '미작성').length }}</strong><small>준비됨</small></article>
    </section>
    <section class="surface">
      <div class="section-title"><div><h2>이어 할 작업</h2><p>행을 선택하면 현재 단계로 이동합니다.</p></div><RouterLink to="/contents">전체 작업목록 →</RouterLink></div>
      <RouterLink v-for="item in items.slice(0, 6)" :key="item.id" class="task-row" :to="`/workflow/${item.id}/${item.currentWorkflowStep.toLowerCase().replaceAll('_', '-')}`">
        <span class="task-number">{{ String(items.indexOf(item) + 1).padStart(2, '0') }}</span>
        <span><small>{{ item.categoryDisplayName }}</small><strong>{{ item.title }}</strong></span>
        <span class="badge">{{ workflowStepLabel(item.currentWorkflowStep) }}</span><span>{{ item.updatedAtUtc.slice(0, 10) }}</span><b>→</b>
      </RouterLink>
    </section>
  </div>
</template>
