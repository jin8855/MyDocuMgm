<script setup lang="ts">
import { RouterLink } from 'vue-router'

const cards = [
  { id: 'demo', title: '바삭한 새우 토스트', summary: '주재료 새우 · 20분 · 쉬움', tone: 'tone-one' },
  { id: 'demo-2', title: '마늘 버터 새우', summary: '주재료 새우 · 15분 · 쉬움', tone: 'tone-two' },
]
</script>

<template>
  <div class="page mobile-reading">
    <header class="mobile-reading-head">
      <div><p class="eyebrow">MOBILE READING</p><h1>모바일 읽기</h1><p>검색하고 읽는 일에 집중합니다.</p></div>
    </header>
    <section class="mobile-search" aria-label="모바일 자료 검색">
      <label class="sr-only" for="mobile-keyword">검색어</label>
      <input id="mobile-keyword" value="새우" inputmode="search" placeholder="자료 검색">
      <button class="button primary" type="button">검색</button>
    </section>
    <div class="mobile-query-row" aria-label="적용된 검색 조건">
      <span class="phone-chip">주재료: 새우 <span aria-hidden="true">×</span></span>
    </div>
    <section class="mobile-card-list" aria-label="모바일 자료 목록">
      <RouterLink v-for="card in cards" :key="card.id" :to="`/workflow/${card.id}/detail`" class="mobile-reading-card">
        <div class="mobile-image" :class="card.tone" />
        <div class="mobile-card-body">
          <span class="badge accent">요리</span>
          <h2>{{ card.title }}</h2>
          <p>{{ card.summary }}</p>
          <span class="mobile-detail-link">상세 보기 <span aria-hidden="true">→</span></span>
        </div>
      </RouterLink>
    </section>
  </div>
</template>
