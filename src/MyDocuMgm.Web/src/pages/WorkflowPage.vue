<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { onBeforeRouteLeave, useRouter } from 'vue-router'
import WorkflowStepper from '../features/content-workflow/WorkflowStepper.vue'
import WorkflowFooter from '../features/content-workflow/WorkflowFooter.vue'
import UrlStage from '../features/content-workflow/UrlStage.vue'
import DetailStage from '../features/content-workflow/DetailStage.vue'
import CompletedStage from '../features/content-workflow/CompletedStage.vue'
import AnalysisReviewStage from '../features/analysis-review/AnalysisReviewStage.vue'
import CategoryEditingStage from '../features/category-editing/CategoryEditingStage.vue'
import BlogDraftStage from '../features/blog-draft/BlogDraftStage.vue'
import IngredientDialog from '../features/ingredient-editing/IngredientDialog.vue'
import MediaToolbar from '../features/media-management/MediaToolbar.vue'
import PaginationBar from '../features/media-management/PaginationBar.vue'
import SelectedMediaEditor from '../features/media-management/SelectedMediaEditor.vue'
import ThumbnailGrid from '../features/media-management/ThumbnailGrid.vue'
import { useMediaState } from '../features/media-management/useMediaState'
import { api } from '../shared/api/client'
import type { ContentItem, CookingIngredient, WorkflowStep } from '../shared/types'
import { workflowSteps } from '../shared/presentation/labels'
import { cloneValue } from '../shared/utils/clone'

const props = defineProps<{ id: string; step: string }>()
const router = useRouter()
const content = ref<ContentItem>()
const ingredients = ref<CookingIngredient[]>([])
const loadError = ref('')
const ingredientDialogOpen = ref(false)
const editingIngredient = ref<CookingIngredient>()
const dirty = ref(false)
const showUnsaved = ref(false)
const showDelete = ref(false)
const pendingAction = ref<null | (() => void)>(null)
const mediaState = useMediaState(() => props.id)

const current = computed<WorkflowStep>(() =>
  workflowSteps.find((item) => item.route === props.step)?.key ?? 'URL')
const currentIndex = computed(() => workflowSteps.findIndex((item) => item.key === current.value))
const previous = computed(() => currentIndex.value > 0 ? workflowSteps[currentIndex.value - 1] : undefined)
const next = computed(() => currentIndex.value < workflowSteps.length - 1 ? workflowSteps[currentIndex.value + 1] : undefined)

async function load() {
  loadError.value = ''
  content.value = undefined
  ingredients.value = []
  try {
    content.value = await api.content(props.id)
    ingredients.value = await api.ingredients(props.id)
    if (current.value === 'MEDIA') await mediaState.load()
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '콘텐츠를 불러오지 못했습니다.'
  } finally {
    dirty.value = false
  }
}

function navigate(route?: string) {
  if (!route) return
  const action = () => router.push(`/workflow/${props.id}/${route}`)
  if (dirty.value) {
    pendingAction.value = action
    showUnsaved.value = true
  } else action()
}

function close() {
  const action = () => router.push('/contents')
  if (dirty.value) {
    pendingAction.value = action
    showUnsaved.value = true
  } else action()
}

function discardAndContinue() {
  dirty.value = false
  showUnsaved.value = false
  pendingAction.value?.()
  pendingAction.value = null
}

function saveDraft() {
  dirty.value = false
  showUnsaved.value = false
  pendingAction.value?.()
  pendingAction.value = null
}

function editIngredient(value?: CookingIngredient) {
  editingIngredient.value = value ? cloneValue(value) : undefined
  ingredientDialogOpen.value = true
}

async function saveIngredient(value: CookingIngredient) {
  await api.saveIngredient(props.id, value)
  ingredients.value = await api.ingredients(props.id)
  ingredientDialogOpen.value = false
  dirty.value = true
}

async function deleteIngredient(value: CookingIngredient) {
  if (!window.confirm(`‘${value.name}’ 재료를 삭제하시겠습니까?`)) return
  await api.deleteIngredient(props.id, value)
  ingredients.value = await api.ingredients(props.id)
  dirty.value = true
}

async function unsetPrimary(value: CookingIngredient) {
  await api.saveIngredient(props.id, { ...value, isPrimary: false })
  ingredients.value = await api.ingredients(props.id)
  dirty.value = true
}

function markDirty() { dirty.value = true }

onBeforeRouteLeave(() => {
  if (!dirty.value) return true
  showUnsaved.value = true
  return false
})

watch(() => props.step, load)
onMounted(load)
</script>

