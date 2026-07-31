import { api } from '../../src/MyDocuMgm.Web/src/shared/api/client'
import {
  contentStatusLabel,
  contentStatusOptions,
  workflowStepLabel,
  workflowStepLabels,
} from '../../src/MyDocuMgm.Web/src/shared/presentation/labels'

describe('Phase 1A repair mock contract', () => {
  it('maps workflow and status codes through one presentation mapper', () => {
    expect(workflowStepLabels).toEqual({
      URL: 'URL',
      ANALYSIS_REVIEW: '분석 검토',
      CATEGORY_EDIT: '분류별 편집',
      MEDIA: '이미지',
      DETAIL: '자료 상세',
      BLOG_DRAFT: '블로그 초안',
      COMPLETED: '완료',
    })
    expect(workflowStepLabel('CATEGORY_EDIT')).toBe('분류별 편집')
    expect(contentStatusLabel('REVIEW_REQUIRED')).toBe('검토 필요')
    expect(contentStatusOptions).toContainEqual({ value: 'REVIEW_REQUIRED', label: '검토 필요' })
  })

  it('exposes exactly the fixed eleven categories and the corrected phone label', async () => {
    const categories = await api.categories()

    expect(categories).toHaveLength(11)
    expect(categories.map((category) => category.code)).toEqual([
      'PLACE', 'COOKING', 'EXERCISE', 'CLEANING_LAUNDRY', 'TRAVEL', 'PHOTO',
      'STUDY', 'PRODUCT', 'PHONE_COMPUTER', 'TIP', 'OTHER',
    ])
    expect(categories.find((category) => category.code === 'PHONE_COMPUTER')?.displayName).toBe('폰&컴')
  })

  it('supports primary ingredient search separately from tag-only search', async () => {
    const primary = await api.contents({
      majorCategory: 'COOKING',
      attributeKey: 'primaryIngredient',
      attributeValue: '새우',
      status: '',
      searchScope: 'ALL',
      keyword: '',
      page: 1,
      pageSize: 24,
    })
    const tags = await api.contents({
      majorCategory: '',
      attributeKey: '',
      attributeValue: '',
      status: '',
      searchScope: 'TAG',
      keyword: '간단요리',
      page: 1,
      pageSize: 24,
    })

    expect(primary.items.length).toBeGreaterThan(0)
    expect(primary.items.every((item) => item.categoryCode === 'COOKING')).toBe(true)
    expect(tags.items.map((item) => item.id)).toContain('demo')
  })

  it('adds, changes and deletes an ingredient without forcing a single primary', async () => {
    const before = await api.ingredients()
    const secondPrimary = before.find((item) => item.name === '새우 페이스트')
    expect(before.filter((item) => item.isPrimary)).toHaveLength(2)
    expect(secondPrimary).toBeDefined()

    await api.saveIngredient({ ...secondPrimary!, isPrimary: false })
    expect((await api.ingredients()).filter((item) => item.isPrimary)).toHaveLength(1)

    await api.deleteIngredient(secondPrimary!.id)
    expect((await api.ingredients()).some((item) => item.id === secondPrimary!.id)).toBe(false)
  })

  it('performs media filter, sort and server-page shaped slicing', async () => {
    const first = await api.media('ALL', 'TIME_ASC', 1, 24)
    const second = await api.media('ALL', 'TIME_ASC', 2, 24)
    const selected = await api.media('SELECTED', 'TIME_DESC', 1, 48)
    const duplicates = await api.media('DUPLICATE', 'TIME_ASC', 1, 96)

    expect(first.items).toHaveLength(24)
    expect(second.items[0].id).not.toBe(first.items[0].id)
    expect(first.totalCount).toBe(137)
    expect(first.totalPages).toBe(6)
    expect(selected.items.every((item) => item.isSelected)).toBe(true)
    expect(duplicates.items.length).toBe(4)
  })
})
