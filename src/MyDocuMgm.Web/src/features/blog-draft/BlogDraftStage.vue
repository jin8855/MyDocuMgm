<script setup lang="ts">
import type { ContentItem } from '../../shared/types'
defineProps<{ content?: ContentItem }>()
const emit = defineEmits<{ dirty: [] }>()
</script>

<template>
  <section class="surface blog-preview">
    <div class="section-title"><div><h2>블로그 초안</h2><p>공개 전 개인 정보와 사실 관계를 마지막으로 확인합니다.</p></div><span class="badge">미공개</span></div>
    <label>제목<input :value="content?.title" @input="emit('dirty')"></label>
    <label>본문<textarea rows="14" @input="emit('dirty')">오늘은 새우와 식빵으로 바삭한 간식을 만들었습니다. 준비가 간단해서 다시 만들기 좋았습니다.</textarea></label>
  </section>
</template>
