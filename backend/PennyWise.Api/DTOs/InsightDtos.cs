namespace PennyWise.Api.DTOs;

public record InsightResponse(
    // Stable id for dismissing an insight. It includes the month and the
    // situation, so a dismissed note comes back if things change (e.g. a
    // category goes from "close to its limit" to "over its limit").
    string Key,
    string Severity, // Critical | Warning | Success | Info
    string Kind,     // Fact (from your records) | Suggestion (an idea, not a fact)
    string Title,
    string Message,
    string Link,     // page that helps: dashboard | expenses | bills | savings
    bool Dismissible);
