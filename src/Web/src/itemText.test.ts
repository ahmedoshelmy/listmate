import { describe, expect, it } from 'vitest'
import { formatItem, parseItemText } from './itemText'

describe('parseItemText', () => {
  it.each([
    ['milk', 1, 'milk'],
    ['2x milk', 2, 'milk'],
    ['2 x milk', 2, 'milk'],
    ['milk x2', 2, 'milk'],
    ['3 eggs', 3, 'eggs'],
    ['7up', 1, '7up'],
    ['  oat   milk ', 1, 'oat milk'],
  ])('parses %s like the server does', (text, quantity, name) => {
    expect(parseItemText(text)).toEqual({ quantity, name })
  })

  it('formats quantities above one', () => {
    expect(formatItem({ quantity: 3, name: 'milk' })).toBe('3x milk')
    expect(formatItem({ quantity: 1, name: 'milk' })).toBe('milk')
  })
})
