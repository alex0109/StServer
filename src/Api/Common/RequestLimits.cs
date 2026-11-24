using Microsoft.AspNetCore.Mvc;

namespace Api.Common;

// How large a request body may be. Kestrel's default of 30 MB let one account post huge documents and
// fill the database; most endpoints get far less, the few that carry editor documents a little more
public static class RequestLimits
{
    public const long DefaultBodyBytes = 1 * 1024 * 1024;
    // A material's rich text, e.g. a long imported article being edited
    public const long MaterialBodyBytes = 4 * 1024 * 1024;
    // A note with a handwritten drawing
    public const long NoteBodyBytes = 10 * 1024 * 1024;
    // A whole library snapshot sent by the admin panel (5 million characters at most, UTF-8)
    public const long LibraryImportBodyBytes = 16 * 1024 * 1024;

    public static TBuilder WithBodyLimit<TBuilder>(this TBuilder builder, long bytes)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new RequestSizeLimitAttribute(bytes));
}
