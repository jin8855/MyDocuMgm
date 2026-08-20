import { createApp, nextTick } from 'vue'
import CategoryManagementPage from '../../src/MyDocuMgm.Web/src/pages/CategoryManagementPage.vue'
import { api } from '../../src/MyDocuMgm.Web/src/shared/api/client'
import type { Category, SearchAttribute } from '../../src/MyDocuMgm.Web/src/shared/types'

async function flush(times = 1) {
  for (let index = 0; index < times; index += 1) {
    await Promise.resolve()
    await new Promise(resolve => setTimeout(resolve, 0))
    await nextTick()
  }
}

const categoryCodes = [
  'PLACE', 'COOKING', 'EXERCISE', 'CLEANING_LAUNDRY', 'TRAVEL', 'PHOTO',
  'STUDY', 'PRODUCT', 'PHONE_COMPUTER', 'TIP', 'OTHER',
]

function category(code: string, index: number): Category {
  return {
    id: `10000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
    code,
    displayName: code === 'COOKING' ? '요리' : code,
    sortOrder: index + 1,
    isActive: true,
    rowVersion: 'row',
  }
}

function attribute(code: string, key: string, name: string, sortOrder = 1): SearchAttribute {
  return {
    id: `${code}-${key}`,
    categoryCode: code,
    attributeKey: key,
    displayName: name,
    sortOrder,
    isActive: true,
    isSearchable: true,
    rowVersion: 'row',
  }
}

describe('category management attribute loading', () => {
  afterEach(() => vi.restoreAllMocks())

  it('prioritizes cooking, loads all eleven once, and distinguishes empty from failure', async () => {
    const categories = categoryCodes.map(category)
    const calls: string[] = []
    vi.spyOn(api, 'categories').mockResolvedValue(categories)
    vi.spyOn(api, 'attributes').mockImplementation(async code => {
      calls.push(code)
      if (code === 'PHOTO') throw new Error('synthetic attribute failure')
      if (code === 'OTHER') return []
      if (code === 'COOKING') {
        return [
          attribute(code, 'primaryIngredient', '주재료', 1),
          attribute(code, 'difficulty', '난이도', 2),
          attribute(code, 'time', '소요시간', 3),
        ]
      }
      return [attribute(code, 'sample', `${code} 속성`)]
    })

    const host = document.createElement('div')
    const app = createApp(CategoryManagementPage)
    app.mount(host)
    await flush(5)

    expect(calls[0]).toBe('COOKING')
    expect(calls).toHaveLength(11)
    expect(new Set(calls)).toEqual(new Set(categoryCodes))
    expect(host.querySelector('[data-category-code="COOKING"]')?.textContent).toContain('3개 속성')
    expect(host.querySelector('[data-category-code="PHOTO"]')?.textContent).toContain('불러오기 실패')
    expect(host.querySelector('[data-category-code="OTHER"]')?.textContent).toContain('0개 속성')
    expect(host.querySelectorAll('[data-attribute-row]')).toHaveLength(3)
    expect([...host.querySelectorAll<HTMLInputElement>('[data-attribute-display-name]')].map(input => input.value))
      .toEqual(['주재료', '난이도', '소요시간'])
    expect(host.querySelectorAll('.help-trigger')).toHaveLength(1)
    expect(host.textContent).not.toContain('COOKING')
    expect([...host.querySelectorAll('th')].at(-1)?.textContent).toBe('저장')
    expect(host.querySelector('[data-save-attribute]')?.closest('td')?.getAttribute('data-label')).toBe('저장')

    const product = host.querySelector<HTMLButtonElement>('[data-category-code="PRODUCT"]')!
    product.click()
    await flush(2)
    expect(calls.filter(code => code === 'PRODUCT')).toHaveLength(1)
    expect(host.querySelectorAll('[data-attribute-row]')).toHaveLength(1)

    const photo = host.querySelector<HTMLButtonElement>('[data-category-code="PHOTO"]')!
    photo.click()
    await flush(2)
    expect(host.textContent).toContain('검색 속성을 불러오지 못했습니다.')
    expect(host.querySelector<HTMLButtonElement>('[data-retry-attributes]')).not.toBeNull()
    expect(host.querySelector('[data-category-code="COOKING"]')?.textContent).toContain('3개 속성')
    app.unmount()
  })

  it('refreshes the same cached source after an attribute is saved', async () => {
    const categories = categoryCodes.map(category)
    let cookingRead = 0
    vi.spyOn(api, 'categories').mockResolvedValue(categories)
    vi.spyOn(api, 'attributes').mockImplementation(async code => {
      if (code !== 'COOKING') return []
      cookingRead += 1
      return [attribute(code, 'primaryIngredient', cookingRead === 1 ? '주재료' : '대표 재료')]
    })
    const update = vi.spyOn(api, 'updateAttribute').mockImplementation(async (_code, value) => value)

    const host = document.createElement('div')
    const app = createApp(CategoryManagementPage)
    app.mount(host)
    await flush(5)

    const displayName = host.querySelector<HTMLInputElement>('[data-attribute-display-name]')!
    displayName.value = '대표 재료'
    displayName.dispatchEvent(new Event('input', { bubbles: true }))
    host.querySelector<HTMLButtonElement>('[data-save-attribute]')!.click()
    await flush(4)

    expect(update).toHaveBeenCalledTimes(1)
    expect(cookingRead).toBe(2)
    expect(host.querySelector('[data-category-code="COOKING"]')?.textContent).toContain('1개 속성')
    expect(host.querySelector<HTMLInputElement>('[data-attribute-display-name]')?.value).toBe('대표 재료')
    app.unmount()
  })
})
