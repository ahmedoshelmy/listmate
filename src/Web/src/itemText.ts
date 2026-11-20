/** Mirrors the server's ItemText.Parse so an item added offline shows its quantity right away. */
export function parseItemText(text: string): { quantity: number; name: string } {
  const cleaned = text.trim().replace(/\s+/g, ' ')
  const leading = /^(\d{1,3})\s*(?:x|×)?\s+(.+)$/i.exec(cleaned)
  const trailing = leading ? null : /^(.+?)\s+(?:x|×)\s*(\d{1,3})$/i.exec(cleaned)
  const quantity = leading ? Number(leading[1]) : trailing ? Number(trailing[2]) : 1
  const name = (leading ? leading[2] : trailing ? trailing[1] : cleaned) ?? cleaned
  return { quantity: Math.min(Math.max(quantity, 1), 999), name: name.trim() }
}

export function formatItem(item: { quantity: number; name: string }): string {
  return item.quantity > 1 ? `${item.quantity}x ${item.name}` : item.name
}
