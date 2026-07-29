<script setup lang="ts">
const emit = defineEmits<{ dirty: [] }>()
</script>

<template>
  <section class="surface review-grid">
    <div><span class="badge accent">자동 분석 결과</span><h2>바삭한 새우 토스트</h2><p>새우 반죽을 식빵에 얹어 노릇하게 익히는 간단한 간식입니다.</p></div>
    <label>검토 상태<select @change="emit('dirty')"><option>확인 필요</option><option>검토 완료</option></select></label>
    <label>요약<textarea rows="5" @input="emit('dirty')">새우와 식빵으로 만드는 바삭한 간식</textarea></label>
  </section>
</template>
