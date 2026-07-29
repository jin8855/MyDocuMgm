import { routes } from '../../src/MyDocuMgm.Web/src/router'

describe('router', () => {
  it('exposes all Phase 1A screens', () => {
    expect(routes.map((route) => route.path)).toEqual([
      '/',
      '/contents',
      '/contents/new',
      '/contents/:id',
      '/contents/:id/edit',
      '/contents/:id/media',
    ])
  })
})