<template>
  <main class="page workflow-page">
    <WorkflowStepper
      :current="current"
      :show-tools="current === 'DETAIL'"
      @edit="markDirty"
      @delete="showDelete = true"
      @close="close"
    />

    <header class="page-title compact-title">
      <div>
        <span class="eyebrow">7단계 작업 흐름</span>
        <h1>{{ content?.title ?? '콘텐츠 불러오는 중' }}</h1>
        <p>{{ workflowSteps[currentIndex]?.label }} 단계에서 필요한 정보만 차분하게 확인합니다.</p>
      </div>
      <span v-if="dirty" class="dirty-indicator">저장하지 않은 변경</span>
    </header>

    <section v-if="loadError" class="surface empty-state" role="alert">
      <h2>콘텐츠를 불러오지 못했습니다</h2>
      <p>{{ loadError }}</p>
      <button class="button" @click="router.push('/contents')">작업목록으로 돌아가기</button>
    </section>

    <UrlStage v-else-if="current === 'URL'" @dirty="markDirty" />
    <AnalysisReviewStage v-else-if="current === 'ANALYSIS_REVIEW'" @dirty="markDirty" />

    <template v-else-if="current === 'CATEGORY_EDIT'">
      <CategoryEditingStage
        :ingredients="ingredients"
        @dirty="markDirty"
        @add="editIngredient()"
        @edit="editIngredient"
        @delete="deleteIngredient"
        @unset="unsetPrimary"
      />
      <IngredientDialog
        :open="ingredientDialogOpen"
        :value="editingIngredient"
        @close="ingredientDialogOpen = false"
        @save="saveIngredient"
      />
    </template>

    <template v-else-if="current === 'MEDIA'">
      <MediaToolbar
        :filter="mediaState.filter.value"
        :sort="mediaState.sort.value"
        :page-size="mediaState.pageSize.value"
        :total="mediaState.result.value.totalCount"
        :selected="mediaState.result.value.selectedCount"
        :duplicates="mediaState.result.value.duplicateCount"
        @filter="mediaState.setFilter"
        @sort="mediaState.setSort"
        @page-size="mediaState.setPageSize"
      />
      <div class="media-layout">
        <section class="surface">
          <ThumbnailGrid
            :items="mediaState.result.value.items"
            :active-id="mediaState.activeId.value"
            @select="mediaState.select"
            @toggle="(item) => { markDirty(); mediaState.toggle(item) }"
          />
          <PaginationBar
            :page="mediaState.result.value.page"
            :total-pages="mediaState.result.value.totalPages"
            @page="mediaState.setPage"
          />
        </section>
        <SelectedMediaEditor :item="mediaState.activeItem.value" @save="(item) => { markDirty(); mediaState.save(item) }" />
      </div>
    </template>

    <DetailStage v-else-if="current === 'DETAIL'" :content="content" />
    <BlogDraftStage v-else-if="current === 'BLOG_DRAFT'" :content="content" @dirty="markDirty" />
    <CompletedStage v-else @close="router.push('/contents')" />

    <WorkflowFooter
      v-if="!loadError"
      :previous-label="previous?.label"
      :next-label="next?.label"
      :save-label="current === 'COMPLETED' ? '완료 상태 저장' : '임시저장'"
      @previous="navigate(previous?.route)"
      @save="dirty = false"
      @next="navigate(next?.route)"
    />

    <div v-if="showUnsaved" class="modal-backdrop" role="presentation">
      <section class="dialog unsaved-dialog" role="dialog" aria-modal="true" aria-labelledby="unsaved-title">
        <header><div><span class="eyebrow">저장 확인</span><h3 id="unsaved-title">변경 내용을 저장하지 않았습니다</h3></div></header>
        <p>현재 단계에서 입력한 내용을 저장한 뒤 이동하거나, 변경을 버리고 이동할 수 있습니다.</p>
        <footer>
          <button class="button" @click="showUnsaved = false; pendingAction = null">계속 편집</button>
          <button class="button danger-quiet" @click="discardAndContinue">변경 버리기</button>
          <button class="button primary" @click="saveDraft">저장 후 이동</button>
        </footer>
      </section>
    </div>

    <div v-if="showDelete" class="modal-backdrop" role="presentation">
      <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="delete-title">
        <header><h3 id="delete-title">콘텐츠를 삭제하시겠습니까?</h3></header>
        <p>삭제된 항목은 기본 작업목록에서 숨겨집니다. 이 화면에서는 실제 삭제를 실행하지 않습니다.</p>
        <footer><button class="button" @click="showDelete = false">취소</button><button class="button danger" @click="showDelete = false">삭제 확인</button></footer>
      </section>
    </div>
  </main>
</template>
