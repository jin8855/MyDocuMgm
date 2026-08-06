<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { onBeforeRouteLeave, useRouter } from 'vue-router'
import { isNewWorkId } from '../app/router'
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
import MediaUploadPanel from '../features/media-management/MediaUploadPanel.vue'
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
const deletingContent = ref(false)
const pendingAction = ref<null | (() => void)>(null)
const mediaState = useMediaState(() => props.id)
let loadGeneration = 0

const isNewWork = computed(() => isNewWorkId(props.id))
const current = computed<WorkflowStep>(() =>
  workflowSteps.find((item) => item.route === props.step)?.key ?? 'URL')
const currentIndex = computed(() => workflowSteps.findIndex((item) => item.key === current.value))
const previous = computed(() => currentIndex.value > 0 ? workflowSteps[currentIndex.value - 1] : undefined)
const next = computed(() => currentIndex.value < workflowSteps.length - 1 ? workflowSteps[currentIndex.value + 1] : undefined)

function resetWorkflowState() {
  loadError.value = ''
  content.value = undefined
  ingredients.value = []
  ingredientDialogOpen.value = false
  editingIngredient.value = undefined
  dirty.value = false
  showUnsaved.value = false
  showDelete.value = false
  pendingAction.value = null
  mediaState.reset()
}

async function load() {
  const generation = ++loadGeneration
  resetWorkflowState()
  if (isNewWork.value) return
  if (current.value === 'URL') return

  try {
    const nextContent = await api.content(props.id)
    const nextIngredients = nextContent.categoryCode === 'COOKING'
      ? await api.ingredients(props.id)
      : []
    if (generation !== loadGeneration) return
    content.value = nextContent
    ingredients.value = nextIngredients
    if (current.value === 'MEDIA') await mediaState.load()
  } catch (error) {
    if (generation !== loadGeneration) return
    loadError.value = error instanceof Error ? error.message : '콘텐츠를 불러오지 못했습니다.'
  } finally {
    if (generation === loadGeneration) dirty.value = false
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

function acceptUrl(contentId: string) {
  dirty.value = false
  if (contentId !== props.id) void router.replace(`/workflow/${contentId}/url`)
}

async function softDeleteContent() {
  if (!content.value || deletingContent.value) return
  deletingContent.value = true
  loadError.value = ''
  try {
    await api.softDeleteContent(content.value.id, content.value.rowVersion)
    showDelete.value = false
    dirty.value = false
    await router.push('/contents')
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '콘텐츠를 휴지통으로 이동하지 못했습니다.'
  } finally {
    deletingContent.value = false
  }
}

onBeforeRouteLeave((to) => {
  if (!dirty.value) return true
  pendingAction.value = () => { void router.push(to.fullPath) }
  showUnsaved.value = true
  return false
})

watch(() => [props.id, props.step], load, { immediate: true })
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
        <h1>{{ content?.title ?? (isNewWork ? '새 작업' : current === 'URL' ? 'URL 접수 작업' : '콘텐츠 불러오는 중') }}</h1>
        <p>{{ workflowSteps[currentIndex]?.label }} 단계에서 필요한 정보만 차분하게 확인합니다.</p>
      </div>
      <span v-if="dirty" class="dirty-indicator">저장하지 않은 변경</span>
    </header>

    <section v-if="loadError" class="surface empty-state" role="alert">
      <h2>콘텐츠를 불러오지 못했습니다</h2>
      <p>{{ loadError }}</p>
      <button class="button" @click="router.push('/contents')">작업목록으로 돌아가기</button>
    </section>

    <UrlStage
      v-else-if="current === 'URL'"
      :key="props.id"
      :content-id="props.id"
      :new-work="isNewWork"
      @dirty="markDirty"
      @saved="dirty = false"
      @accepted="acceptUrl"
    />
    <section v-else-if="isNewWork" class="surface empty-state" data-testid="new-work-empty-stage">
      <h2>{{ workflowSteps[currentIndex]?.label }} 데이터가 없습니다</h2>
      <p>새 작업은 이전 작업의 데이터와 연결되지 않습니다. URL 단계부터 새 내용을 입력해 주세요.</p>
    </section>
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
      <MediaUploadPanel
        :preview-url="mediaState.uploadPreviewUrl.value"
        :file-name="mediaState.uploadFile.value?.name"
        :progress="mediaState.uploadProgress.value"
        :uploading="mediaState.uploading.value"
        :result="mediaState.uploadResult.value"
        :error="mediaState.operationError.value"
        @upload="mediaState.upload"
        @cancel="mediaState.cancelUpload"
        @clear="mediaState.clearPreview"
      />
      <MediaToolbar
        :filter="mediaState.filter.value"
        :sort="mediaState.sort.value"
        :page-size="mediaState.pageSize.value"
        :total="mediaState.result.value.totalCount"
        :selected="mediaState.result.value.selectedCount"
        :duplicates="mediaState.result.value.duplicateCount"
        :deleted="mediaState.result.value.deletedCount"
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
        <SelectedMediaEditor
          :item="mediaState.activeItem.value"
          @save="(item) => { markDirty(); mediaState.save(item) }"
          @delete="(item) => { markDirty(); mediaState.remove(item) }"
          @restore="(item) => { markDirty(); mediaState.restore(item) }"
          @move="(item, direction) => { markDirty(); mediaState.move(item, direction) }"
        />
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
        <p>콘텐츠를 휴지통으로 이동합니다. 연결된 미디어와 파일은 삭제하지 않습니다.</p>
        <footer><button class="button" :disabled="deletingContent" @click="showDelete = false">취소</button><button class="button danger" :disabled="deletingContent" @click="softDeleteContent">{{ deletingContent ? '이동 중…' : '휴지통으로 이동' }}</button></footer>
      </section>
    </div>
  </main>
</template>
