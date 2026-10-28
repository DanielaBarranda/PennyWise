// These names must match the Category enum in the ASP.NET Core API exactly.
export const CATEGORIES = ['Food', 'Transportation', 'School', 'Bills', 'Entertainment', 'Others']

// Category colors are CSS variables so each theme can set its own shade
// (styles/base.css): darker on light surfaces, brighter on dark ones, at least
// 3:1 contrast on the card background either way.
export const CATEGORY_COLORS = {
  Food: 'var(--cat-food)',
  Transportation: 'var(--cat-transportation)',
  School: 'var(--cat-school)',
  Bills: 'var(--cat-bills)',
  Entertainment: 'var(--cat-entertainment)',
  Others: 'var(--cat-others)',
}

// Must match the threshold the API uses for its "Warning" status.
export const WARNING_PERCENT = 80

export const MAX_AMOUNT = 10_000_000
export const MAX_DESCRIPTION_LENGTH = 200
