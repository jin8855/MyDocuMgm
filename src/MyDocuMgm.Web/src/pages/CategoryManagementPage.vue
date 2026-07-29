<script setup lang="ts">
import { onMounted, ref } from 'vue'
import CategoryEditor from '../features/category-management/CategoryEditor.vue'
import { api } from '../shared/api/client'
import type { Category, SearchAttribute } from '../shared/types'

const categories = ref<Category[]>([])
const selected = ref<Category>()
const notice = ref('')
onMounted(async () => { categories.value = await api.categories(); selected.value = categories.value.find((value) => value.code === 'COOKING') })
async function saveCategory(value: Category) { selected.value = await api.updateCategory(value.code, value); categories.value = await api.categories(); notice.value = '분류 설정을 저장했습니다.' }
async function saveAttribute(value: SearchAttribute) { await api.updateAttribute(value.categoryCode, value); if (selected.value) selected.value.attributes = await api.attributes(selected.value.code); notice.value = '검색 속성을 저장했습니다.' }
</script>

<template>
  <div class="page">
    <header class="page-title"><div><p class="eyebrow">SETTINGS</p><h1>분류 관리</h1><p>11개 고정 코드의 표시와 검색 속성만 관리합니다.</p></div><button class="button" disabled title="11개 고정 분류만 사용합니다.">＋ 대분류 추가</button></header>
    <p v-if="notice" class="notice" role="status">{{ notice }}</p>
    <div class="category-layout">
      <aside class="surface category-list"><button v-for="category in categories" :key="category.code" :class="{ active: selected?.code === category.code }" @click="selected = category"><span>{{ category.displayName }}</span><small>{{ category.attributes?.length ?? 0 }}개 속성 · {{ category.isActive ? '활성' : '비활성' }}</small></button></aside>
      <CategoryEditor v-if="selected" :category="selected" @save-category="saveCategory" @save-attribute="saveAttribute" />
    </div>
  </div>
</template>
