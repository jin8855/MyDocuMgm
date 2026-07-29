import { createApp, nextTick, ref } from 'vue'
import CategoryPicker from '../../src/MyDocuMgm.Web/src/components/CategoryPicker.vue'

describe('CategoryPicker', () => {
  it('shows recognizable category names and updates a selection', async () => {
    const host = document.createElement('div')
    document.body.append(host)
    const selected = ref('one')
    const categories = [
      { id: 'one', sortOrder: 1, code: 'PLACE', displayName: '가볼곳' },
      { id: 'two', sortOrder: 2, code: 'COOKING', displayName: '요리' },
    ]
    const app = createApp({
      components: { CategoryPicker },
      setup: () => ({ categories, selected }),
      template: '<CategoryPicker v-model="selected" :categories="categories" />',
    })
    app.mount(host)

    expect(host.textContent).toContain('가볼곳')
    expect(host.textContent).toContain('요리')
    const second = host.querySelectorAll<HTMLInputElement>('input')[1]
    second.checked = true
    second.dispatchEvent(new Event('change'))
    await nextTick()
    expect(selected.value).toBe('two')

    app.unmount()
    host.remove()
  })
})
