using System.Security.Claims;
using LinkPulse.Api.Authentication;
using LinkPulse.Api.Caching;
using LinkPulse.Api.Data;
using LinkPulse.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LinkPulse.Api.Features.Links;

public static class LinkEndpoints
{
    private const int DefaultPage = 1;

    private const int DefaultPageSize = 20;

    private const int MaximumPageSize = 100;

    private const int MaximumGenerationAttempts = 10;

    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/links")
            .RequireAuthorization()
            .WithTags("Links");

        group.MapPost(
                string.Empty,
                CreateLinkAsync)
            .WithName("CreateShortLink");

        group.MapGet(
                string.Empty,
                GetLinksAsync)
            .WithName("GetShortLinks");

        group.MapGet(
                "/{id:guid}",
                GetLinkAsync)
            .WithName("GetShortLink");

        group.MapPut(
                "/{id:guid}",
                UpdateLinkAsync)
            .WithName("UpdateShortLink");

        group.MapDelete(
                "/{id:guid}",
                DeleteLinkAsync)
            .WithName("DeleteShortLink");
    }

    private static async Task<IResult> CreateLinkAsync(
        CreateShortLinkRequest request,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        LinkPulseDbContext dbContext,
        IShortCodeGenerator shortCodeGenerator,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var currentTime = timeProvider.GetUtcNow();

        var validationErrors =
            ShortLinkInputValidator.ValidateCreate(
                request,
                currentTime);

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(
                validationErrors);
        }

        var ownerId = principal.GetUserId();

        if (ownerId is null)
        {
            return Results.Unauthorized();
        }

        var targetUrl =
            ShortLinkInputValidator.NormalizeTargetUrl(
                request.TargetUrl!);

        if (!string.IsNullOrWhiteSpace(
                request.CustomAlias))
        {
            var customAlias =
                ShortLinkInputValidator.NormalizeAlias(
                    request.CustomAlias);

            var aliasExists =
                await dbContext.ShortLinks
                    .AsNoTracking()
                    .AnyAsync(
                        shortLink =>
                            shortLink.ShortCode
                            == customAlias,
                        cancellationToken);

            if (aliasExists)
            {
                return AliasConflict();
            }

            var customShortLink = new ShortLink(
                ownerId.Value,
                customAlias,
                targetUrl,
                currentTime,
                request.ExpiresAt);

            dbContext.ShortLinks.Add(
                customShortLink);

            try
            {
                await dbContext.SaveChangesAsync(
                    cancellationToken);
            }
            catch (DbUpdateException exception)
                when (IsUniqueViolation(exception))
            {
                return AliasConflict();
            }

            return Results.Created(
                $"/api/links/{customShortLink.Id}",
                CreateResponse(
                    customShortLink,
                    httpContext,
                    currentTime));
        }

        for (var attempt = 0;
             attempt < MaximumGenerationAttempts;
             attempt++)
        {
            var generatedCode =
                shortCodeGenerator.Generate();

            var codeExists =
                await dbContext.ShortLinks
                    .AsNoTracking()
                    .AnyAsync(
                        shortLink =>
                            shortLink.ShortCode
                            == generatedCode,
                        cancellationToken);

            if (codeExists)
            {
                continue;
            }

            var shortLink = new ShortLink(
                ownerId.Value,
                generatedCode,
                targetUrl,
                currentTime,
                request.ExpiresAt);

            dbContext.ShortLinks.Add(shortLink);

            try
            {
                await dbContext.SaveChangesAsync(
                    cancellationToken);

                return Results.Created(
                    $"/api/links/{shortLink.Id}",
                    CreateResponse(
                        shortLink,
                        httpContext,
                        currentTime));
            }
            catch (DbUpdateException exception)
                when (IsUniqueViolation(exception))
            {
                dbContext.Entry(shortLink).State =
                    EntityState.Detached;
            }
        }

        return Results.Problem(
            statusCode:
                StatusCodes.Status503ServiceUnavailable,
            title: "Short code generation failed",
            detail:
                "A unique short code could not be generated. Try the request again.");
    }

    private static async Task<IResult> GetLinksAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        LinkPulseDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var ownerId = principal.GetUserId();

        if (ownerId is null)
        {
            return Results.Unauthorized();
        }

        var paginationErrors = ReadPagination(
            httpContext.Request,
            out var page,
            out var pageSize);

        if (paginationErrors.Count > 0)
        {
            return Results.ValidationProblem(
                paginationErrors);
        }

        var query = dbContext.ShortLinks
            .AsNoTracking()
            .Where(
                shortLink =>
                    shortLink.OwnerId
                    == ownerId.Value);

        var totalCount = await query.CountAsync(
            cancellationToken);

        var shortLinks = await query
            .OrderByDescending(
                shortLink => shortLink.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var currentTime = timeProvider.GetUtcNow();

        var items = shortLinks
            .Select(
                shortLink =>
                    CreateResponse(
                        shortLink,
                        httpContext,
                        currentTime))
            .ToArray();

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(
                totalCount / (double)pageSize);

        return Results.Ok(
            new PagedShortLinksResponse(
                page,
                pageSize,
                totalCount,
                totalPages,
                items));
    }

    private static async Task<IResult> GetLinkAsync(
        Guid id,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        LinkPulseDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var ownerId = principal.GetUserId();

        if (ownerId is null)
        {
            return Results.Unauthorized();
        }

        var shortLink = await dbContext.ShortLinks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                link =>
                    link.Id == id
                    && link.OwnerId == ownerId.Value,
                cancellationToken);

        if (shortLink is null)
        {
            return LinkNotFound();
        }

        return Results.Ok(
            CreateResponse(
                shortLink,
                httpContext,
                timeProvider.GetUtcNow()));
    }

    private static async Task<IResult> UpdateLinkAsync(
        Guid id,
        UpdateShortLinkRequest request,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        LinkPulseDbContext dbContext,
        ILinkCache linkCache,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var currentTime = timeProvider.GetUtcNow();

        var validationErrors =
            ShortLinkInputValidator.ValidateUpdate(
                request,
                currentTime);

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(
                validationErrors);
        }

        var ownerId = principal.GetUserId();

        if (ownerId is null)
        {
            return Results.Unauthorized();
        }

        var shortLink = await dbContext.ShortLinks
            .SingleOrDefaultAsync(
                link =>
                    link.Id == id
                    && link.OwnerId == ownerId.Value,
                cancellationToken);

        if (shortLink is null)
        {
            return LinkNotFound();
        }

        shortLink.Update(
            ShortLinkInputValidator.NormalizeTargetUrl(
                request.TargetUrl!),
            request.ExpiresAt);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await linkCache.RemoveAsync(
            shortLink.ShortCode,
            cancellationToken);

        return Results.Ok(
            CreateResponse(
                shortLink,
                httpContext,
                currentTime));
    }

    private static async Task<IResult> DeleteLinkAsync(
        Guid id,
        ClaimsPrincipal principal,
        LinkPulseDbContext dbContext,
        ILinkCache linkCache,
        CancellationToken cancellationToken)
    {
        var ownerId = principal.GetUserId();

        if (ownerId is null)
        {
            return Results.Unauthorized();
        }

        var shortLink = await dbContext.ShortLinks
            .SingleOrDefaultAsync(
                link =>
                    link.Id == id
                    && link.OwnerId == ownerId.Value,
                cancellationToken);

        if (shortLink is null)
        {
            return LinkNotFound();
        }

        shortLink.Deactivate();

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await linkCache.RemoveAsync(
            shortLink.ShortCode,
            cancellationToken);

        return Results.NoContent();
    }

    private static ShortLinkResponse CreateResponse(
        ShortLink shortLink,
        HttpContext httpContext,
        DateTimeOffset currentTime)
    {
        var baseUrl =
            $"{httpContext.Request.Scheme}://"
            + $"{httpContext.Request.Host}"
            + $"{httpContext.Request.PathBase}";

        var shortUrl =
            $"{baseUrl}/{shortLink.ShortCode}";

        var isExpired =
            shortLink.ExpiresAt.HasValue
            && shortLink.ExpiresAt.Value
            <= currentTime;

        return new ShortLinkResponse(
            shortLink.Id,
            shortLink.ShortCode,
            shortUrl,
            shortLink.TargetUrl,
            shortLink.CreatedAt,
            shortLink.ExpiresAt,
            shortLink.IsActive,
            isExpired);
    }

    private static Dictionary<string, string[]> ReadPagination(
        HttpRequest request,
        out int page,
        out int pageSize)
    {
        page = DefaultPage;
        pageSize = DefaultPageSize;

        var errors = new Dictionary<string, string[]>(
            StringComparer.Ordinal);

        if (request.Query.TryGetValue(
                "page",
                out var pageValue)
            && (!int.TryParse(
                    pageValue.ToString(),
                    out page)
                || page < 1))
        {
            errors["page"] =
            [
                "Page must be a positive integer."
            ];
        }

        if (request.Query.TryGetValue(
                "pageSize",
                out var pageSizeValue)
            && (!int.TryParse(
                    pageSizeValue.ToString(),
                    out pageSize)
                || pageSize < 1
                || pageSize > MaximumPageSize))
        {
            errors["pageSize"] =
            [
                $"Page size must be between 1 and {MaximumPageSize}."
            ];
        }

        return errors;
    }

    private static IResult AliasConflict()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Short alias is already used",
            detail:
                "Another short link already uses the specified alias.");
    }

    private static IResult LinkNotFound()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Short link was not found",
            detail:
                "The link does not exist or does not belong to the current user.");
    }

    private static bool IsUniqueViolation(
        DbUpdateException exception)
    {
        return exception.InnerException
            is PostgresException
        {
            SqlState:
                    PostgresErrorCodes.UniqueViolation
        };
    }
}