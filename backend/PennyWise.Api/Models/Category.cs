namespace PennyWise.Api.Models;

// Stored as text in the database ("Food", not 0) so the data stays readable.
// The names must match CATEGORIES in frontend/src/utils/constants.js.
public enum Category
{
    Food,
    Transportation,
    School,
    Bills,
    Entertainment,
    Others,
}
