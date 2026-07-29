<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
</script>

<template>
  <div class="app-shell">
    <header class="topbar">
      <RouterLink class="brand" to="/" aria-label="나의 생활북 홈">
        <span class="brand-mark">나</span>
        <span><strong>나의 생활북</strong><small>생활의 발견을 내 것으로</small></span>
      </RouterLink>
      <nav aria-label="주 메뉴">
        <RouterLink to="/">요약</RouterLink>
        <RouterLink to="/contents">생활 기록</RouterLink>
      </nav>
      <RouterLink class="button primary compact" to="/contents/new">새 기록</RouterLink>
    </header>
    <main>
      <RouterView />
    </main>
    <footer>MyDocuMgm · 내 컴퓨터에 차곡차곡 쌓이는 생활 지식</footer>
  </div>
</template>
