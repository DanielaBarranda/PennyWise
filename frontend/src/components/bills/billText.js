import { formatDate } from '../../utils/format.js'

// Words for bill frequencies, statuses and due dates, shared by the Bills page
// and the dashboard's upcoming-bills card.
export const FREQUENCY_LABELS = { OneTime: 'One time', Weekly: 'Every week', Monthly: 'Every month', Yearly: 'Every year' }
export const STATUS_LABELS = { Overdue: 'Overdue', DueSoon: 'Due soon', Upcoming: 'Upcoming', Paid: 'Paid' }
export const STATUS_TONES = { Overdue: 'danger', DueSoon: 'warning', Upcoming: 'info', Paid: 'ok' }

export function dueText(o) {
  if (o.status === 'Paid') return `Paid ${formatDate(o.paidOn)}`
  if (o.daysUntilDue < 0) return `${-o.daysUntilDue} day${o.daysUntilDue === -1 ? '' : 's'} overdue`
  if (o.daysUntilDue === 0) return 'Due today'
  if (o.daysUntilDue === 1) return 'Due tomorrow'
  return `Due in ${o.daysUntilDue} days`
}
