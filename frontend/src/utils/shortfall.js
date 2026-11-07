// When spending goes over a category's limit, money can be moved into that limit
// from somewhere else in the same budget, so the budget's total never changes:
//   - another category's unused limit (it always keeps at least ₱1, because the
//     API requires every limit to be greater than 0), or
//   - the part of the budget not given to any category ("unassigned"), less what
//     was already spent in categories without a limit or moved into savings.
// All math is in centavos to avoid floating-point drift.
const toCents = (n) => Math.round(Number(n) * 100)
const toPesos = (c) => c / 100
const MIN_LIMIT_CENTS = 100

export const UNASSIGNED = 'Unassigned'

// `summary` is GET /api/dashboard/summary for the expense's period. Returns null
// when `category` is not over a limit; otherwise how much it is over and where
// money could come from, largest first.
export function shortfallFor(summary, category) {
  const budget = summary?.budget
  const info = summary?.categories?.find((c) => c.category === category)
  if (!budget || info?.limit == null || toCents(info.total) <= toCents(info.limit)) return null

  const overCents = toCents(info.total) - toCents(info.limit)
  const sources = []
  let limitedCents = 0
  let unlimitedSpentCents = 0
  for (const c of summary.categories) {
    if (c.limit == null) {
      unlimitedSpentCents += toCents(c.total)
      continue
    }
    limitedCents += toCents(c.limit)
    if (c.category === category) continue
    const limit = toCents(c.limit)
    const room = Math.min(limit - toCents(c.total), limit - MIN_LIMIT_CENTS)
    if (room > 0) sources.push({ key: c.category, label: c.category, available: toPesos(room) })
  }
  // Categories with the most room first; unassigned money (if any) goes on top,
  // because using it doesn't shrink another category's limit.
  sources.sort((a, b) => b.available - a.available)
  const unassigned = toCents(budget.amount) - limitedCents - unlimitedSpentCents - toCents(summary.saved ?? 0)
  if (unassigned > 0) sources.unshift({ key: UNASSIGNED, label: 'Budget not given to a category', available: toPesos(unassigned) })

  return {
    category,
    limit: info.limit,
    spent: info.total,
    over: toPesos(overCents),
    sources,
    budgetOver: summary.remaining < 0 ? -summary.remaining : 0,
  }
}

// The budget's new category limits after moving up to `over` from `sourceKey`
// into the overspent category. Returns { limits, moved }.
export function moveIntoLimit(budget, shortfall, sourceKey) {
  const source = shortfall.sources.find((s) => s.key === sourceKey)
  const movedCents = Math.min(toCents(shortfall.over), toCents(source?.available ?? 0))
  const limits = budget.categoryLimits.map((l) => {
    if (l.category === shortfall.category) return { category: l.category, amount: toPesos(toCents(l.amount) + movedCents) }
    if (l.category === sourceKey) return { category: l.category, amount: toPesos(toCents(l.amount) - movedCents) }
    return { category: l.category, amount: l.amount }
  })
  return { limits, moved: toPesos(movedCents) }
}
