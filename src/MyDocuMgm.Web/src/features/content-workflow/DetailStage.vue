<script setup lang="ts">
import type { ContentItem } from '../../shared/types'
defineProps<{ content?: ContentItem }>()
</script>

<template>
  <section class="surface detail-sheet">
    <span class="badge accent">{{ content?.categoryDisplayName }}</span>
    <h2>{{ content?.title }}</h2>
    <p class="lead">{{ content?.shortSummary }}</p>
    <div class="detail-hero tone-one">대표 이미지</div>
    <h3>만드는 순서</h3>
    <ol><li>새우와 양념을 곱게 다집니다.</li><li>식빵 위에 얇게 펴 바릅니다.</li><li>팬에서 앞뒤로 노릇하게 익힙니다.</li></ol>
    <div class="tag-row"><span v-for="tag in content?.tags" :key="tag">#{{ tag }}</span></div>
  </section>
</template>
