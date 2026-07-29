<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { api } from '../api'
import StatePanel from '../components/StatePanel.vue'
import type { ContentItem } from '../types'

const props = defineProps<{ id: string }>()
const item = ref<ContentItem>()
const error = ref('')
onMounted(async () => {
  try { item.value = await api.content(props.id) }
  catch (reason) { error.value = reason instanceof Error ? reason.message : '기록을 불러오지 못했습니다.' }
})
</script>

<template>
  <div class="page narrow">
    <StatePanel v-if="error" kind="error" title="기록을 열지 못했습니다" :detail="error" />
    <StatePanel v-else-if="!item" kind="loading" title="기록을 펼치는 중입니다" />
    <article v-else class="detail-sheet">
      <div class="detail-actions"><RouterLink to="/contents">← 목록</RouterLink><span><RouterLink :to="`/contents/${id}/media`">이미지 관리</RouterLink><RouterLink class="button compact" :to="`/contents/${id}/edit`">편집</RouterLink></span></div>
      <p class="eyebrow">{{ item.categoryDisplayName }} · {{ item.status }}</p>
      <h1>{{ item.title }}</h1>
      <p class="lead">{{ item.shortSummary }}</p>
      <div class="detail-body">{{ item.detailContent || '아직 상세 내용을 적지 않았습니다.' }}</div>
      <div class="tags"><span v-for="tag in item.tags" :key="tag">#{{ tag }}</span></div>
      <dl><div><dt>공개 범위</dt><dd>{{ item.visibility === 'PRIVATE' ? '비공개' : '향후 공개 허용' }}</dd></div><div><dt>최근 수정</dt><dd>{{ new Date(item.updatedAtUtc).toLocaleString('ko-KR') }}</dd></div></dl>
    </article>
  </div>
</template>
