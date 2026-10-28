// Quick checks before sending the form. The API repeats every rule
// (DTOs/AuthDtos.cs, Auth/AuthRules.cs), so these only save a round trip.

export const PASSWORD_RULES = [
  { id: 'length', text: 'At least 8 characters', test: (p) => p.length >= 8 && p.length <= 128 },
  { id: 'letter', text: 'At least one letter', test: (p) => /\p{L}/u.test(p) },
  { id: 'number', text: 'At least one number', test: (p) => /\d/.test(p) },
]

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

function validateEmail(email) {
  const value = email.trim()
  if (!value) return 'Email is required.'
  if (!EMAIL.test(value) || value.length > 254) return 'Enter a valid email address.'
  return null
}

export function validateLogin(form) {
  const errors = {}
  const emailError = validateEmail(form.email)
  if (emailError) errors.email = emailError
  if (!form.password) errors.password = 'Password is required.'
  return errors
}

export function validateRegistration(form) {
  const errors = {}
  if (!form.name.trim()) errors.name = 'Name is required.'
  else if (form.name.trim().length > 80) errors.name = 'Name must be 80 characters or fewer.'
  const emailError = validateEmail(form.email)
  if (emailError) errors.email = emailError
  if (!PASSWORD_RULES.every((r) => r.test(form.password))) {
    errors.password = 'Password must be at least 8 characters and include a letter and a number.'
  }
  if (!form.confirmPassword) errors.confirmPassword = 'Confirm your password.'
  else if (form.confirmPassword !== form.password) errors.confirmPassword = 'Passwords do not match.'
  return errors
}
