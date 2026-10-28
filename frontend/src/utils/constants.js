// These names must match the Category enum in the ASP.NET Core API exactly.
export const CATEGORIES = ['Food', 'Transportation', 'School', 'Bills', 'Entertainment', 'Others']

// Chart colors, chosen to stay distinguishable and to keep 3:1 contrast on white.
export const CATEGORY_COLORS = {
  Food: '#b4432f',
  Transportation: '#2f6f8f',
  School: '#6a4c9c',
  Bills: '#8a6a10',
  Entertainment: '#b0336e',
  Others: '#5f6b66',
}

// Must match the threshold the API uses for its "Warning" status.
export const WARNING_PERCENT = 80

export const MAX_AMOUNT = 10_000_000
export const MAX_DESCRIPTION_LENGTH = 200
