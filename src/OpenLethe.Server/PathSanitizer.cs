using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

/// Collapses duplicate slashes in request paths before routing.
/// Ported from lethe-server/middleware/src/lib.rs::sanitize_path - the Limbus client
/// emits paths like "//api//Foo", which would otherwise 404.
public static class PathSanitizer
{
    public static IApplicationBuilder UsePathSanitizer(this IApplicationBuilder app)
    {
        return app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path.Value;

            if (!string.IsNullOrEmpty(path) && path.Contains("//", StringComparison.Ordinal))
            {
                while (path.Contains("//", StringComparison.Ordinal))
                {
                    path = path.Replace("//", "/", StringComparison.Ordinal);
                }

                ctx.Request.Path = path;
            }

            await next();
        });
    }
}
