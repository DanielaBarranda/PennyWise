// These names must match the Category enum in the ASP.NET Core API exactly.
export const CATEGORIES = ['Food', 'Transportation', 'School', 'Bills', 'Entertainment', 'Others']

// Category colors: distinguishable from each other, at least 3:1 contrast on white,
// and kept away from the brand burgundy/crimson so they are not confused with warnings.
export const CATEGORY_COLORS = {
  Food: '#c0532f',
  Transportation: '#2d6a8a',
  School: '#6a4c9c',
  Bills: '#8a6a10',
  Entertainment: '#a8336b',
  Others: '#56635d',
}

// Must match the threshold the API uses for its "Warning" status.
export const WARNING_PERCENT = 80

export const MAX_AMOUNT = 10_000_000
export const MAX_DESCRIPTION_LENGTH = 200
