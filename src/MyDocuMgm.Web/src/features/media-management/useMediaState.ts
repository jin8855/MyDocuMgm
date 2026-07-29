import { computed, ref } from 'vue'
import { api } from '../../shared/api/client'
import type { MediaFilter, MediaItem, MediaPage, MediaSort } from '../../shared/types'

export function useMediaState() {
  const filter = ref<MediaFilter>('ALL')
  const sort = ref<MediaSort>('TIME_ASC')
  const page = ref(1)
  const pageSize = ref(24)
  const result = ref<MediaPage>({ items: [], totalCount: 0, selectedCount: 0, duplicateCount: 0, page: 1, pageSize: 24, totalPages: 1 })
  const activeId = ref<string>()
  const activeItem = computed(() => result.value.items.find((item) => item.id === activeId.value))

  async function load() {
    result.value = await api.media(filter.value, sort.value, page.value, pageSize.value)
    if (activeId.value && !result.value.items.some((item) => item.id === activeId.value)) activeId.value = undefined
  }
  async function setFilter(value: MediaFilter) { filter.value = value; page.value = 1; await load() }
  async function setSort(value: MediaSort) { sort.value = value; page.value = 1; await load() }
  async function setPageSize(value: number) { pageSize.value = value; page.value = 1; await load() }
  async function setPage(value: number) { page.value = value; await load() }
  function select(item: MediaItem) { activeId.value = item.id }
  async function toggle(item: MediaItem) { item.isSelected = !item.isSelected; await api.updateMedia(item); await load() }
  async function save(item: MediaItem) { await api.updateMedia(item); await load(); activeId.value = item.id }

  return { filter, sort, page, pageSize, result, activeId, activeItem, load, setFilter, setSort, setPageSize, setPage, select, toggle, save }
}
