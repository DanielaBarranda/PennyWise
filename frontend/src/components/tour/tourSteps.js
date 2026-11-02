// What the app tour says, page by page. Each step points at one or more
// data-tour="…" markers (the first one visible on screen is used); a step with
// no marker on screen shows its card in the middle instead.
//
// The full tour walks through every page in this order. "Tour this page" in the
// menu runs only the steps for the page you are on.
export const PAGE_STEPS = {
  dashboard: [
    {
      targets: ['period'],
      title: 'Choose how you budget',
      body: 'Budget by the day, the week or the month. Each period has its own budget, and the arrows move between them.',
    },
    {
      targets: ['budget'],
      title: 'Set your budget',
      body: 'Set how much you plan to spend in this period, plus optional limits for categories like Food or School.',
    },
    {
      targets: ['add-expense'],
      title: 'Log what you spend',
      body: 'Add each expense as it happens. It counts against your budget right away, and PennyWise warns you before you overspend.',
    },
    {
      targets: ['left-to-spend'],
      title: 'See what’s left',
      body: 'How much you can still spend in this period, after spending and unpaid bills. Switch to Per day to see a daily amount.',
    },
  ],
  expenses: [
    {
      targets: ['expenses-filter'],
      title: 'Every expense, month by month',
      body: 'This lists everything you’ve logged for the month. Show one category at a time here, and use the edit and delete buttons on each row to fix mistakes.',
    },
    {
      targets: ['expenses-month'],
      title: 'Look back at other months',
      body: 'Use the arrows to see earlier months. Nothing is ever deleted when a month ends.',
    },
  ],
  savings: [
    {
      targets: ['savings-add'],
      title: 'Save for something',
      body: 'Create a goal with a name and a target amount, like a laptop or an emergency fund. A target date is optional.',
    },
    {
      targets: ['savings-goal', 'savings-empty'],
      title: 'Watch it grow',
      body: 'Add money whenever you set some aside, or withdraw if you need it. History shows every change, and finished goals can be archived.',
    },
  ],
  bills: [
    {
      targets: ['bills-add'],
      title: 'Add your regular bills',
      body: 'Rent, internet, phone load: add each bill once with how often it repeats, and PennyWise works out the due dates.',
    },
    {
      targets: ['bills-due'],
      title: 'Mark bills as paid',
      body: 'Due dates from the last and next 60 days are listed here. Mark a bill paid when you pay it, and it can be recorded as an expense too.',
    },
  ],
  insights: [
    {
      targets: ['insights-list'],
      title: 'Notes about your money',
      body: 'PennyWise points out things worth knowing, like a category close to its limit or a bill due soon. Hide the ones you’ve already read.',
    },
  ],
  reports: [
    {
      targets: ['reports-summary', 'reports-empty'],
      title: 'Your month at a glance',
      body: 'Budget, spending, savings and bills for one month, with a comparison to the month before. Use the arrows to pick a month.',
    },
    {
      targets: ['reports-export'],
      title: 'Keep a copy',
      body: 'Download the month as a CSV file for Excel or Google Sheets, or print it or save it as a PDF.',
    },
  ],
  profile: [
    {
      targets: ['profile-security'],
      title: 'Keep your account safe',
      body: 'Turn on two-step verification here, so logging in also needs a code from your phone.',
    },
    {
      targets: ['profile-preferences'],
      title: 'Your budget period',
      body: 'Choose whether the dashboard shows daily, weekly or monthly budgets. You can also change your name, photo and password on this page.',
    },
  ],
}

export const PAGE_NAMES = {
  dashboard: 'Dashboard',
  expenses: 'Expenses',
  savings: 'Savings',
  bills: 'Bills',
  insights: 'Insights',
  reports: 'Reports',
  profile: 'Profile',
}

const withRoute = (route) => PAGE_STEPS[route].map((step) => ({ ...step, route }))

// Every page in turn, with a welcome first and a pointer to "Tour this page" last.
export function fullTour() {
  return [
    {
      route: 'dashboard',
      welcome: true,
      title: (name) => `Welcome to PennyWise, ${name}`,
      body: 'This tour walks you through each page. It takes about two minutes, and you can skip it at any time.',
    },
    ...Object.keys(PAGE_STEPS).flatMap(withRoute),
    {
      route: 'profile',
      targets: ['tour-button', 'menu-button'],
      title: 'Need a reminder later?',
      body: 'Use Tour this page at the bottom of the menu to see a quick tour of whichever page you’re on. The full tour is on this page, under PennyWise app.',
    },
  ]
}

// Only the page you are on.
export function pageTour(route) {
  return PAGE_STEPS[route] ? withRoute(route) : withRoute('dashboard')
}
