import { CategoryIcon } from '../components/common/CategoryBadge.jsx'
import Icon from '../components/common/Icon.jsx'
import PageHeader from '../components/common/PageHeader.jsx'
import MonthPicker from '../components/common/MonthPicker.jsx'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { STATUS_LABELS, STATUS_TONES } from '../components/bills/billText.js'
import InsightList from '../components/insights/InsightList.jsx'
import { useAsync } from '../hooks/useAsync.js'
import { api } from '../services/api.js'
import { formatDate, formatMonth, formatPeso } from '../utils/format.js'

const signedPeso = (value) => `${value > 0 ? '+' : value < 0 ? '−' : ''}${formatPeso(Math.abs(value))}`
const signedPercent = (value) => (value == null ? '—' : `${value > 0 ? '+' : value < 0 ? '−' : ''}${Math.abs(value)}%`)

// Every figure comes from GET /api/reports/monthly; the CSV download is built by
// the API from the same report, so the file always matches this page.
export default function ReportsPage({ month, onMonthChange }) {
  const { data, error, reload } = useAsync(() => api.getMonthlyReport(month), [month])
  const report = data?.month === month ? data : null
  const isEmpty = report && report.expenseCount === 0 && report.budget == null && report.savings.goals.length === 0 && report.bills.due === 0

  return (
    <>
      <PageHeader
        title="Monthly report"
        subtitle={`Your money in ${formatMonth(month)}, from your saved records.`}
        controls={<MonthPicker month={month} onChange={onMonthChange} />}
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
        <div className="card">
          <EmptyState title={`Nothing recorded for ${formatMonth(month)}`} message="Reports fill in as you record expenses, budgets, bills and savings." />
        </div>
      ) : (
        <div className="report">
          <section className="report__summary" aria-label="Summary">
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
