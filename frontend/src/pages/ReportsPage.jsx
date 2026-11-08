import { useState } from 'react'
import { CategoryIcon } from '../components/common/CategoryBadge.jsx'
import Icon from '../components/common/Icon.jsx'
import PageHeader from '../components/common/PageHeader.jsx'
import MonthPicker from '../components/common/MonthPicker.jsx'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { STATUS_LABELS, STATUS_TONES } from '../components/bills/billText.js'
import InsightList from '../components/insights/InsightList.jsx'
import { useAsync } from '../hooks/useAsync.js'
import { api } from '../services/api.js'
import { periodLabel, periodStart } from '../utils/budgetPeriod.js'
import { formatDate, formatMonth, formatPeso } from '../utils/format.js'

const signedPeso = (value) => `${value > 0 ? '+' : value < 0 ? '−' : ''}${formatPeso(Math.abs(value))}`
const signedPercent = (value) => (value == null ? '—' : `${value > 0 ? '+' : value < 0 ? '−' : ''}${Math.abs(value)}%`)

// Every figure comes from GET /api/reports/monthly; the CSV download is built by
// the API from the same report, so the file always matches this page.
export default function ReportsPage({ month, onMonthChange, period }) {
  const { data, error, reload } = useAsync(() => api.getMonthlyReport(month), [month])
  const report = data?.month === month ? data : null
  const isEmpty = report && report.expenseCount === 0 && report.budget == null && report.savings.goals.length === 0 && report.bills.due === 0 && report.periodBudgets.length === 0

  return (
    <>
      <PageHeader
        title="Monthly report"
        subtitle={`Your money in ${formatMonth(month)}, from your saved records.`}
        controls={<MonthPicker month={month} onChange={onMonthChange} />}
        actionsTour="reports-export"
      >
        <a className="button button--secondary" href={api.monthlyReportCsvUrl(month)} download>
          <Icon name="download" />
          Download CSV
        </a>
        <button type="button" className="button button--secondary" onClick={() => window.print()} disabled={!report}>
          <Icon name="print" />
          Print / Save as PDF
        </button>
      </PageHeader>

      {error && !report ? (
        <ErrorState error={error} onRetry={reload} />
      ) : !report ? (
        <LoadingState label="Loading report…" variant="card" />
      ) : isEmpty ? (
        <div className="card" data-tour="reports-empty">
          <EmptyState title={`Nothing recorded for ${formatMonth(month)}`} message="Reports fill in as you record expenses, budgets, bills and savings." />
        </div>
      ) : (
        <div className="report">
          <section className="report__summary" aria-label="Summary" data-tour="reports-summary">
            <Figure label="Monthly budget" value={report.budget == null ? 'Not set' : formatPeso(report.budget)} />
            <Figure label="Total spent" value={formatPeso(report.totalSpent)} note={`${report.expenseCount} expense${report.expenseCount === 1 ? '' : 's'}`} />
            <Figure label="Budget used" value={report.budgetUsedPercent == null ? '—' : `${report.budgetUsedPercent}%`}
              note={report.remaining == null ? null : report.remaining < 0 ? `${formatPeso(-report.remaining)} over` : `${formatPeso(report.remaining)} left`} />
            <Figure label={`Compared with ${formatMonth(report.previousMonth)}`} value={signedPercent(report.changeFromPreviousPercent)}
              note={`${formatPeso(report.previousTotalSpent)} spent then`} />
          </section>

          <section className="card card--flush" aria-labelledby="report-categories">
            <div className="card__header card__header--padded"><h2 id="report-categories">Spending by category</h2></div>
            {report.categories.length === 0 ? (
              <div className="card__body"><EmptyState compact title="No spending this month" /></div>
            ) : (
              <div className="table-wrap">
                <table className="data-table data-table--keep-head">
                  <thead>
                    <tr>
                      <th scope="col">Category</th>
                      <th scope="col" className="num">Spent</th>
                      <th scope="col" className="num">Share</th>
                      <th scope="col" className="num">Limit</th>
                      <th scope="col" className="num">Last month</th>
                      <th scope="col" className="num">Change</th>
                    </tr>
                  </thead>
                  <tbody>
                    {report.categories.map((c) => (
                      <tr key={c.category}>
                        <td><span className="data-table__cell"><CategoryIcon category={c.category} size="sm" shape="round" />{c.category}</span></td>
                        <td className="num data-table__strong">{formatPeso(c.total)}</td>
                        <td className="num data-table__muted">{c.sharePercent}%</td>
                        <td className="num data-table__muted">{c.limit == null ? '—' : formatPeso(c.limit)}</td>
                        <td className="num data-table__muted">{formatPeso(c.previousTotal)}</td>
                        <td className="num">{signedPeso(c.change)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {report.periodBudgets.length > 0 && (
            <section className="card card--flush" aria-labelledby="report-period-budgets">
              <div className="card__header card__header--padded"><h2 id="report-period-budgets">Weekly and daily budgets</h2></div>
              <div className="table-wrap">
                <table className="data-table data-table--keep-head">
                  <thead>
                    <tr>
                      <th scope="col">Period</th>
                      <th scope="col" className="num">Budget</th>
                      <th scope="col" className="num">Spent</th>
                      <th scope="col" className="num">To savings</th>
                      <th scope="col" className="num">Left</th>
                    </tr>
                  </thead>
                  <tbody>
                    {report.periodBudgets.map((b) => (
                      <tr key={`${b.period}-${b.start}`}>
                        <td>
                          <span className="data-table__strong">{periodLabel(b.period, b.start)}</span>
                          <span className="data-table__muted"> · {b.period === 'Weekly' ? 'Week' : 'Day'}</span>
                        </td>
                        <td className="num">{formatPeso(b.amount)}</td>
                        <td className="num">{formatPeso(b.spent)}</td>
                        <td className="num">{formatPeso(b.saved)}</td>
                        <td className={`num ${b.left < 0 ? 'amount--out' : ''}`}>
                          {b.left < 0 ? `${formatPeso(-b.left)} over` : formatPeso(b.left)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <p className="card__footnote">Each one counts the spending and the money moved into savings inside its own dates. They are not added up into a monthly budget.</p>
            </section>
          )}

          <ReportTransactions transactions={report.transactions ?? []} month={month} defaultGroup={period ?? 'Monthly'} />

          <div className="report__columns">
            <section className="card" aria-labelledby="report-savings">
              <div className="card__header"><h2 id="report-savings">Savings</h2></div>
              <dl className="report__facts">
                <div><dt>Set aside this month</dt><dd>{formatPeso(report.savings.contributions)}</dd></div>
                <div><dt>Withdrawn</dt><dd>{formatPeso(report.savings.withdrawals)}</dd></div>
                <div><dt>Net</dt><dd>{signedPeso(report.savings.net)}</dd></div>
              </dl>
              {report.savings.goals.length > 0 && (
                <ul className="report__goals">
                  {report.savings.goals.map((g) => (
                    <li key={g.name}>
                      <span className="report__goal-name">{g.name}</span>
                      <span className="report__goal-amounts">{formatPeso(g.savedAtMonthEnd)} of {formatPeso(g.targetAmount)} ({g.percentAtMonthEnd}%)</span>
                    </li>
                  ))}
                </ul>
              )}
              <p className="card__footnote">Savings are money set aside, so they are not counted as spending.</p>
            </section>

            <section className="card" aria-labelledby="report-bills">
              <div className="card__header"><h2 id="report-bills">Bills due this month</h2></div>
              <dl className="report__facts">
                <div><dt>Paid</dt><dd>{report.bills.paid} ({formatPeso(report.bills.paidAmount)})</dd></div>
                <div><dt>Unpaid</dt><dd>{report.bills.unpaid} ({formatPeso(report.bills.unpaidAmount)})</dd></div>
              </dl>
              {report.bills.items.length > 0 && (
                <ul className="report__bills">
                  {report.bills.items.map((b) => (
                    <li key={`${b.name}-${b.dueDate}`}>
                      <span>{b.name} <span className="data-table__muted">· {formatDate(b.dueDate)}</span></span>
                      <span className={`status-badge status-badge--${STATUS_TONES[b.status]}`}>{STATUS_LABELS[b.status]}</span>
                    </li>
                  ))}
                </ul>
              )}
              <p className="card__footnote">Paid bills recorded as expenses are already in total spending; they are not added again.</p>
            </section>
          </div>

          {report.insights.length > 0 && (
            <section className="card" aria-labelledby="report-insights">
              <div className="card__header"><h2 id="report-insights">Insights</h2></div>
              <InsightList insights={report.insights} showLinks={false} />
            </section>
          )}

          <ul className="report__notes" aria-label="Notes about this report">
            {report.notes.map((n) => <li key={n}><Icon name="info" size={14} />{n}</li>)}
          </ul>
        </div>
      )}
    </>
  )
}

const GROUPS = [
  ['Daily', 'By day'],
  ['Weekly', 'By week'],
  ['Monthly', 'Whole month'],
]

// Every expense and savings move in the month, grouped by day, week or the whole
// month, with each group's totals. Starts on the account's budget period.
function ReportTransactions({ transactions, month, defaultGroup }) {
  const [group, setGroup] = useState(defaultGroup)
  const groups = []
  for (const t of transactions) {
    const key = periodStart(group, t.date)
    let g = groups[groups.length - 1]
    if (!g || g.key !== key) {
      g = { key, rows: [], spent: 0, saved: 0 }
      groups.push(g)
    }
    g.rows.push(t)
    if (t.kind === 'Expense') g.spent += t.amount
    else g.saved += t.kind === 'SavingsDeposit' ? t.amount : -t.amount
  }

  return (
    <section className="card card--flush report__transactions" aria-labelledby="report-transactions">
      <div className="card__header card__header--padded">
        <h2 id="report-transactions">Transactions</h2>
        {transactions.length > 0 && (
          <fieldset className="segmented report__group-picker">
            <legend className="visually-hidden">Group transactions</legend>
            {GROUPS.map(([value, label]) => (
              <label key={value} className="segmented__option">
                <input type="radio" name="report-group" value={value} checked={group === value} onChange={() => setGroup(value)} />
                <span>{label}</span>
              </label>
            ))}
          </fieldset>
        )}
      </div>
      {transactions.length === 0 ? (
        <div className="card__body"><EmptyState compact title={`No transactions in ${formatMonth(month)}`} /></div>
      ) : (
        <div className="table-wrap">
          <table className="data-table report__tx-table">
            <thead>
              <tr>
                <th scope="col">Date</th>
                <th scope="col">Description</th>
                <th scope="col">Category</th>
                <th scope="col" className="num">Amount</th>
              </tr>
            </thead>
            {groups.map((g) => (
              <tbody key={g.key}>
                {group !== 'Monthly' && (
                  <tr className="report__tx-group">
                    <th scope="rowgroup" colSpan={4}>
                      <span className="report__tx-group-row">
                        <span>{periodLabel(group, g.key)}</span>
                        <span className="report__tx-totals">
                          <span>Spent {formatPeso(g.spent)}</span>
                          {g.saved !== 0 && <span className="report__tx-saved">{g.saved > 0 ? 'Saved' : 'Taken from savings'} {formatPeso(Math.abs(g.saved))}</span>}
                        </span>
                      </span>
                    </th>
                  </tr>
                )}
                {g.rows.map((t, i) => {
                  const savings = t.kind !== 'Expense'
                  return (
                    <tr key={`${g.key}-${i}`}>
                      <td className="data-table__muted data-table__date">{formatDate(t.date)}</td>
                      <td>
                        <span className="data-table__text">
                          <span>{savings ? `${t.kind === 'SavingsDeposit' ? 'Saved for' : 'Took out of'} ${t.category}` : t.description}</span>
                          {/* Phones hide the Date and Category columns; this line replaces them. */}
                          <span className="data-table__mobile-meta">{savings ? 'Savings' : t.category}, {formatDate(t.date)}</span>
                        </span>
                      </td>
                      <td className="data-table__category">
                        <span className="data-table__cell">
                          <CategoryIcon category={savings ? 'Savings' : t.category} icon={savings ? 'piggy' : undefined} size="sm" shape="round" />
                          {savings ? 'Savings' : t.category}
                        </span>
                      </td>
                      <td className="num data-table__strong data-table__amount">{t.kind === 'SavingsWithdrawal' ? `+${formatPeso(t.amount)}` : formatPeso(t.amount)}</td>
                    </tr>
                  )
                })}
              </tbody>
            ))}
          </table>
        </div>
      )}
      <p className="card__footnote report__tx-note">Savings moves are listed here but are not counted as spending. A withdrawal (+) goes back to your budget.</p>
    </section>
  )
}

function Figure({ label, value, note }) {
  return (
    <div className="stat">
      <dl className="stat__body">
        <dt className="stat__label">{label}</dt>
        <dd className="stat__value">{value}</dd>
        {note && <dd className="stat__note">{note}</dd>}
      </dl>
    </div>
  )
}
