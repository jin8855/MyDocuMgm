<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'

const navigation = [
  { to: '/', label: '작업보드', icon: '▦' },
  { to: '/contents', label: '작업목록', icon: '☷' },
  { to: '/categories', label: '분류 관리', icon: '⌘' },
  { to: '/mobile', label: '모바일 읽기', icon: '▯' },
]
</script>

<template>
  <div class="app-shell">
    <aside class="sidebar">
      <RouterLink class="brand" to="/">
        <span class="brand-symbol">나</span>
        <span><strong>나의 생활북</strong><small>MyDocuMgm</small></span>
      </RouterLink>
      <nav class="side-nav" aria-label="왼쪽 메뉴">
        <RouterLink v-for="item in navigation" :key="item.to" :to="item.to">
          <span aria-hidden="true">{{ item.icon }}</span>{{ item.label }}
        </RouterLink>
      </nav>
    </aside>
    <div class="shell-main">
      <header class="workspace-header">
        <div><strong>나의 생활북</strong><span>생활 자료 작업 공간</span></div>
        <RouterLink class="button primary" to="/workflow/demo/url">새 작업</RouterLink>
      </header>
      <main><RouterView /></main>
    </div>
  </div>
</template>
