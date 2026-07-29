<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { api } from '../api'
import StatePanel from '../components/StatePanel.vue'
import type { ContentItem } from '../types'

const items = ref<ContentItem[]>([])
const loading = ref(true)
const error = ref('')
const readyCount = computed(() => items.value.filter((item) => item.status === 'READY').length)

onMounted(async () => {
  try { items.value = (await api.contents()).items }
  catch (reason) { error.value = reason instanceof Error ? reason.message : '상태를 불러오지 못했습니다.' }
  finally { loading.value = false }
})
</script>

<template>
  <div class="page dashboard">
    <section class="hero">
      <p class="eyebrow">오늘의 생활 서가</p>
      <h1>알게 된 것을<br><em>잊지 않도록.</em></h1>
      <p>요리의 작은 요령부터 다시 가고 싶은 장소까지, 생활에서 건진 지식을 한곳에 정리하세요.</p>
      <RouterLink class="button primary" to="/contents/new">첫 문장 적기 <span>→</span></RouterLink>
    </section>
    <section class="summary-strip" aria-label="기록 상태 요약">
      <div><strong>{{ items.length }}</strong><span>전체 기록</span></div>
      <div><strong>{{ readyCount }}</strong><span>정리 완료</span></div>
      <div><strong>{{ items.filter((item) => item.isFavorite).length }}</strong><span>즐겨찾기</span></div>
      <div class="summary-note"><span class="pulse" /> Phase 1A · 로컬 준비</div>
    </section>
    <section class="recent">
      <div class="section-heading">
        <div><p class="eyebrow">최근 펼쳐본 기록</p><h2>이어 정리하기</h2></div>
        <RouterLink to="/contents">모두 보기 →</RouterLink>
      </div>
      <StatePanel v-if="loading" kind="loading" title="생활 기록을 펼치는 중입니다" />
      <StatePanel v-else-if="error" kind="error" title="기록을 불러오지 못했습니다" :detail="error" />
      <StatePanel v-else-if="!items.length" kind="empty" title="아직 적어둔 생활 기록이 없습니다">
        <RouterLink class="text-link" to="/contents/new">새 기록 시작하기</RouterLink>
      </StatePanel>
      <div v-else class="record-list">
        <RouterLink v-for="item in items.slice(0, 4)" :key="item.id" :to="`/contents/${item.id}`" class="record-row">
          <span class="record-index">{{ String(items.indexOf(item) + 1).padStart(2, '0') }}</span>
          <span class="record-main"><small>{{ item.categoryDisplayName }}</small><strong>{{ item.title }}</strong></span>
          <span class="record-summary">{{ item.shortSummary }}</span>
          <span class="status" :data-status="item.status">{{ item.status === 'READY' ? '정리 완료' : '정리 중' }}</span>
          <span aria-hidden="true">↗</span>
        </RouterLink>
      </div>
    </section>
  </div>
</template>
